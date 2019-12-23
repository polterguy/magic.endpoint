/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using magic.endpoint.contracts;

namespace magic.endpoint.controller
{
    /// <summary>
    /// Hyperlambda controller for evaluating a Hyperlambda file, from a URL
    /// and a verb, allowing the caller tooptionally pass in arguments, if the
    /// endpoint can accept arguments.
    /// </summary>
    [Route("magic")]
    [Consumes("application/json")]
    [Produces("application/json")]
    public class EndpointController : ControllerBase
    {
        readonly IExecutorAsync _executor;

        /// <summary>
        /// Creates a new instance of your type.
        /// </summary>
        /// <param name="executor">Service implementation.</param>
        public EndpointController(IExecutorAsync executor)
        {
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        }

        /// <summary>
        /// Executes a dynamically registered Hyperlambda HTTP GET endpoint.
        /// </summary>
        /// <param name="url">The requested URL.</param>
        [HttpGet]
        [Route("{*url}")]
        public async Task<ActionResult> Get(string url)
        {
            var result = await _executor.ExecuteGetAsync(WebUtility.UrlDecode(url), GetPayload());
            return TransformToActionResult(result);
        }

        /// <summary>
        /// Executes a dynamically registered Hyperlambda HTTP DELETE endpoint.
        /// </summary>
        /// <param name="url">The requested URL.</param>
        [HttpDelete]
        [Route("{*url}")]
        public async Task<ActionResult> Delete(string url)
        {
            var result = await _executor.ExecuteDeleteAsync(WebUtility.UrlDecode(url), GetPayload());
            return TransformToActionResult(result);
        }

        /// <summary>
        /// Executes a dynamically registered Hyperlambda HTTP POST endpoint.
        /// </summary>
        /// <param name="url">The requested URL.</param>
        /// <param name="payload">Payload from client.</param>
        [HttpPost]
        [Route("{*url}")]
        public async Task<ActionResult> Post(string url, [FromBody] JContainer payload)
        {
            var result = await _executor.ExecutePostAsync(WebUtility.UrlDecode(url), payload);
            return TransformToActionResult(result);
        }

        /// <summary>
        /// Executes a dynamically registered Hyperlambda HTTP PUT endpoint.
        /// </summary>
        /// <param name="url">The requested URL.</param>
        /// <param name="payload">Payload from client.</param>
        [HttpPut]
        [Route("{*url}")]
        public async Task<ActionResult> Put(string url, [FromBody] JContainer payload)
        {
            var result = await _executor.ExecutePutAsync(WebUtility.UrlDecode(url), payload);
            return TransformToActionResult(result);
        }

        #region [ -- Private helper methods -- ]

        /*
         * Transforms from our internal HttpResponse wrapper to an ActionResult
         */
        ActionResult TransformToActionResult(HttpResponse response)
        {
            // Making sure we attach any HTTP headers to the response.
            foreach (var idx in response.Headers)
            {
                Response.Headers.Add(idx.Key, idx.Value);
            }

            // Retrieving result, if any, and returns it to caller.
            if (response.Content != null)
            {
                if (response.Content is Stream stream)
                    return new FileStreamResult(stream, response.Headers["Content-Type"]);
                return new ObjectResult(response.Content) { StatusCode = response.Result };
            }

            // If no return value exists, we return the status code only to caller.
            return new StatusCodeResult(response.Result);
        }

        /*
         * Common helper method to construct a dictionary from the request's
         * QUERY parameters, and evaluate the endpoint with the dictionary as
         * its arguments.
         */
        JContainer GetPayload()
        {
            var payload = new JObject();
            foreach (var idx in Request.Query)
            {
                payload.Add(idx.Key, new JValue(idx.Value));
            }
            return payload;
        }

        #endregion
    }
}
