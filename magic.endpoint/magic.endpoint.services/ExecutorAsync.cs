/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2021, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
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

        /// <summary>
        /// Creates an instance of your type.
        /// </summary>
        /// <param name="signaler">Signaler necessary evaluate endpoint.</param>
        public ExecutorAsync(ISignaler signaler)
        {
            _signaler = signaler;
        }

        /// <inheritdoc/>
        public async Task<HttpResponse> ExecuteGetAsync(
            string url,
            IEnumerable<(string Name, string Value)> query,
            IEnumerable<(string Name, string Value)> headers,
            IEnumerable<(string Name, string Value)> cookies)
        {
            return await ExecuteUrl(url, "get", query, headers, cookies);
        }

        /// <inheritdoc/>
        public async Task<HttpResponse> ExecuteDeleteAsync(
            string url, 
            IEnumerable<(string Name, string Value)> query,
            IEnumerable<(string Name, string Value)> headers,
            IEnumerable<(string Name, string Value)> cookies)
        {
            return await ExecuteUrl(url, "delete", query, headers, cookies);
        }

        /// <inheritdoc/>
        public async Task<HttpResponse> ExecutePostAsync(
            string url,
            IEnumerable<(string Name, string Value)> query,
            Node payload,
            IEnumerable<(string Name, string Value)> headers,
            IEnumerable<(string Name, string Value)> cookies)
        {
            return await ExecuteUrl(url, "post", query, headers, cookies, payload);
        }

        /// <inheritdoc/>
        public async Task<HttpResponse> ExecutePutAsync(
            string url,
            IEnumerable<(string Name, string Value)> query,
            Node payload,
            IEnumerable<(string Name, string Value)> headers,
            IEnumerable<(string Name, string Value)> cookies)
        {
            return await ExecuteUrl(url, "put", query, headers, cookies, payload);
        }

        /// <inheritdoc/>
        public async Task<HttpResponse> ExecutePatchAsync(
            string url,
            IEnumerable<(string Name, string Value)> query,
            Node payload,
            IEnumerable<(string Name, string Value)> headers,
            IEnumerable<(string Name, string Value)> cookies)
        {
            return await ExecuteUrl(url, "patch", query, headers, cookies, payload);
        }

        #region [ -- Private helper methods -- ]

        /*
         * Executes a URL that was given QUERY arguments.
         */
        async Task<HttpResponse> ExecuteUrl(
            string url,
            string verb,
            IEnumerable<(string Name, string Value)> query,
            IEnumerable<(string Name, string Value)> headers,
            IEnumerable<(string Name, string Value)> cookies,
            Node payload = null)
        {
            url = url ?? "";

            // Making sure we never resolve to anything outside of "/modules/" folder.
            if (!url.StartsWith("modules/"))
                throw new ArgumentException($"Sorry, I cannot resolve Hyperlambda endpoints outside of the 'modules/' folder, and you tried to access '{url}'");

            // Figuring out file to execute, and doing some basic sanity check.
            var path = Utilities.GetEndpointFile(url, verb);
            if (!File.Exists(path))
                return new HttpResponse { Result = 404 };

            // Reading and parsing file as Hyperlambda.
            using (var stream = File.OpenRead(path))
            {
                var lambda = new Parser(stream).Lambda();
                AttachQueryArguments(lambda, query, payload);

                var evalResult = new Node();
                var httpResponse = new HttpResponse();
                try
                {
                    await _signaler.ScopeAsync("http.request.cookies", cookies, async () =>
                    {
                        await _signaler.ScopeAsync("http.request.headers", headers, async () =>
                        {
                            await _signaler.ScopeAsync("http.response", httpResponse, async () =>
                            {
                                await _signaler.ScopeAsync("slots.result", evalResult, async () =>
                                {
                                    await _signaler.SignalAsync("eval", lambda);
                                });
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
                    if (httpResponse.Content is IDisposable disposable2)
                        disposable2.Dispose();
                    throw;
                }
            }
        }

        /*
         * Attaches arguments (payload + query params) to lambda node.
         */
        void AttachQueryArguments(
            Node fileLambda, 
            IEnumerable<(string Name, string Value)> query, 
            Node payload)
        {
            var declaration = fileLambda.Children.FirstOrDefault(x => x.Name == ".arguments");
            declaration?.UnTie();

            var args = new Node(".arguments");

            if (query != null)
                args.AddRange(GetQueryParameters(declaration, query));

            if (payload != null)
                args.AddRange(GetPayloadParameters(declaration, payload));

            if (args.Children.Any())
                fileLambda.Insert(0, args);
        }

        /*
         * Converts if necessary, and attaches arguments found in
         * query parameters to args node, sanity checking that the
         * query parameter is allowed in the process.
         */
        IEnumerable<Node> GetQueryParameters(
            Node declaration,
            IEnumerable<(string Name, string Value)> queryParameters)
        {
            foreach (var idxArg in queryParameters)
            {
                object value = idxArg.Value;

                /*
                 * Checking if file contains a declaration at all.
                 * This is done since by default all endpoints accepts all arguments,
                 * unless an explicit [.arguments] declaration node is found.
                 */
                if (declaration != null)
                {
                    var declarationType = declaration?
                        .Children
                        .FirstOrDefault(x => x.Name == idxArg.Name)?
                        .Get<string>() ??
                        throw new ArgumentException($"I don't know how to handle the '{idxArg.Name}' query parameter");
                    value = Converter.ToObject(idxArg.Value, declarationType);
                }
                yield return new Node(idxArg.Name, value);
            }
        }

        /*
         * Converts if necessary, and attaches arguments found in
         * payload to args node, sanity checking that the
         * parameter is allowed in the process.
         */
        IEnumerable<Node> GetPayloadParameters(Node declaration, Node payload)
        {
            /*
             * Checking if file contains a declaration at all.
             * This is done since by default all endpoints accepts all arguments,
             * unless an explicit [.arguments] declaration node is found.
             */
            if (declaration != null)
            {
                foreach (var idxArg in payload.Children)
                {
                    ConvertArgumentRecursively(
                        idxArg,
                        declaration.Children.FirstOrDefault(x => x.Name == idxArg.Name));
                }
            }
            return payload.Children.ToList();
        }

        /*
         * Converts the given input argument to the type specified in the
         * declaration node. Making sure the argument is allowed for the
         * endpoint.
         */
        void ConvertArgumentRecursively(Node arg, Node declaration)
        {
            if (declaration == null)
                throw new ArgumentException($"I don't know how to handle the '{arg.Name}' argument");

            var type = declaration.Get<string>();
            if (type == "*")
                return; // Turning OFF all argument sanity checking and conversion recursively.

            // Making sure type declaration for argument exists.
            if (declaration.Value != null)
                arg.Value = Converter.ToObject(arg.Value, declaration.Get<string>());
            foreach (var idxChild in arg.Children)
            {
                ConvertArgumentRecursively(idxChild, declaration.Children.FirstOrDefault(x => x.Name == idxChild.Name));
            }
        }

        /*
         * Creates a returned payload of some sort and returning to caller.
         */
        object GetReturnValue(HttpResponse httpResponse, Node lambda)
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
                            var hyper = Generator.GetHyper(lambda.Children);
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

        #endregion
    }
}
