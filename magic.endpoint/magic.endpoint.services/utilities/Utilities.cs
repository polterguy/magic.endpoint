
/*
 * Magic Cloud, copyright Aista, Ltd. See the attached LICENSE file for details.
 */

using magic.node.contracts;
using magic.node.extensions;

namespace magic.endpoint.services.utilities
{
    /// <summary>
    /// Utility class, mostly here to retrieve and set the RootFolder of where
    /// to resolve Hyperlambda files.
    /// </summary>
    public static class Utilities
    {
        /*
         * Returns true if request URL contains only legal characters.
         */
        internal static bool IsLegalHttpName(string requestUrl)
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
                        case '-':
                        case '_':
                            break;
                        case '.':
                            if (idxNo > 0)
                                return false; // Support for "hidden" files and folders.
                            break;
                        default:
                            if ((idxChar < 'a' || idxChar > 'z') &&
                                (idxChar < '0' || idxChar > '9'))
                                return false;
                            break;
                    }
                    ++idxNo;
                }
            }
            return true;
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
            if (!IsLegalHttpName(url))
                throw new HyperlambdaException($"The URL '{url}' is not a legal URL for Magic");

            // Making sure we resolve "magic/" folder files correctly.
            return rootResolver.RootFolder + url.TrimStart('/') + $".{verb}.hl";
        }
    }
}
