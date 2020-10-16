/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2020, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace magic.endpoint.contracts
{
    /// <summary>
    /// Service interface for executing a Magic endpoint when some URL is
    /// requested.
    /// </summary>
    public interface IExecutorAsync
    {
        /// <summary>
        /// Executes an HTTP GET endpoint with the specified URL and the
        /// specified QUERY arguments.
        /// </summary>
        /// <param name="url">URL that was requested.</param>
        /// <param name="args">QUERY arguments to your endpoint.</param>
        /// <param name="headers">HTTP request headers.</param>
        /// <returns>The result of the evaluation.</returns>
        Task<HttpResponse> ExecuteGetAsync(
            string url, 
            IEnumerable<(string Name, string Value)> args,
            IEnumerable<(string Name, string Value)> headers);

        /// <summary>
        /// Executes an HTTP DELETE endpoint with the specified URL and the
        /// specified QUERY arguments.
        /// </summary>
        /// <param name="url">URL that was requested.</param>
        /// <param name="args">QUERY arguments to your endpoint.</param>
        /// <param name="headers">HTTP request headers.</param>
        /// <returns>The result of the evaluation.</returns>
        Task<HttpResponse> ExecuteDeleteAsync(
            string url, 
            IEnumerable<(string Name, string Value)> args,
            IEnumerable<(string Name, string Value)> headers);

        /// <summary>
        /// Executes an HTTP POST endpoint with the specified URL and the
        /// specified payload.
        /// </summary>
        /// <param name="url">URL that was requested.</param>
        /// <param name="args">HTTP arguments to endpoints.</param>
        /// <param name="payload">JSON payload to your endpoint.</param>
        /// <param name="headers">HTTP request headers.</param>
        /// <returns>The result of the evaluation.</returns>
        Task<HttpResponse> ExecutePostAsync(
            string url, 
            IEnumerable<(string Name, string Value)> args,
            JContainer payload,
            IEnumerable<(string Name, string Value)> headers);

        /// <summary>
        /// Executes an HTTP PUT endpoint with the specified URL and the
        /// specified payload.
        /// </summary>
        /// <param name="url">URL that was requested.</param>
        /// <param name="args">HTTP arguments to endpoints.</param>
        /// <param name="payload">JSON payload to your endpoint.</param>
        /// <param name="headers">HTTP request headers.</param>
        /// <returns>The result of the evaluation.</returns>
        Task<HttpResponse> ExecutePutAsync(
            string url, 
            IEnumerable<(string Name, string Value)> args, 
            JContainer payload,
            IEnumerable<(string Name, string Value)> headers);

        /// <summary>
        /// Executes an HTTP PATCH endpoint with the specified URL and the
        /// specified payload.
        /// </summary>
        /// <param name="url">URL that was requested.</param>
        /// <param name="args">HTTP arguments to endpoints.</param>
        /// <param name="payload">String payload to your endpoint.</param>
        /// <param name="headers">HTTP request headers.</param>
        /// <returns>The result of the evaluation.</returns>
        Task<HttpResponse> ExecutePatchAsync(
            string url, 
            IEnumerable<(string Name, string Value)> args, 
            string payload,
            IEnumerable<(string Name, string Value)> headers);
    }
}
