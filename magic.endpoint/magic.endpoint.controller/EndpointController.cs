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
using ms = Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using magic.node;
using magic.signals.contracts;
using magic.endpoint.contracts;
using System.Collections.Generic;

namespace magic.endpoint.controller
{
    /// <summary>
    /// Dynamic controller for executing dynamic logic, resolved with dynamic endpoint URLs,
    /// which again is passed into the executor service, to allow it to dynamically resolve
    /// towards whatever it wants to resolve the request with.
    /// 
    /// Can be used to execute scripts and such, based upon what URL is supplied by caller.
    /// Normally used for executing Hyperlambda files, resolving to files on disc, using the URL
    /// supplied as a file path and name.
    /// 
    /// Basically, anything starting with "magic" in its name, will be resolved using this
    /// controller, as long as it is a POST, PUT, GET, DELETE or PATCH request.
    /// </summary>
    [Route("magic")]
    public class EndpointController : ControllerBase
    {
        // Signal implementation needed to invoke slots.
        readonly ISignaler _signaler;

        // Service implementation responsible for executing the request.
        readonly IExecutorAsync _executor;

        /*
         * Registered Content-Type handlers, responsible for handling requests and parametrising invocation
         * according to Content-Type specified by caller.
         */
        static readonly Dictionary<string, Func<ISignaler, ms.HttpRequest, Task<Node>>> _requestHandlers =
            new Dictionary<string, Func<ISignaler, ms.HttpRequest, Task<Node>>>
        {
            {
                "application/json", (signaler, request) => RequestHandlers.JsonHandler(signaler, request)
            },
            {
                "application/x-www-form-urlencoded", (signaler, request) => RequestHandlers.UrlEncodedHandler(signaler, request)
            },
            {
                "multipart/form-data", (signaler, request) => RequestHandlers.FormDataHandler(signaler, request)
            },
            {
                "application/x-hyperlambda", (signaler, request) => RequestHandlers.HyperlambdaHandler(signaler, request)
            }
        };

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
        public async Task<IActionResult> Get(string url)
        {
            return TransformToActionResult(
                await _executor.ExecuteGetAsync(
                    WebUtility.UrlDecode(url),
                    Request.Query.Select(x => (x.Key, x.Value.ToString())),
                    Request.Headers.Select(x => (x.Key, x.Value.ToString())),
                    Request.Cookies.Select(x => (x.Key, x.Value)),
                    HttpContext.Request.Host.Value,
                    HttpContext.Request.Scheme));
        }

        /// <summary>
        /// Executes a dynamically registered Hyperlambda HTTP DELETE endpoint.
        /// </summary>
        /// <param name="url">The requested URL.</param>
        [HttpDelete]
        [Route("{*url}")]
        public async Task<IActionResult> Delete(string url)
        {
            return TransformToActionResult(
                await _executor.ExecuteDeleteAsync(
                    WebUtility.UrlDecode(url), 
                    Request.Query.Select(x => (x.Key, x.Value.ToString())),
                    Request.Headers.Select(x => (x.Key, x.Value.ToString())),
                    Request.Cookies.Select(x => (x.Key, x.Value)),
                    HttpContext.Request.Host.Value,
                    HttpContext.Request.Scheme));
        }

        /// <summary>
        /// Executes a dynamically registered Hyperlambda HTTP POST endpoint.
        /// </summary>
        /// <param name="url">The requested URL.</param>
        [HttpPost]
        [Route("{*url}")]
        public async Task<IActionResult> Post(string url)
        {
            return TransformToActionResult(
                await _executor.ExecutePostAsync(
                    WebUtility.UrlDecode(url),
                    Request.Query.Select(x => (x.Key, x.Value.ToString())),
                    await GetPayload(),
                    Request.Headers.Select(x => (x.Key, x.Value.ToString())),
                    Request.Cookies.Select(x => (x.Key, x.Value)),
                    HttpContext.Request.Host.Value,
                    HttpContext.Request.Scheme));
        }

        /// <summary>
        /// Executes a dynamically registered Hyperlambda HTTP PUT endpoint.
        /// </summary>
        /// <param name="url">The requested URL.</param>
        [HttpPut]
        [Route("{*url}")]
        public async Task<IActionResult> Put(string url)
        {
            return TransformToActionResult(
                await _executor.ExecutePutAsync(
                    WebUtility.UrlDecode(url),
                    Request.Query.Select(x => (x.Key, x.Value.ToString())),
                    await GetPayload(),
                    Request.Headers.Select(x => (x.Key, x.Value.ToString())),
                    Request.Cookies.Select(x => (x.Key, x.Value)),
                    HttpContext.Request.Host.Value,
                    HttpContext.Request.Scheme));
        }

        /// <summary>
        /// Executes a dynamically registered Hyperlambda HTTP PUT endpoint.
        /// </summary>
        /// <param name="url">The requested URL.</param>
        [HttpPatch]
        [Route("{*url}")]
        public async Task<IActionResult> Patch(string url)
        {
            return TransformToActionResult(
                await _executor.ExecutePatchAsync(
                    WebUtility.UrlDecode(url),
                    Request.Query.Select(x => (x.Key, x.Value.ToString())),
                    await GetPayload(),
                    Request.Headers.Select(x => (x.Key, x.Value.ToString())),
                    Request.Cookies.Select(x => (x.Key, x.Value)),
                    HttpContext.Request.Host.Value,
                    HttpContext.Request.Scheme));
        }

        /// <summary>
        /// Registers a Content-Type handler for specified Content-Type, allowing you to
        /// have a custom handler for specified Content-Type, that will be used to parametrise
        /// your invocations to your executor.
        /// 
        /// Notice, this method is not thread safe, and should be invoked during startup of your
        /// application, for then to later be left alone and not tampered with.
        /// </summary>
        /// <param name="contentType">Content-Type to register</param>
        /// <param name="functor">Function to be invoked once specified Content-Type is provided to your endpoints</param>
        public static void RegisterContentType(string contentType, Func<ISignaler, ms.HttpRequest, Task<Node>> functor)
        {
            _requestHandlers[contentType] = functor;
        }

        #region [ -- Private helper methods -- ]

        /*
         * Helper method to create arguments from body payload.
         */
        async Task<Node> GetPayload()
        {
            // Figuring out Content-Type of request.
            var contentType = Request.ContentType?
                .Split(';')
                .Select(x => x.Trim())
                .FirstOrDefault() ??
                "application/json";

            /*
             * Figuring out how to read request, which depends upon its Content-Type, and
             * whether or not we have a registered handler for specified Content-Type or not.
             */
            if (_requestHandlers.ContainsKey(contentType))
                return await _requestHandlers[contentType](_signaler, Request); // Specialised handler
            else
                return new Node("", null, new Node[] { new Node("body", Request.Body) }); // Default handler
        }

        /*
         * Transforms from our internal HttpResponse wrapper to an ActionResult
         */
        IActionResult TransformToActionResult(HttpResponse response)
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

            // Making sure we return the correct ActionResult according to Content-Type.
            switch (Response.ContentType.Split(';')[0])
            {
                case "application/json":
                    if (response.Content is string strContent)
                        return new ContentResult() { Content = strContent, StatusCode = response.Result };
                    return new JsonResult(response.Content as JToken) { StatusCode = response.Result };

                case "application/octet-stream":
                    if (response.Content is Stream streamResponse)
                    {
                        return new ObjectResult(response.Content) { StatusCode = response.Result };
                    }
                    else
                    {
                        var bytes = response.Content is byte[] rawBytes ?
                            rawBytes :
                            Convert.FromBase64String(response.Content as string);
                        return File(bytes, "application/octet-stream");
                    }

                case "application/x-hyperlambda":
                    return Content(response.Content as string);

                default:
                    return new ObjectResult(response.Content) { StatusCode = response.Result };
            }
        }

        #endregion
    }
}

