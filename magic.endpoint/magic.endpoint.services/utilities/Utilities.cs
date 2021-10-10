
/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2021, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Linq;

namespace magic.endpoint.services.utilities
{
    /// <summary>
    /// Utility class, mostly here to retrieve and set the RootFolder of where
    /// to resolve Hyperlambda files.
    /// </summary>
    public static class Utilities
    {
        /// <summary>
        /// The root folder from where to resolve dynamic Hyperlambda files from.
        /// 
        /// Notice, this property needs to be set by client code before evaluating dynamic
        /// Hyperlambda files.
        /// </summary>
        public static string RootFolder { get; set; }

        /*
         * Returns true if request URL contains only legal characters.
         */
        internal static bool IsLegalHttpName(string requestUrl)
        {
            foreach (var idx in requestUrl)
            {
                switch (idx)
                {
                    case '-':
                    case '_':
                    case '/':
                        break;
                    default:
                        if ((idx < 'a' || idx > 'z') &&
                            (idx < '0' || idx > '9'))
                        {
                            // Supporting "xxx/.well-known" URLs and other types of "hidden" URLs.
                            var splits = requestUrl.Split('/');
                            var last = splits[splits.Length - 1];
                            if (last.StartsWith(".") &&
                                !last.Substring(1).StartsWith(".") &&
                                IsLegalHttpName(last.Substring(1)))
                                return IsLegalHttpName(string.Join("/", splits.Take(splits.Length - 1)));
                            return false;
                        }
                        break;
                }
            }
            return true;
        }

        /*
         * Returns the path to the endpoints file matching the specified
         * URL and verb.
         */
        internal static string GetEndpointFile(string url, string verb)
        {
            // Sanity checking invocation.
            if (!IsLegalHttpName(url))
                throw new ArgumentException($"The URL '{url}' is not a legal URL for Magic");

            // Making sure we resolve "magic/" folder files correctly.
            return RootFolder + url.TrimStart('/') + $".{verb}.hl";
        }
    }
}
