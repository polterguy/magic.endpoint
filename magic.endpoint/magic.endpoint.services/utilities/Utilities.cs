
/*
 * Magic, Copyright(c) Thomas Hansen 2019 - thomas@gaiasoul.com
 * Licensed as Affero GPL unless an explicitly proprietary license has been obtained.
 */

using System.IO;
using Microsoft.Extensions.Configuration;

namespace magic.endpoint.services.utilities
{
    /*
     * Utility class for Magic endpoint.
     */
    static class Utilities
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

        public static string GetRootFolder(IConfiguration configuration)
        {
            // Figuring out what our root folder is.
            var rootFolder = configuration["magic:endpoint:root-folder"] ?? "~/files/";
            return rootFolder
                .Replace("~", Directory.GetCurrentDirectory())
                .Replace("\\", "/");
        }
    }
}
