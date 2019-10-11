/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Net;
using System.Collections.Generic;
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
        readonly IExecutor _executor;

        /// <summary>
        /// Creates a new instance of your type.
        /// </summary>
        /// <param name="executor">Service implementation.</param>
        public EndpointController(IExecutor executor)
        {
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        }

        /// <summary>
        /// Executes a dynamically registered Hyperlambda HTTP GET endpoint.
        /// </summary>
        /// <param name="url">The requested URL.</param>
        [HttpGet]
        [Route("{*url}")]
        public ActionResult Get(string url)
        {
            return _executor.ExecuteGet(WebUtility.UrlDecode(url), GetPayload());
        }

        /// <summary>
        /// Executes a dynamically registered Hyperlambda HTTP DELETE endpoint.
        /// </summary>
        /// <param name="url">The requested URL.</param>
        [HttpDelete]
        [Route("{*url}")]
        public ActionResult Delete(string url)
        {
            return _executor.ExecuteDelete(WebUtility.UrlDecode(url), GetPayload());
        }

        /// <summary>
        /// Executes a dynamically registered Hyperlambda HTTP POST endpoint.
        /// </summary>
        /// <param name="url">The requested URL.</param>
        /// <param name="payload">Payload from client.</param>
        [HttpPost]
        [Route("{*url}")]
        public ActionResult Post(string url, [FromBody] dynamic payload)
        {
            return _executor.ExecutePost(WebUtility.UrlDecode(url), payload);
        }

        /// <summary>
        /// Executes a dynamically registered Hyperlambda HTTP PUT endpoint.
        /// </summary>
        /// <param name="url">The requested URL.</param>
        /// <param name="payload">Payload from client.</param>
        [HttpPut]
        [Route("{*url}")]
        public ActionResult Put(string url, [FromBody] dynamic payload)
        {
            return _executor.ExecutePut(WebUtility.UrlDecode(url), payload);
        }

        #region [ -- Private helper methods -- ]

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
