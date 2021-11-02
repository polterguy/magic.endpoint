/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2021, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Net;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using ms = Microsoft.AspNetCore.Http;
using magic.node;
using magic.signals.contracts;
using magic.endpoint.contracts;
using magic.endpoint.controller.utilities;

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

        /*
         * Registered Content-Type handlers, responsible for handling response and returning an IActionResult
         * according to the Content-Type of the response.
         */
        readonly static Dictionary<string, Func<HttpResponse, IActionResult>> _responseHandlers =
            new Dictionary<string, Func<HttpResponse, IActionResult>>
            {
                {
                    "application/json", (response) => ResponseHandlers.JsonHandler(response)
                },
                {
                    "application/octet-stream", (response) => ResponseHandlers.OctetStreamHandler(response)
                },
                {
                    "application/x-hyperlambda", (response) => ResponseHandlers.HyperlambdaHandler(response)
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
            return HandleResponse(
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
            return HandleResponse(
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
            return HandleResponse(
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
            return HandleResponse(
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
            return HandleResponse(
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
        /// have a custom HTTP request handler for specified Content-Type, that will be used to parametrise
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

        /// <summary>
        /// Registers a Content-Type handler for specified Content-Type, allowing you to
        /// have a custom HTTP response handler for specified Content-Type, that will be used to
        /// return an IActionResult to the core.
        /// 
        /// Notice, this method is not thread safe, and should be invoked during startup of your
        /// application, for then to later be left alone and not tampered with.
        /// </summary>
        /// <param name="contentType">Content-Type to register</param>
        /// <param name="functor">Function to be invoked once specified Content-Type is returned from your endpoint</param>
        public static void RegisterContentType(string contentType, Func<HttpResponse, IActionResult> functor)
        {
            _responseHandlers[contentType] = functor;
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
                .FirstOrDefault()?
                .ToLowerInvariant() ??
                "application/json";

            /*
             * Figuring out how to read request, which depends upon its Content-Type, and
             * whether or not we have a registered handler for specified Content-Type or not.
             */
            if (_requestHandlers.ContainsKey(contentType))
                return await _requestHandlers[contentType](_signaler, Request); // Specialised handler
            else
                return new Node("", null, new Node[] { new Node("body", Request.Body) }); // Default handler, simply adding raw Stream as [body].
        }

        /*
         * Transforms from our internal HttpResponse wrapper to an ActionResult
         */
        IActionResult HandleResponse(HttpResponse response)
        {
            // Making sure we attach any explicitly added HTTP headers to the response.
            foreach (var idx in response.Headers)
            {
                Response.Headers.Add(idx.Key, idx.Value);
            }

            // Making sure we attach all cookies.
            foreach (var idx in response.Cookies)
            {
                var options = new ms.CookieOptions
                {
                    Secure = idx.Secure,
                    Expires = idx.Expires,
                    HttpOnly = idx.HttpOnly,
                    Domain = idx.Domain,    
                    Path = idx.Path,
                };
                if (!string.IsNullOrEmpty(idx.SameSite))
                    options.SameSite = (ms.SameSiteMode)Enum.Parse(typeof(ms.SameSiteMode), idx.SameSite, true);
                Response.Cookies.Append(idx.Name, idx.Value, options);
            }

            // Unless explicitly overridden by service, we default Content-Type to JSON / UTF8.
            if (!response.Headers.ContainsKey("Content-Type") || string.IsNullOrEmpty(response.Headers["Content-Type"]))
                Response.ContentType = "application/json; char-set=utf-8";

            // Figuring out Content-Type (minus arguments).
            var contentType = Response.ContentType
                .Split(';')
                .Select(x => x.Trim())
                .FirstOrDefault()
                .ToLowerInvariant();

            // If empty result, we return nothing.
            if (response.Content == null)
                return new StatusCodeResult(response.Result);

            /*
             * Figuring out how to return response, which depends upon its Content-Type, and
             * whether or not we have a registered handler for specified Content-Type or not.
             */
            if (_responseHandlers.ContainsKey(contentType))
                return _responseHandlers[contentType](response);
            else
                return new ObjectResult(response.Content) { StatusCode = response.Result };
        }

        #endregion
    }
}

