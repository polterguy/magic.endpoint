
/*
 * Magic Cloud, copyright Aista, Ltd. See the attached LICENSE file for details.
 */

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
                        case '.':
                            if (idxNo > 0)
                                return false; // Support for "hidden" files and folders.
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

            // Turning specified URL into a full path of file.
            return rootResolver.AbsolutePath(url + $".{verb}.hl");
        }

        #region [ -- Private helper methods -- ]

        /*
         * Returns true if specified character is in general a legal character for an URL endpoint name.
         */
        static bool IsLegal(char idxChar)
        {
            return idxChar == '_' || idxChar == '-' || (idxChar >= 'a' && idxChar <= 'z') || (idxChar >= '0' && idxChar <= '9');
        }

        #endregion
    }
}
