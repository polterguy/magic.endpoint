/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2021, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using magic.endpoint.contracts;

namespace magic.endpoint.controller
{
    /*
     * Default Content-Type response handlers responsible for creating the response according to
     * Content-Type HTTP header provided by Hyperlambda.
     */
    internal static class ResponseHandlers
    {
        /*
         * Default JSON handler, simply returning a JsonResult to caller.
         */
        internal static IActionResult JsonHandler(HttpResponse response)
        {
            if (response.Content is string strContent)
                return new ContentResult() { Content = strContent, StatusCode = response.Result };
            return new JsonResult(response.Content as JToken) { StatusCode = response.Result };
        }

        /*
         * Default octet-stream handler, returning a stream or byte[] result to caller.
         */
        internal static IActionResult OctetStreamHandler(HttpResponse response)
        {
            if (response.Content is Stream streamResponse)
            {
                return new ObjectResult(response.Content) { StatusCode = response.Result };
            }
            else
            {
                var bytes = response.Content is byte[] rawBytes ?
                    rawBytes :
                    Convert.FromBase64String(response.Content as string);
                return new FileContentResult(bytes, "application/octet-stream");
            }
        }

        /*
         * Default Hyperlambda handler, returning Hyperlambda as string to caller.
         */
        internal static IActionResult HyperlambdaHandler(HttpResponse response)
        {
            if (response.Content is Stream streamResponse)
            {
                return new ObjectResult(response.Content) { StatusCode = response.Result };
            }
            else
            {
                var bytes = response.Content is byte[] rawBytes ?
                    rawBytes :
                    Convert.FromBase64String(response.Content as string);
                return new FileContentResult(bytes, "application/octet-stream");
            }
        }
    }
}
