/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2021, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using magic.node;
using magic.node.extensions;
using magic.signals.contracts;
using magic.endpoint.contracts.poco;
using magic.endpoint.services.utilities;
using magic.node.extensions.hyperlambda;
using magic.endpoint.contracts.contracts;

namespace magic.endpoint.services
{
    /// <summary>
    /// Implementation of IExecutor service contract, allowing you to
    /// execute a dynamically created Hyperlambda endpoint.
    /// </summary>
    public class ExecutorAsync : IExecutorAsync
    {
        readonly ISignaler _signaler;
        readonly IArgumentsHandler _argumentsHandler;

        /// <summary>
        /// Creates an instance of your type.
        /// </summary>
        /// <param name="signaler">Signaler necessary evaluate endpoint.</param>
        /// <param name="argumentsHandler">Needed to attach arguments to endpoint invocation.</param>
        public ExecutorAsync(ISignaler signaler, IArgumentsHandler argumentsHandler)
        {
            _signaler = signaler;
            _argumentsHandler = argumentsHandler;
        }

        /// <inheritdoc/>
        public async Task<MagicResponse> ExecuteAsync(MagicRequest request)
        {
            // Making sure we never resolve to anything outside of "/modules/" and "/system" folder.
            if (request.URL == null || (!request.URL.StartsWith("modules/") && !request.URL.StartsWith("system/")))
                return new MagicResponse { Result = 401 };

            // Figuring out file to execute, and doing some basic sanity check.
            var path = Utilities.GetEndpointFile(request.URL, request.Verb);
            if (!File.Exists(path))
                return new MagicResponse { Result = 404 };

            // Creating our lambda object and attaching arguments specified as query parameters, and/or payload.
            var lambda = LoadHyperlambdaFile(request.URL, path);
            _argumentsHandler.Attach(lambda, request.Query, request.Payload);

            // Creating our result wrapper, wrapping whatever the endpoint wants to return to the client.
            var evalResult = new Node();
            var httpResponse = new MagicResponse();
            var httpRequest = new MagicRequest
            {
                Cookies = request.Cookies,
                Headers = request.Headers,
                Host = request.Host,
                Scheme = request.Scheme,
            };
            try
            {
                await _signaler.ScopeAsync("http.request", httpRequest, async () =>
                {
                    await _signaler.ScopeAsync("http.response", httpResponse, async () =>
                    {
                        await _signaler.ScopeAsync("slots.result", evalResult, async () =>
                        {
                            await _signaler.SignalAsync("eval", lambda);
                        });
                    });
                });
                httpResponse.Content = GetReturnValue(httpResponse, evalResult);
                return httpResponse;
            }
            catch
            {
                if (evalResult.Value is IDisposable disposable)
                    disposable.Dispose();
                if (httpResponse.Content is IDisposable disposable2 && !object.ReferenceEquals(httpResponse.Content, evalResult.Value))
                    disposable2.Dispose();
                throw;
            }
        }

        #region [ -- Private helper methods -- ]

        /*
         * Creates a returned payload of some sort and returning to caller.
         */
        object GetReturnValue(MagicResponse httpResponse, Node lambda)
        {
            object result = null;
            if (lambda.Value != null)
            {
                // IDisposables are automatically disposed by ASP.NET Core.
                if (lambda.Value is IDisposable || lambda.Value is byte[])
                    return lambda.Value;
                result = lambda.Get<string>();
            }
            else if (lambda.Children.Any())
            {
                var isJson = true;
                if (httpResponse.Headers.ContainsKey("Content-Type"))
                {
                    switch (httpResponse.Headers["Content-Type"]?.Split(';').FirstOrDefault() ?? "application/json")
                    {
                        case "application/hyperlambda":
                        case "application/x-hyperlambda":
                            var hyper = HyperlambdaGenerator.GetHyperlambda(lambda.Children);
                            result = hyper;
                            isJson = false;
                            break;
                    }
                }
                if (isJson)
                {
                    var convert = new Node();
                    convert.AddRange(lambda.Children.ToList());
                    _signaler.Signal(".lambda2json-raw", convert);
                    result = convert.Value;
                }
            }
            return result;
        }

        /*
         * Loads the specified Hyperlambda file, braiding in any existing interceptors,
         * and returns the resulting Node to caller.
         */
        Node LoadHyperlambdaFile(string url, string path)
        {
            // Loading endpoint file and parsing as lambda into result node.
            Node result;
            using (var stream = File.OpenRead(path))
            {
                result = HyperlambdaParser.Parse(stream);
            }

            // Checking to see if interceptors exists recursively upwards in folder hierarchy.
            var splits = url.Split(new char [] {'/'}, StringSplitOptions.RemoveEmptyEntries);
            var folders = splits.Take(splits.Length - 1);
            while (true)
            {
                var current = Utilities.RootFolder + string.Join("/", folders) + "/interceptor.hl";
                if (File.Exists(current))
                {
                    using (var interceptStream = File.OpenRead(current))
                    {
                        // Getting interceptor lambda.
                        var interceptNode = HyperlambdaParser.Parse(interceptStream);

                        // Moving [.arguments] from endpoint lambda to the top of interceptor lambda if existing.
                        var args = result
                            .Children
                            .Where(x =>
                                x.Name == ".arguments" ||
                                x.Name == ".description" ||
                                x.Name == ".type" ||
                                x.Name == "auth.ticket.verify" ||
                                x.Name.StartsWith("validators."));
                        foreach (var idx in args.Reverse().ToList())
                        {
                            interceptNode.Insert(0, idx);
                        }

                        // Moving endpoint lambda to position before any [.interceptor] node found in interceptor lambda.
                        foreach (var idxLambda in new Expression("**/.interceptor").Evaluate(interceptNode).ToList())
                        {
                            // Iterating through each node in current result and injecting before currently iterated [.lambda] node.
                            foreach (var idx in result.Children)
                            {
                                idxLambda.InsertBefore(idx.Clone()); // This logic ensures we keep existing order without any fuzz.
                            }

                            // Removing currently iterated [.lambda] node in interceptor lambda object.
                            idxLambda.Parent.Remove(idxLambda);
                        }

                        // Updating result to point to interceptor root node which contains the combined result at this point.
                        result = interceptNode;
                    }
                }

                // Checking if we're at root.
                if (folders.Any())
                    break;

                // Traversing upwards in hierarchy to be able to nest interceptors upwards in hierarchy.
                folders = folders.Take(folders.Count() - 1);
            }

            // Returning result to caller.
            return result;
        }

        #endregion
    }
}
