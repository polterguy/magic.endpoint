/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2020, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using magic.node;
using magic.node.extensions;
using magic.signals.contracts;
using magic.endpoint.contracts;
using magic.endpoint.services.utilities;
using magic.node.extensions.hyperlambda;
using Microsoft.AspNetCore.StaticFiles;
using magic.node.expressions;

namespace magic.endpoint.services
{
    /// <summary>
    /// Implementation of IExecutor contract, allowing you to
    /// execute a dynamically created Hyperlambda endpoint.
    /// </summary>
    public class ExecutorAsync : IExecutorAsync
    {
        readonly ISignaler _signaler;

        /// <summary>
        /// Creates an instance of your type.
        /// </summary>
        /// <param name="signaler">Signaler necessary evaluate endpoint.</param>
        public ExecutorAsync(ISignaler signaler)
        {
            _signaler = signaler ?? throw new ArgumentNullException(nameof(signaler));
        }

        /// <summary>
        /// Executes an HTTP GET endpoint with the specified URL and the
        /// specified arguments.
        /// </summary>
        /// <param name="url">URL that was requested, mapping to some Hyperlambda
        /// file on your server.</param>
        /// <param name="args">Arguments to your endpoint.</param>
        /// <returns>The result of the evaluation.</returns>
        public async Task<HttpResponse> ExecuteGetAsync(string url, JContainer args)
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
        public async Task<HttpResponse> ExecuteDeleteAsync(string url, JContainer args)
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
        public async Task<HttpResponse> ExecutePostAsync(string url, JContainer payload)
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
        public async Task<HttpResponse> ExecutePutAsync(string url, JContainer payload)
        {
            return await ExecuteUrl(url, "put", payload);
        }

        /// <summary>
        /// Retrieves a dynamic document, as in one not starting with "magic/" as its URL.
        /// Useful for CMS systems, and similar things
        /// </summary>
        /// <param name="url">Entire URL that was requested, including QUERY parameters.</param>
        /// <returns>The document requested.</returns>
        public async Task<HttpResponse> RetrieveDocument(string url)
        {
            if (url.StartsWith("static/"))
            {
                /*
                 * URL starts with "content/", hence it's a request for a static document.
                 * Notice, you normally want to configure your web server to handle this directly,
                 * however for simplicity reasons I've still added this into the core of Magic.
                 * 
                 * For instance, there is no caching or similar constructs, you'd normally want to configure
                 * a real web server with here - Yet still, to be able to easily test things, I've still added it.
                 */
                var filename = Utilities.RootFolder + url;
                if (!File.Exists(filename))
                    throw new ArgumentException("Not Found");

                var provider = new FileExtensionContentTypeProvider();
                if (!provider.TryGetContentType(filename, out string contentType))
                    contentType = "application/octet-stream";
                var httpResponse = new HttpResponse
                {
                    Content = File.OpenRead(filename)
                };
                httpResponse.Headers["Content-Type"] = contentType;
                return httpResponse;
            }
            else
            {
                // Invoking dynamic content Hyperlambda slot here.
                var evalResult = new Node();
                var httpResponse = new HttpResponse();
                await _signaler.ScopeAsync("http.response", httpResponse, async () =>
                {
                    await _signaler.ScopeAsync("slots.result", evalResult, async () =>
                    {
                        var lambda = new Node("");
                        lambda.Add(new Node("slots.signal", "magic.content.get"));
                        lambda.Children.Last().Add(new Node("url", url));
                        lambda.Add(new Node("slots.return-value", new Expression("-")));
                        await _signaler.SignalAsync("eval", lambda);

                        // Retrieving content for request.
                        httpResponse.Content = GetReturnValue(evalResult);
                    });
                });
                return httpResponse;
            }
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
        async Task<HttpResponse> ExecuteUrl(
            string url,
            string verb,
            JContainer arguments,
            bool convertArguments = true)
        {
            // Retrieving file, and verifying it exists.
            var path = Utilities.GetEndpointFile(url, verb);
            if (!File.Exists(path))
                return new HttpResponse { Result = 404 };

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
                AttachArguments(lambda, arguments, verb, convertArguments);

                /*
                 * Evaluating our lambda async, making sure we allow for the
                 * lambda object to return values, and to modify the response HTTP headers,
                 * and its status code, etc.
                 */
                var evalResult = new Node();
                var httpResponse = new HttpResponse();
                await _signaler.ScopeAsync("http.response", httpResponse, async () =>
                {
                    await _signaler.ScopeAsync("slots.result", evalResult, async () =>
                    {
                        await _signaler.SignalAsync("eval", lambda);
                    });
                });

                // Retrieving content for request.
                httpResponse.Content = GetReturnValue(evalResult);
                return httpResponse;
            }
        }

        /*
         * Attaches the specified JContainer values as arguments to the given
         * lambda object, doing some basic sanity checking in the process,
         * and also possibly converting the arguments to their correct type in
         * the process.
         */
        void AttachArguments(Node lambda, JContainer arguments, string verb, bool convertArguments)
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
             * Checking if we need to convert the individual arguments.
             * 
             * Notice, we only attempt to convert arguments if Hyperlambda endpoint
             * file contains an [.arguments] node.
             */
            if (convertArguments && fileArgs != null)
            {
                /*
                 * Notice, we might have to convert the arguments passed into this endpoint,
                 * unless they were passed in as something else but a string.
                 * 
                 * If arguments are given to this method, that are *not* strings, we assume they're
                 * already of the correct type somehow.
                 */
                foreach (var idxArg in argsNode.Children)
                {
                    /*
                     * Notice, GET and DELETE invocations cannot legally have children nodes in their arguments.
                     */
                    if ((verb == "get" || verb == "delete") && idxArg.Children.Any())
                        throw new ArgumentException($"The argument '{idxArg.Name}' had children, which is not allowed for GET or DELETE requests.");

                    // Converting argument according to [.arguments] declaration node.
                    idxArg.Value = ConvertArgument(idxArg, fileArgs.Children.FirstOrDefault(x => x.Name == idxArg.Name));
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
                 * 
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
        object ConvertArgument(Node node, Node declaration)
        {
            if (declaration == null)
                throw new ApplicationException($"I don't know how to handle the '{node.Name}' argument");

            if (node.Value == null)
                return null; // Allowing for null values

            var type = declaration.Get<string>();
            if (string.IsNullOrEmpty(type))
            {
                // No conversion can be done on main node, but declaration node might have children.
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

            } else if (type == "*")
            {
                // Any object tolerated!
                return node.Value;
            }
            return Parser.ConvertValue(node.Value, type);
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
