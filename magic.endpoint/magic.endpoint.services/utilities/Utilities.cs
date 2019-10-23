
/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;

namespace magic.endpoint.services.utilities
{
    /*
     * Utility class for Magic endpoint.
     */
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
                            (idx < 'A' || idx > 'Z') &&
                            (idx < '0' || idx > '9'))
                            return false;
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
            if (!IsLegalHttpName(url))
                throw new ApplicationException($"The URL '{url}' is not a legal URL for Magic");

            return RootFolder + url + $".{verb}.hl";
        }
    }
}
