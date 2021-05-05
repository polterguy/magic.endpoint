/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2021, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System.Collections.Generic;

namespace magic.endpoint.contracts
{
    /// <summary>
    /// Class wrapping content from the HTTP request in structured format.
    /// </summary>
    public class HttpRequest
    {
        /// <summary>
        /// Request HTTP headers provided by client.
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Cookies provided by client.
        /// </summary>
        public Dictionary<string, string> Cookies { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Host value of request.
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// Scheme of request, e.g. 'http' or 'https'.
        /// </summary>
        public string Scheme { get; set; }
    }
}
