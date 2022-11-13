
/*
 * Magic Cloud, copyright Aista, Ltd. See the attached LICENSE file for details.
 */

using System;
using System.Linq;
using magic.node.contracts;
using magic.node.extensions;

namespace magic.endpoint.services.utilities
{
    /// <summary>
    /// Utility class, mostly here to retrieve and set the DynamicFiles of where
    /// to resolve Hyperlambda files.
    /// </summary>
    public static class Utilities
    {
        /*
         * Returns true if request URL contains only legal characters.
         */
        internal static bool IsLegalAPIRequest(string requestUrl)
        {
            var entities = requestUrl.Split('/');
            foreach (var idxEntity in entities)
            {
                /*
                 * We need to keep track of character index in name of folder/file
                 * to allow for "hidden" files and folders.
                 */
                var idxNo = 0;
                foreach (var idxChar in idxEntity)
                {
                    switch (idxChar)
                    {
                        case '.':
                            if (idxNo > 0)
                                return false; // File or folder is not served
                            break;
                        default:
                            if (!IsLegal(idxChar))
                                return false;
                            break;
                    }
                    ++idxNo;
                }
            }
            return true;
        }

        /*
         * Returns true if this is a legal request for a file from the "/etc/www/" folder.
         */
        internal static bool IsLegalFileRequest(string url)
        {
            // Making sure we don't serve Hyperlambda files.
            if (url.EndsWith(".hl"))
                return false;

            // Splitting up URL in separate entities.
            var splits = url.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (!splits.Any())
                return true; // Request for root index.html page.

            foreach (var idx in splits)
            {
                if (idx.StartsWith("."))
                    return false; // Hidden file or folder.
            }
            return true; // OK URL!
        }

        /*
         * Returns the path to the endpoints file matching the specified
         * URL and verb.
         */
        internal static string GetEndpointFilePath(
            IRootResolver rootResolver,
            string url,
            string verb)
        {
            // Sanity checking invocation.
            if (!IsLegalAPIRequest(url))
                throw new HyperlambdaException($"The URL '{url}' is not a legal URL for Magic");

            // Turning specified URL into a full path of file.
            return rootResolver.AbsolutePath(url + $".{verb}.hl");
        }

        /*
         * Returns true if request URL is requesting a mixin page (server side rendered HTML page)
         */
        internal static bool IsHtmlFileRequest(string url)
        {
            // A mixin page either does not have a file extension or ends with ".html".
            var splits = url.Split(new char [] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (!splits.Any())
                return true; // Request for root index.html document

            // Finding filename of request.
            var filename = splits.Last();

            if (!filename.Contains("."))
                return true; // If filename does not contain "." at all, it's a mixin URL.

            if (filename.EndsWith(".html"))
                return true; // If URL ends with ".html", it's a mixin URL.

            return false; // Defaulting to statically served content.
        }

        #region [ -- Private helper methods -- ]

        /*
         * Returns true if specified character is in general a legal character for an URL endpoint name.
         */
        static bool IsLegal(char idxChar)
        {
            return idxChar == '_' ||
                idxChar == '-' ||
                (idxChar >= 'a' && idxChar <= 'z') ||
                (idxChar >= 'A' && idxChar <= 'Z') ||
                (idxChar >= '0' && idxChar <= '9');
        }

        #endregion
    }
}
