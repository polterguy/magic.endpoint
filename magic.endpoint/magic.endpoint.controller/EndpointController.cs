/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2020, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Net;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using magic.endpoint.contracts;

namespace magic.endpoint.controller
{
    /// <summary>
    /// Dynamic controller for exuting dynamic logic, resolved with dynamic endpoint URLs,
    /// which again is passed into the executor service, to allow it to dynamically resolve
    /// towards whatever it wants to resolve the request using.
    /// 
    /// Can be used to execute scripts and such, based upon what URL is supplied by caller.
    /// Normally used for executing Hyperlambda files, resolved to files on disc, using the URL
    /// supplied as a file path and name.
    /// 
    /// Basically, anything starting with "magic" in its name, will be resolved using this
    /// controller, as long as its a POST, PUT, GET or DELETE request.
    /// </summary>
    [Route("magic")]
    public class EndpointController : ControllerBase
    {
        readonly IExecutorAsync _executor;

        /// <summary>
        /// Creates a new instance of your controller.
        /// </summary>
        /// <param name="executor">Service implementation.</param>
        public EndpointController(IExecutorAsync executor)
        {
            _executor = executor;
        }

        /// <summary>
        /// Executes a dynamically resolved HTTP GET endpoint.
        /// </summary>
        /// <param name="url">The requested URL.</param>
        [HttpGet]
        [Route("{*url}")]
        public async Task<ActionResult> Get(string url)
        {
            return TransformToActionResult(
                await _executor.ExecuteGetAsync(
                    WebUtility.UrlDecode(url),
                    Request.Query.Select(x => (x.Key, x.Value.ToString()))));
        }

        /// <summary>
        /// Executes a dynamically registered Hyperlambda HTTP DELETE endpoint.
        /// </summary>
        /// <param name="url">The requested URL.</param>
        [HttpDelete]
        [Route("{*url}")]
        public async Task<ActionResult> Delete(string url)
        {
            return TransformToActionResult(
                await _executor.ExecuteDeleteAsync(
                    WebUtility.UrlDecode(url), 
                    Request.Query.Select(x => (x.Key, x.Value.ToString()))));
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
            return TransformToActionResult(
                await _executor.ExecutePostAsync(
                    WebUtility.UrlDecode(url),
                    Request.Query.Select(x => (x.Key, x.Value.ToString())),
                    payload));
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
            return TransformToActionResult(
                await _executor.ExecutePutAsync(
                    WebUtility.UrlDecode(url),
                    Request.Query.Select(x => (x.Key, x.Value.ToString())),
                    payload));
        }

        #region [ -- Private helper methods -- ]

        /*
         * Transforms from our internal HttpResponse wrapper to an ActionResult
         */
        ActionResult TransformToActionResult(HttpResponse response)
        {
            // Making sure we attach any explicitly added HTTP headers to the response.
            foreach (var idx in response.Headers)
            {
                Response.Headers.Add(idx.Key, idx.Value);
            }

            // Unless explicitly overridden by Hyperlambda file, we default Content-Type to JSON.
            if (!response.Headers.ContainsKey("Content-Type"))
                Response.ContentType = "application/json";

            // If empty result, we return nothing.
            if (response.Content == null)
                return new StatusCodeResult(response.Result);

            // Checking if we have a non-successful result status code.
            if (response.Result < 200 || response.Result >= 300)
            {
                // Making sure we dispose any already added streams/disposables, if already added.
                if (response.Content is IDisposable strResponse)
                    strResponse.Dispose();

                return new StatusCodeResult(response.Result);
            }

            // Converting string values to JSON if necessary.
            if (response.Content is string strContent && Response.ContentType.StartsWith("application/json"))
                return new JsonResult(JToken.Parse(strContent)) { StatusCode = response.Result };
            return new ObjectResult(response.Content) { StatusCode = response.Result };
        }

        #endregion
    }
}

