/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2021, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using magic.node;
using magic.signals.contracts;
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
        readonly ISignaler _signaler;

        readonly IExecutorAsync _executor;

        /// <summary>
        /// Creates a new instance of your controller.
        /// </summary>
        /// <param name="executor">Service implementation for executing URLs.</param>
        /// <param name="signaler">Super signals implementation, needed to convert from JSON to Node.</param>
        public EndpointController(IExecutorAsync executor, ISignaler signaler)
        {
            _executor = executor;
            _signaler = signaler;
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
                    Request.Query.Select(x => (x.Key, x.Value.ToString())),
                    Request.Headers.Select(x => (x.Key, x.Value.ToString())),
                    Request.Cookies.Select(x => (x.Key, x.Value))));
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
                    Request.Query.Select(x => (x.Key, x.Value.ToString())),
                    Request.Headers.Select(x => (x.Key, x.Value.ToString())),
                    Request.Cookies.Select(x => (x.Key, x.Value))));
        }

        /// <summary>
        /// Executes a dynamically registered Hyperlambda HTTP POST endpoint.
        /// </summary>
        /// <param name="url">The requested URL.</param>
        /// <param name="payload">Payload from client.</param>
        [HttpPost]
        [Route("{*url}")]
        [DisableRequestSizeLimit]
        public async Task<ActionResult> Post(string url)
        {
            return TransformToActionResult(
                await _executor.ExecutePostAsync(
                    WebUtility.UrlDecode(url),
                    Request.Query.Select(x => (x.Key, x.Value.ToString())),
                    await GetPayload(),
                    Request.Headers.Select(x => (x.Key, x.Value.ToString())),
                    Request.Cookies.Select(x => (x.Key, x.Value))));
        }

        /// <summary>
        /// Executes a dynamically registered Hyperlambda HTTP PUT endpoint.
        /// </summary>
        /// <param name="url">The requested URL.</param>
        /// <param name="payload">Payload from client.</param>
        [HttpPut]
        [Route("{*url}")]
        [DisableRequestSizeLimit]
        public async Task<ActionResult> Put(string url)
        {
            return TransformToActionResult(
                await _executor.ExecutePutAsync(
                    WebUtility.UrlDecode(url),
                    Request.Query.Select(x => (x.Key, x.Value.ToString())),
                    await GetPayload(),
                    Request.Headers.Select(x => (x.Key, x.Value.ToString())),
                    Request.Cookies.Select(x => (x.Key, x.Value))));
        }

        /// <summary>
        /// Executes a dynamically registered Hyperlambda HTTP PUT endpoint.
        /// </summary>
        /// <param name="url">The requested URL.</param>
        [HttpPatch]
        [Route("{*url}")]
        public async Task<ActionResult> Patch(string url)
        {
            return TransformToActionResult(
                await _executor.ExecutePatchAsync(
                    WebUtility.UrlDecode(url),
                    Request.Query.Select(x => (x.Key, x.Value.ToString())),
                    await GetPayload(),
                    Request.Headers.Select(x => (x.Key, x.Value.ToString())),
                    Request.Cookies.Select(x => (x.Key, x.Value))));
        }

        #region [ -- Private helper methods -- ]

        /*
         * Helper method to create arguments from body payload.
         */
        async Task<Node> GetPayload()
        {
            switch (Request.ContentType)
            {
                case "application/x-www-form-urlencoded":
                {

                    // URL encoded transmission, having potentially multiple arguments.
                    var collection = await Request.ReadFormAsync();
                    var args = new Node();
                    foreach (var idx in collection)
                    {
                        args.Add(new Node(idx.Key, idx.Value.ToString()));
                    }
                    return args;
                }

                case "application/json":
                case "application/javascript":
                {

                    // Reading body as JSON from request.
                    using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
                    {  
                        var payload = await reader.ReadToEndAsync();
                        var json = JToken.Parse(payload);
                        var args = new Node("", json);
                        _signaler.Signal(".json2lambda-raw", args);
                        return args;
                    }
                }

                case "text/plain":
                case "application/hyperlambda":
                case "application/x-hyperlambda":
                {

                    // Anything BUT MIME and URL encoded parameters transmission
                    using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
                    {  
                        var payload = await reader.ReadToEndAsync();
                        return new Node("body", payload);
                    }
                }

                default:
                {

                    // Binary content of some sort, e.g. image upload etc.
                    using (var rawStream = new MemoryStream())
                    {
                        await Request.Body.CopyToAsync(rawStream);
                        return new Node("body", rawStream.GetBuffer());
                    }
                }
            }
        }

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

            // Making sure we attach all cookies.
            foreach (var idx in response.Cookies)
            {
                var options = new Microsoft.AspNetCore.Http.CookieOptions
                {
                    Secure = idx.Secure,
                    Expires = idx.Expires,
                    HttpOnly = idx.HttpOnly,
                    Domain = idx.Domain,
                    Path = idx.Path,
                };
                if (!string.IsNullOrEmpty(idx.SameSite))
                    options.SameSite = (Microsoft.AspNetCore.Http.SameSiteMode)Enum.Parse(typeof(Microsoft.AspNetCore.Http.SameSiteMode), idx.SameSite, true);
                Response.Cookies.Append(idx.Name, idx.Value, options);
            }

            // If empty result, we return nothing.
            if (response.Content == null)
                return new StatusCodeResult(response.Result);

            // Unless explicitly overridden by service, we default Content-Type to JSON.
            if (!response.Headers.ContainsKey("Content-Type"))
                Response.ContentType = "application/json";

            // Null value.
            if (response.Content == null)
                return new ObjectResult(response.Content) { StatusCode = response.Result };

            // Making sure we return the correct ActionResult according to Content-Type
            switch (Response.ContentType.Split(';')[0])
            {
                case "application/json":
                    if (response.Content is string strContent)
                        return new JsonResult(JToken.Parse(strContent)) { StatusCode = response.Result };
                    return new ObjectResult(response.Content) { StatusCode = response.Result };

                case "application/octet-stream":
                    var bytes = response.Content is byte[] rawBytes ?
                        rawBytes :
                        Convert.FromBase64String(response.Content as string);
                    return File(bytes, "application/octet-stream");

                case "application/hyperlambda":
                case "application/x-hyperlambda":
                    return Content(response.Content as string);

                default:
                    return new ObjectResult(response.Content) { StatusCode = response.Result };
            }
        }

        #endregion
    }
}

