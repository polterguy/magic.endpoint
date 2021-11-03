/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2021, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using magic.endpoint.contracts.poco;

namespace magic.endpoint.controller.utilities
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
        internal static IActionResult JsonHandler(MagicResponse response)
        {
            // Checking if JSON is already converted into a string, at which point we return it as such.
            if (response.Content is string strContent)
                return new ContentResult { Content = strContent, StatusCode = response.Result };

            // Strongly typed JSON object, hence returning as such.
            return new JsonResult(response.Content as JToken) { StatusCode = response.Result };
        }
    }
}
