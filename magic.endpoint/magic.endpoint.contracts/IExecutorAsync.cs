/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2021, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System.Threading.Tasks;
using System.Collections.Generic;
using magic.node;

namespace magic.endpoint.contracts
{
    /// <summary>
    /// Service interface for executing a dynamic Magic URL.
    /// </summary>
    public interface IExecutorAsync
    {
        /// <summary>
        /// Executes an HTTP GET endpoint with the specified URL and the
        /// specified query parameters.
        /// </summary>
        /// <param name="url">URL that was requested.</param>
        /// <param name="query">Query parameters to your endpoint.</param>
        /// <param name="headers">HTTP request headers.</param>
        /// <param name="cookies">Cookies passed in by client.</param>
        /// <returns>The result of the evaluation.</returns>
        Task<HttpResponse> ExecuteGetAsync(
            string url, 
            IEnumerable<(string Name, string Value)> query,
            IEnumerable<(string Name, string Value)> headers,
            IEnumerable<(string Name, string Value)> cookies);

        /// <summary>
        /// Executes an HTTP DELETE endpoint with the specified URL and the
        /// specified query parameters.
        /// </summary>
        /// <param name="url">URL that was requested.</param>
        /// <param name="query">Query parameters to your endpoint.</param>
        /// <param name="headers">HTTP request headers.</param>
        /// <param name="cookies">Cookies passed in by client.</param>
        /// <returns>The result of the evaluation.</returns>
        Task<HttpResponse> ExecuteDeleteAsync(
            string url, 
            IEnumerable<(string Name, string Value)> query,
            IEnumerable<(string Name, string Value)> headers,
            IEnumerable<(string Name, string Value)> cookies);

        /// <summary>
        /// Executes an HTTP POST endpoint with the specified URL and the
        /// specified payload, in addition to the specified query parameters.
        /// </summary>
        /// <param name="url">URL that was requested.</param>
        /// <param name="query">Query parameters to your endpoint.</param>
        /// <param name="payload">Payload to your endpoint in structure format.</param>
        /// <param name="headers">HTTP request headers.</param>
        /// <param name="cookies">Cookies passed in by client.</param>
        /// <returns>The result of the evaluation.</returns>
        Task<HttpResponse> ExecutePostAsync(
            string url, 
            IEnumerable<(string Name, string Value)> query,
            Node payload,
            IEnumerable<(string Name, string Value)> headers,
            IEnumerable<(string Name, string Value)> cookies);

        /// <summary>
        /// Executes an HTTP PUT endpoint with the specified URL and the
        /// specified payload, in addition to the specified query parameters.
        /// </summary>
        /// <param name="url">URL that was requested.</param>
        /// <param name="query">Query parameters to your endpoint.</param>
        /// <param name="payload">Payload to your endpoint in structure format.</param>
        /// <param name="headers">HTTP request headers.</param>
        /// <param name="cookies">Cookies passed in by client.</param>
        /// <returns>The result of the evaluation.</returns>
        Task<HttpResponse> ExecutePutAsync(
            string url, 
            IEnumerable<(string Name, string Value)> query, 
            Node payload,
            IEnumerable<(string Name, string Value)> headers,
            IEnumerable<(string Name, string Value)> cookies);

        /// <summary>
        /// Executes an HTTP PATCH endpoint with the specified URL and the
        /// specified payload, in addition to the specified query parameters.
        /// </summary>
        /// <param name="url">URL that was requested.</param>
        /// <param name="query">Query parameters to your endpoint.</param>
        /// <param name="payload">Payload to your endpoint in structure format.</param>
        /// <param name="headers">HTTP request headers.</param>
        /// <param name="cookies">Cookies passed in by client.</param>
        /// <returns>The result of the evaluation.</returns>
        Task<HttpResponse> ExecutePatchAsync(
            string url, 
            IEnumerable<(string Name, string Value)> query, 
            Node payload,
            IEnumerable<(string Name, string Value)> headers,
            IEnumerable<(string Name, string Value)> cookies);
    }
}
