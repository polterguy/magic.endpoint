/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2020, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
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
    /// Implementation of IExecutor serive contract, allowing you to
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
        /// <param name="configuration">Configuration object for application.</param>
        public ExecutorAsync(ISignaler signaler, IConfiguration configuration)
        {
            _signaler = signaler;
            _configuration = configuration;
        }

        /// <inheritdoc/>
        public async Task<HttpResponse> ExecuteGetAsync(
            string url,
            IEnumerable<(string Name, string Value)> args)
        {
            return await ExecuteUrl(url, "get", args);
        }

        /// <inheritdoc/>
        public async Task<HttpResponse> ExecuteDeleteAsync(
            string url, 
            IEnumerable<(string Name, string Value)> args)
        {
            return await ExecuteUrl(url, "delete", args);
        }

        /// <inheritdoc/>
        public async Task<HttpResponse> ExecutePostAsync(
            string url,
            IEnumerable<(string Name, string Value)> args,
            JContainer payload)
        {
            return await ExecuteUrl(url, "post", args, payload);
        }

        /// <inheritdoc/>
        public async Task<HttpResponse> ExecutePutAsync(
            string url,
            IEnumerable<(string Name, string Value)> args,
            JContainer payload)
        {
            return await ExecuteUrl(url, "put", args, payload);
        }

        #region [ -- Private helper methods -- ]

        /*
         * Executes a URL that was given QUERY arguments.
         */
        async Task<HttpResponse> ExecuteUrl(
            string url,
            string verb,
            IEnumerable<(string Name, string Value)> args,
            JContainer payload = null)
        {
            url = url ?? "";

            // Figuring out file to execute, anbd doing some basic sanity check.
            var path = Utilities.GetEndpointFile(url, verb);
            if (!File.Exists(path))
                return new HttpResponse { Result = 404 };

            // Reading and parsing file as Hyperlambda.
            using (var stream = File.OpenRead(path))
            {
                var lambda = new Parser(stream).Lambda();
                AttachArguments(lambda, url, args, payload);

                var evalResult = new Node();
                var httpResponse = new HttpResponse();
                try
                {
                    await _signaler.ScopeAsync("http.response", httpResponse, async () =>
                    {
                        await _signaler.ScopeAsync("slots.result", evalResult, async () =>
                        {
                            await _signaler.SignalAsync("wait.eval", lambda);
                        });
                    });
                    httpResponse.Content = GetReturnValue(evalResult);
                    return httpResponse;
                }
                catch
                {
                    if (evalResult.Value is IDisposable disposable)
                        disposable.Dispose();
                    if (httpResponse.Content is IDisposable disposable2)
                        disposable2.Dispose();
                    throw;
                }
            }
        }

        /*
         * Attaches arguments (payload + query params) to lambda node.
         */
        void AttachArguments(
            Node lambda, 
            string url,
            IEnumerable<(string Name, string Value)> args, 
            JContainer payload)
        {
            var fileArgs = lambda.Children.FirstOrDefault(x => x.Name == ".arguments");
            fileArgs?.UnTie();

            var argsNode = new Node(".arguments");

            if (payload != null)
            {
                argsNode.Value = payload;
                _signaler.Signal(".json2lambda-raw", argsNode);
                argsNode.Value = null; // To remove actual JContainer from node.

                /*
                 * Checking if we need to convert the individual arguments,
                 * and sanity check arguments, which is true if lambda file
                 * contains an [.arguments] declaration.
                 */
                if (fileArgs != null)
                {
                    foreach (var idxArg in argsNode.Children)
                    {
                        idxArg.Value = ConvertArgument(
                            idxArg,
                            fileArgs.Children.FirstOrDefault(x => x.Name == idxArg.Name));
                    }
                }
            }

            if (args != null)
            {
                foreach (var idxArg in args)
                {
                    object value = idxArg.Value;
                    var declaration = fileArgs?.Children.FirstOrDefault(x => x.Name == idxArg.Name);
                    if (declaration != null)
                        value = Converter.ToObject(idxArg.Value, declaration.Get<string>());
                    argsNode.Add(
                        new Node(
                            idxArg.Name,
                            value));
                }
            }

            lambda.Insert(0, argsNode);
        }

        /*
         * Converts the given input argument to the type specified in the
         * declaration node. Making sure the argument is legally given to the
         * endpoint.
         */
        object ConvertArgument(Node node, Node declaration)
        {
            if (declaration == null)
                throw new ArgumentException($"I don't know how to handle the '{node.Name}' argument");

            if (node.Value == null)
                return null; // Allowing for null values

            var type = declaration.Get<string>();
            if (string.IsNullOrEmpty(type))
            {
                // No conversion can be done on main node, but declaration node might have children.
                return ConvertRecursively(node, declaration);

            }
            else if (type == "*")
            {
                // Any object tolerated!
                return node.Value;
            }
            return Converter.ToObject(node.Value, type);
        }

        /*
         * Recursively tries to convert arguments.
         */
        private object ConvertRecursively(Node node, Node declaration)
        {
            if (declaration.Children.Any())
            {
                if (node.Children.Count() == 1 && node.Children.First().Name == "." && node.Children.First().Value == null)
                {
                    // Array!
                    if (declaration.Children.Count() != 1 || declaration.Children.First().Name != "." || declaration.Children.First().Value != null)
                        throw new ArgumentException($"We were given an array argument ('{node.Children.First().Value}') where an object argument was expected.");

                    foreach (var idxArg in node.Children.First().Children)
                    {
                        idxArg.Value = ConvertArgument(idxArg, declaration.Children.First().Children.FirstOrDefault(x => x.Name == idxArg.Name));
                    }
                }
                else
                {
                    // Object!
                    foreach (var idxArg in node.Children)
                    {
                        idxArg.Value = ConvertArgument(idxArg, declaration.Children.FirstOrDefault(x => x.Name == idxArg.Name));
                    }
                }
            }
            return node.Value;
        }

        /*
         * Creates a JContainer of some sort from the given lambda node.
         */
        object GetReturnValue(Node lambda)
        {
            // Checking if we have a value.
            if (lambda.Value != null)
            {
                if (lambda.Value is Stream)
                    return lambda.Value;
                return lambda.Get<string>();
            }

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
