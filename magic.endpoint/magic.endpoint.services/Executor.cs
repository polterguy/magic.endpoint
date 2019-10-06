/*
 * Magic, Copyright(c) Thomas Hansen 2019 - thomas@gaiasoul.com
 * Licensed as Affero GPL unless an explicitly proprietary license has been obtained.
 */

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using magic.node;
using magic.node.extensions;
using magic.signals.contracts;
using magic.endpoint.contracts;
using magic.endpoint.services.utilities;
using magic.node.extensions.hyperlambda;

namespace magic.endpoint.services
{
    /// <summary>
    /// Implementation of IExecutor contract, allowing you to
    /// execute a dynamically created Hyperlambda endpoint.
    /// </summary>
    public class Executor : IExecutor
    {
        readonly ISignaler _signaler;
        readonly IConfiguration _configuration;

        /// <summary>
        /// Creates an instance of your type.
        /// </summary>
        /// <param name="signaler">Signaler necessary evaluate endpoint.</param>
        /// <param name="configuration">Configuration for your application.</param>
        public Executor(ISignaler signaler, IConfiguration configuration)
        {
            _signaler = signaler ?? throw new ArgumentNullException(nameof(signaler));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// Executes an HTTP GET endpoint with the specified URL and the
        /// specified arguments.
        /// </summary>
        /// <param name="url">URL that was requested, mapping to some Hyperlambda
        /// file on your server.</param>
        /// <param name="args">Arguments to your endpoint.</param>
        /// <returns>The result of the evaluation.</returns>
        public ActionResult ExecuteGet(string url, Dictionary<string, string> args)
        {
            return ExecuteUrl(url, "get", args);
        }

        /// <summary>
        /// Executes an HTTP DELETE endpoint with the specified URL and the
        /// specified arguments.
        /// </summary>
        /// <param name="url">URL that was requested, mapping to some Hyperlambda
        /// file on your server.</param>
        /// <param name="args">Arguments to your endpoint.</param>
        /// <returns>The result of the evaluation.</returns>
        public ActionResult ExecuteDelete(string url, Dictionary<string, string> args)
        {
            return ExecuteUrl(url, "delete", args);
        }

        /// <summary>
        /// Executes an HTTP POST endpoint with the specified URL and the
        /// specified payload.
        /// </summary>
        /// <param name="url">URL that was requested, mapping to some Hyperlambda
        /// file on your server.</param>
        /// <param name="payload">JSON payload to your endpoint.</param>
        /// <returns>The result of the evaluation.</returns>
        public ActionResult ExecutePost(string url, JContainer payload)
        {
            return ExecuteUrl(url, "post", payload);
        }

        /// <summary>
        /// Executes an HTTP PUT endpoint with the specified URL and the
        /// specified payload.
        /// </summary>
        /// <param name="url">URL that was requested, mapping to some Hyperlambda
        /// file on your server.</param>
        /// <param name="payload">JSON payload to your endpoint.</param>
        /// <returns>The result of the evaluation.</returns>
        public ActionResult ExecutePut(string url, JContainer payload)
        {
            return ExecuteUrl(url, "put", payload);
        }

        #region [ -- Private helper methods -- ]

        /*
         * Executes a QUERY based HTTP URL.
         */
        ActionResult ExecuteUrl(
            string url,
            string verb,
            Dictionary<string, string> arguments)
        {
            // Sanity checking URL.
            if (!Utilities.IsLegalHttpName(url))
                throw new ApplicationException("Illeal URL");

            // Retrieving root folder.
            var rootFolder = Utilities.GetRootFolder(_configuration);

            // Checking that file matching URL and verb actually exists.
            var path = rootFolder + url + $".{verb}.hl";
            if (!File.Exists(path))
                return new NotFoundResult();

            /*
             * Open file, parses it, and evaluates it with the specified
             * arguments given by caller.
             */
            using (var stream = File.OpenRead(path))
            {
                var lambda = new Parser(stream).Lambda();

                /*
                 * Checking file [.arguments], and if given, removing them to
                 * make sure invocation of file only has a single [.arguments]
                 * node.
                 */
                var fileArgs = lambda.Children.Where(x => x.Name == ".arguments").ToList();
                if (fileArgs.Any())
                {
                    if (fileArgs.Count() > 1)
                        throw new ApplicationException($"URL '{url}' has an invalid [.arguments] declaration. Multiple [.arguments] nodes found in endpoint's file");

                    fileArgs.First().UnTie();
                }

                /*
                 * Adding arguments from invocation to evaluated lambda node.
                 *
                 * Notice, this will also verify that no arguments are passed in
                 * to endpoint,that also doesn't exist in the declaration
                 * [.arguments] node of the file.
                 */
                if (arguments.Count > 0)
                {
                    var argsNode = new Node(".arguments");
                    argsNode.AddRange(arguments.Select(x =>
                        ConvertArgument(x.Key, x.Value,
                            fileArgs.First().Children.FirstOrDefault(x2 => x2.Name == x.Key))));
                    lambda.Insert(0, argsNode);
                }

                // Evaluating file, now parametrized with arguments.
                _signaler.Signal("eval", lambda);

                // Converting returned nodes, if any, to JSON.
                var result = GetReturnValue(lambda);
                if (result != null)
                    return new OkObjectResult(result);

                return new OkResult();
            }
        }

        /*
         * Executes a URL that was given a JSON payload of some sort.
         */
        ActionResult ExecuteUrl(
            string url,
            string verb,
            JContainer arguments)
        {
            // Sanity checking URL.
            if (!Utilities.IsLegalHttpName(url))
                throw new ApplicationException("Illeal URL");

            // Retrieving root folder.
            var rootFolder = Utilities.GetRootFolder(_configuration);

            // Checking that file matching URL and verb actually exists.
            var path = rootFolder + url + $".{verb}.hl";
            if (!File.Exists(path))
                return new NotFoundResult();

            /*
             * Open file, parses it, and evaluates it with the specified
             * arguments given by caller.
             */
            using (var stream = File.OpenRead(path))
            {
                var lambda = new Parser(stream).Lambda();

                /*
                 * Checking file [.arguments], and if given, removing them to make sure invocation of file
                 * only has a single [.arguments] node.
                 * Notice, future improvements implies validating arguments.
                 */
                var fileArgs = lambda.Children.Where(x => x.Name == ".arguments").ToList();
                if (fileArgs.Any())
                {
                    if (fileArgs.Count() > 1)
                        throw new ApplicationException($"URL '{url}' has an invalid [.arguments] declaration. Multiple [.arguments] nodes found in endpoint's file");

                    fileArgs.First().UnTie();
                }

                // Adding arguments from invocation to evaluated lambda node.
                var argsNode = new Node("", arguments);
                _signaler.Signal(".from-json-raw", argsNode);
                var convertedArgs = new Node(".arguments");
                foreach (var idxArg in argsNode.Children)
                {
                    if (idxArg.Value == null)
                        convertedArgs.Add(idxArg.Clone()); // TODO: Recursively sanity check arguments.
                    else
                        convertedArgs.Add(
                            ConvertArgument(
                                idxArg.Name,
                                idxArg.Get<string>(),
                                fileArgs.First().Children.FirstOrDefault(x => x.Name == idxArg.Name)));
                }
                lambda.Insert(0, convertedArgs);

                _signaler.Signal("eval", lambda);

                var result = GetReturnValue(lambda);
                if (result != null)
                    return new OkObjectResult(result);

                return new OkResult();
            }
        }

        /*
         * Converts the given input argument to the type specified in the
         * declaration node.
         */
        Node ConvertArgument(string name, string value, Node declaration)
        {
            if (declaration == null)
                throw new ApplicationException($"I don't know how to handle the '{name}' argument");

            return new Node(name, Parser.ConvertStringToken(value, declaration.Get<string>()));
        }

        /*
         * Creates a JContainer of some sort from the given lambda node.
         */
        object GetReturnValue(Node lambda)
        {
            if (lambda.Value != null)
            {
                if (lambda.Value is IEnumerable<Node> list)
                {
                    var convert = new Node();
                    convert.AddRange(list.ToList());
                    _signaler.Signal(".to-json-raw", convert);
                    return convert.Value as JToken;
                }
                return JToken.Parse(lambda.Get<string>());
            }
            return null;
        }

        #endregion
    }
}
