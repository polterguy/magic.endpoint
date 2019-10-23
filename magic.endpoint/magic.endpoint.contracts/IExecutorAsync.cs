/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System.Threading.Tasks;
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
        /// specified arguments.
        /// </summary>
        /// <param name="response">HTTP response of your request.</param>
        /// <param name="url">URL that was requested, mapping to some Hyperlambda
        /// file on your server.</param>
        /// <param name="args">QUERY arguments to your endpoint.</param>
        /// <returns>The result of the evaluation.</returns>
        Task<HttpResponse> ExecuteGetAsync(string url, JContainer args);

        /// <summary>
        /// Executes an HTTP DELETE endpoint with the specified URL and the
        /// specified arguments.
        /// </summary>
        /// <param name="response">HTTP response of your request.</param>
        /// <param name="url">URL that was requested, mapping to some Hyperlambda
        /// file on your server.</param>
        /// <param name="args">QUERY arguments to your endpoint.</param>
        /// <returns>The result of the evaluation.</returns>
        Task<HttpResponse> ExecuteDeleteAsync(string url, JContainer args);

        /// <summary>
        /// Executes an HTTP POST endpoint with the specified URL and the
        /// specified payload.
        /// </summary>
        /// <param name="response">HTTP response of your request.</param>
        /// <param name="url">URL that was requested, mapping to some Hyperlambda
        /// file on your server.</param>
        /// <param name="payload">JSON payload to your endpoint.</param>
        /// <returns>The result of the evaluation.</returns>
        Task<HttpResponse> ExecutePostAsync(string url, JContainer payload);

        /// <summary>
        /// Executes an HTTP PUT endpoint with the specified URL and the
        /// specified payload.
        /// </summary>
        /// <param name="response">HTTP response of your request.</param>
        /// <param name="url">URL that was requested, mapping to some Hyperlambda
        /// file on your server.</param>
        /// <param name="payload">JSON payload to your endpoint.</param>
        /// <returns>The result of the evaluation.</returns>
        Task<HttpResponse> ExecutePutAsync(string url, JContainer payload);
    }
}
