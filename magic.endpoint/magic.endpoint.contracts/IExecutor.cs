/*
 * Magic, Copyright(c) Thomas Hansen 2019 - thomas@gaiasoul.com
 * Licensed as Affero GPL unless an explicitly proprietary license has been obtained.
 */

using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace magic.endpoint.contracts
{
    /// <summary>
    /// Service interface for executing a Magic endpoint when sore URL is
    /// requested.
    /// </summary>
	public interface IExecutor
	{
        /// <summary>
        /// Executes an HTTP GET endpoint with the specified URL and the
        /// specified arguments.
        /// </summary>
        /// <param name="url">URL that was requested, mapping to some Hyperlambda
        /// file on your server.</param>
        /// <param name="args">Arguments to your endpoint.</param>
        /// <returns>The result of the evaluation.</returns>
        ActionResult ExecuteGet(string url, Dictionary<string, string> args);

        /// <summary>
        /// Executes an HTTP DELETE endpoint with the specified URL and the
        /// specified arguments.
        /// </summary>
        /// <param name="url">URL that was requested, mapping to some Hyperlambda
        /// file on your server.</param>
        /// <param name="args">Arguments to your endpoint.</param>
        /// <returns>The result of the evaluation.</returns>
        ActionResult ExecuteDelete(string url, Dictionary<string, string> args);

        /// <summary>
        /// Executes an HTTP POST endpoint with the specified URL and the
        /// specified payload.
        /// </summary>
        /// <param name="url">URL that was requested, mapping to some Hyperlambda
        /// file on your server.</param>
        /// <param name="payload">JSON payload to your endpoint.</param>
        /// <returns>The result of the evaluation.</returns>
        ActionResult ExecutePost(string url, JContainer payload);

        /// <summary>
        /// Executes an HTTP PUT endpoint with the specified URL and the
        /// specified payload.
        /// </summary>
        /// <param name="url">URL that was requested, mapping to some Hyperlambda
        /// file on your server.</param>
        /// <param name="payload">JSON payload to your endpoint.</param>
        /// <returns>The result of the evaluation.</returns>
        ActionResult ExecutePut(string url, JContainer payload);
    }
}
