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
            return await ExecuteUrl(url, "post", payload, false);
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
            return await ExecuteUrl(url, "put", payload, false);
        }

        #region [ -- Private helper methods -- ]

        /*
         * Executes a URL that was given a JSON payload of some sort.
         *
         * Notice, the JSON payload might also have been created by the QUERY
         * parameters, and not necessarily passed in as JSON to the endpoint.
         * If the latter is true, we must convert the argument from its string
         * representation, to the type declaration found in the [.arguments]
         * declaration node of the endpoint's file.
         */
        async Task<ActionResult> ExecuteUrl(
            string url,
            string verb,
            JContainer arguments,
            bool convertArguments = true)
        {
            // Retrieving file, and verifying it exists.
            var path = Utilities.GetEndpointFile(_configuration, url, verb);
            if (!File.Exists(path))
                return new NotFoundResult();

            // Reading and parsing file as Hyperlambda.
            using (var stream = File.OpenRead(path))
            {
                // Creating a lambda object out of file.
                var lambda = new Parser(stream).Lambda();

                /*
                 * Attaching arguments to lambda, which will also to some
                 * extent sanity check the arguments, and possibly convert
                 * them according to the declaration node.
                 */
                AttachArguments(lambda, arguments, convertArguments);

                /*
                 * Evaluating our lambda async, making sure we allow for the
                 * lambda object to return values.
                 */
                var evalResult = new Node();
                await _signaler.ScopeAsync("slots.result", evalResult, async () =>
                {
                    await _signaler.SignalAsync("eval", lambda);
                });

                /*
                 * Retrieving return value, if any, and returns success
                 * to caller.
                 */
                var result = GetReturnValue(evalResult);
                if (result != null)
                    return new OkObjectResult(result);

                // If no return value exists, we return "OK" to caller.
                return new OkResult();
            }
        }

        /*
         * Attaches the specified JContainer values as arguments to the given
         * lambda object, doing some basic sanity checking in the process,
         * and also possibly converting the arguments to their correct type in
         * the process.
         */
        void AttachArguments(Node lambda, JContainer arguments, bool convertArguments)
        {
            /*
             * Checking if file has [.arguments] node, and removing it to
             * make sure invocation of file only has a single [.arguments]
             * node, being the arguments supplied by caller, and not the
             * declaration [.arguments] node for the file.
             */
            var fileArgs = lambda.Children.FirstOrDefault(x => x.Name == ".arguments");
            fileArgs?.UnTie();

            /*
             * Converting the given arguments from JSON to lambda.
             */
            var argsNode = new Node(".arguments", arguments);
            _signaler.Signal(".json2lambda-raw", argsNode);

            /*
             * Checking if we need to convert the individual arguments, which
             * is true if they were supplied as QUERY parameters, since
             * everything is passed in as strings if it's a QUERY parameter.
             */
            if (convertArguments)
            {
                /*
                 * Notice, if we need to convert the arguments, it implies
                 * they were given as QUERY parameters - At which point there
                 * will not be recursively given arguments, since that would
                 * be impossible. Hence, we can ignore the children collection
                 * of each argument.
                 */
                foreach (var idxArg in argsNode.Children)
                {
                    idxArg.Value = ConvertArgument(
                        idxArg.Name,
                        idxArg.Get<string>(),
                        fileArgs.Children.FirstOrDefault(x => x.Name == idxArg.Name));
                }
            }
            else if (fileArgs != null)
            {
                /*
                 * Only doing some basic sanity checking.
                 *
                 * Notice, we do not recursively sanity check arguments, to
                 * allow for passing in any type of objects - At which point
                 * sanity checking is left as an exercize for the particular
                 * endpoint implementation.
                 * TODO: Consider sanity checking arguments recursively, which
                 * would imply supporting "any argument type", implies additional
                 * logic, possibly avoiding an [.arguments] declaration on
                 * file entirely.
                 */
                foreach (var idx in argsNode.Children)
                {
                    if (!fileArgs.Children.Any(x => x.Name == idx.Name))
                        throw new ApplicationException($"I don't know how to handle the '{idx.Name}' argument");
                }
            }

            /*
             * Inserting the arguments specified to the endpoint as arguments
             * inside of our lambda object.
             */
            lambda.Insert(0, argsNode);
        }

        /*
         * Converts the given input argument to the type specified in the
         * declaration node. Making sure the argument is legally given to the
         * endpoint.
         */
        object ConvertArgument(string name, string value, Node declaration)
        {
            if (declaration == null)
                throw new ApplicationException($"I don't know how to handle the '{name}' argument");

            return Parser.ConvertStringToken(value, declaration.Get<string>());
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
