﻿/*
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
        public async Task<HttpResponse> ExecuteGetAsync(
            string url,
            IEnumerable<(string Name, string Value)> query,
            IEnumerable<(string Name, string Value)> headers,
            IEnumerable<(string Name, string Value)> cookies,
            string host,
            string scheme)
        {
            return await ExecuteUrl(url, "get", query, headers, cookies, host, scheme);
        }

        /// <inheritdoc/>
        public async Task<HttpResponse> ExecuteDeleteAsync(
            string url, 
            IEnumerable<(string Name, string Value)> query,
            IEnumerable<(string Name, string Value)> headers,
            IEnumerable<(string Name, string Value)> cookies,
            string host,
            string scheme)
        {
            return await ExecuteUrl(url, "delete", query, headers, cookies, host, scheme);
        }

        /// <inheritdoc/>
        public async Task<HttpResponse> ExecutePostAsync(
            string url,
            IEnumerable<(string Name, string Value)> query,
            Node payload,
            IEnumerable<(string Name, string Value)> headers,
            IEnumerable<(string Name, string Value)> cookies,
            string host,
            string scheme)
        {
            return await ExecuteUrl(url, "post", query, headers, cookies, host, scheme, payload);
        }

        /// <inheritdoc/>
        public async Task<HttpResponse> ExecutePutAsync(
            string url,
            IEnumerable<(string Name, string Value)> query,
            Node payload,
            IEnumerable<(string Name, string Value)> headers,
            IEnumerable<(string Name, string Value)> cookies,
            string host,
            string scheme)
        {
            return await ExecuteUrl(url, "put", query, headers, cookies, host, scheme, payload);
        }

        /// <inheritdoc/>
        public async Task<HttpResponse> ExecutePatchAsync(
            string url,
            IEnumerable<(string Name, string Value)> query,
            Node payload,
            IEnumerable<(string Name, string Value)> headers,
            IEnumerable<(string Name, string Value)> cookies,
            string host,
            string scheme)
        {
            return await ExecuteUrl(url, "patch", query, headers, cookies, host, scheme, payload);
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
            string host,
            string scheme,
            Node payload = null)
        {
            // Making sure we never resolve to anything outside of "/modules/" folder.
            if (url == null || !url.StartsWith("modules/"))
                return new HttpResponse { Result = 401 };

            // Figuring out file to execute, and doing some basic sanity check.
            var path = Utilities.GetEndpointFile(url, verb);
            if (!File.Exists(path))
                return new HttpResponse { Result = 404 };

            // Reading and parsing file as Hyperlambda.
            using (var stream = File.OpenRead(path))
            {
                // Creating our lambda object and attaching arguments specified as query parameters, and/or payload.
                var lambda = new Parser(stream).Lambda();
                _argumentsHandler.Attach(lambda, query, payload);

                // Creating our result wrapper, wrapping whatever the endpoint wants to return to the client.
                var evalResult = new Node();
                var httpResponse = new HttpResponse();
                var httpRequest = new HttpRequest
                {
                    Cookies = cookies.ToDictionary(x => x.Name, x => x.Value),
                    Headers = headers.ToDictionary(x => x.Name, x => x.Value),
                    Host = host,
                    Scheme = scheme,
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
