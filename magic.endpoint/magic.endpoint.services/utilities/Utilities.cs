
/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace magic.endpoint.services.utilities
{
    /*
     * Utility class for Magic endpoint.
     */
    static internal class Utilities
    {
        /*
         * Returns true if request URL contains only legal characters.
         */
        public static bool IsLegalHttpName(string requestUrl)
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
         * Returns the root folder for you application to retrieve Hyperlambda
         * files from.
         */
        public static string GetRootFolder(IConfiguration configuration)
        {
            // Figuring out what our root folder is.
            var rootFolder = configuration["magic:endpoint:root-folder"] ?? "~/files/";
            return rootFolder
                .Replace("~", Directory.GetCurrentDirectory())
                .Replace("\\", "/");
        }

        /*
         * Returns the path to the endpoints file matching the specified
         * URL and verb.
         */
        public static string GetEndpointFile(
            IConfiguration configuration,
            string url,
            string verb)
        {
            if (!IsLegalHttpName(url))
                throw new ApplicationException($"The URL '{url}' is not a legal URL for Magic");

            return GetRootFolder(configuration) + url + $".{verb}.hl";
        }
    }
}
