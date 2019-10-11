/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
    public class ExecutorAsync : IExecutorAsync
    {
        readonly ISignaler _signaler;
        readonly IConfiguration _configuration;

        /// <summary>
        /// Creates an instance of your type.
        /// </summary>
        /// <param name="signaler">Signaler necessary evaluate endpoint.</param>
        /// <param name="configuration">Configuration for your application.</param>
        public ExecutorAsync(ISignaler signaler, IConfiguration configuration)
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
        public async Task<ActionResult> ExecuteGetAsync(string url, JContainer args)
        {
            return await ExecuteUrl(url, "get", args);
        }

        /// <summary>
        /// Executes an HTTP DELETE endpoint with the specified URL and the
        /// specified arguments.
        /// </summary>
        /// <param name="url">URL that was requested, mapping to some Hyperlambda
        /// file on your server.</param>
        /// <param name="args">Arguments to your endpoint.</param>
        /// <returns>The result of the evaluation.</returns>
        public async Task<ActionResult> ExecuteDeleteAsync(string url, JContainer args)
        {
            return await ExecuteUrl(url, "delete", args);
        }

        /// <summary>
        /// Executes an HTTP POST endpoint with the specified URL and the
        /// specified payload.
        /// </summary>
        /// <param name="url">URL that was requested, mapping to some Hyperlambda
        /// file on your server.</param>
        /// <param name="payload">JSON payload to your endpoint.</param>
        /// <returns>The result of the evaluation.</returns>
        public async Task<ActionResult> ExecutePostAsync(string url, JContainer payload)
        {
            return await ExecuteUrl(url, "post", payload);
        }

        /// <summary>
        /// Executes an HTTP PUT endpoint with the specified URL and the
        /// specified payload.
        /// </summary>
        /// <param name="url">URL that was requested, mapping to some Hyperlambda
        /// file on your server.</param>
        /// <param name="payload">JSON payload to your endpoint.</param>
        /// <returns>The result of the evaluation.</returns>
        public async Task<ActionResult> ExecutePutAsync(string url, JContainer payload)
        {
            return await ExecuteUrl(url, "put", payload);
        }

        #region [ -- Private helper methods -- ]

        /*
         * Executes a URL that was given a JSON payload of some sort.
         */
        async Task<ActionResult> ExecuteUrl(
            string url,
            string verb,
            JContainer arguments)
        {
            // Retrieving file, and verifying it exists.
            var path = Utilities.GetEndpointFile(_configuration, url, verb);
            if (!File.Exists(path))
                return new NotFoundResult();

            /*
             * Open file, parses it, and evaluates it with the specified
             * arguments given by caller.
             */
            using (var stream = File.OpenRead(path))
            {
                // Creating a lambda object out of file.
                var lambda = new Parser(stream).Lambda();

                /*
                 * Checking if file has [.arguments] node, and removing it to
                 * make sure invocation of file only has a single [.arguments]
                 * node, being the arguments supplied by caller.
                 */
                var fileArgs = lambda.Children.FirstOrDefault(x => x.Name == ".arguments");
                fileArgs?.UnTie();

                // Adding arguments from invocation to evaluated lambda node.
                var argsNode = new Node("", arguments);
                _signaler.Signal(".json2lambda-raw", argsNode);
                var convertedArgs = new Node(".arguments");
                foreach (var idxArg in argsNode.Children)
                {
                    // TODO: Recursively sanity check arguments.
                    if (idxArg.Value == null)
                        convertedArgs.Add(idxArg.Clone());
                    else if (fileArgs != null)
                        convertedArgs.Add(ConvertArgument(
                            idxArg.Name,
                            idxArg.Get<string>(),
                            fileArgs?.Children.FirstOrDefault(x => x.Name == idxArg.Name)));
                    else
                        convertedArgs.Add(new Node(idxArg.Name, idxArg.Value));
                }
                lambda.Insert(0, convertedArgs);

                var evalResult = new Node();
                await _signaler.ScopeAsync("slots.result", evalResult, async () =>
                {
                    await _signaler.SignalAsync("eval", lambda);
                });

                var result = GetReturnValue(evalResult);
                if (result != null)
                    return new OkObjectResult(result);

                // If no return value exists, we return "OK" to caller.
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
            // Checking if we have a value.
            if (lambda.Value != null)
                return lambda.Get<string>();

            // Checking if we have children.
            if (lambda.Children.Any())
            {
                var convert = new Node();
                convert.AddRange(lambda.Children.ToList());
                _signaler.Signal(".lambda2json-raw", convert);
                return convert.Value as JToken;
            }

            // Nothing here ...
            return null;
        }

        #endregion
    }
}
