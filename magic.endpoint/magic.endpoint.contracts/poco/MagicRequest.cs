﻿/*
 * Aista Cloud, copyright Aista, Ltd. See the attached LICENSE file for details.
 * See the enclosed LICENSE file for details.
 */

using System.Collections.Generic;
using magic.node;

namespace magic.endpoint.contracts.poco
{
    /// <summary>
    /// Class wrapping content from the HTTP request in a structured format.
    /// </summary>
    public class MagicRequest
    {
        /// <summary>
        /// URL of HTTP request.
        /// </summary>
        public string URL { get; set; }

        /// <summary>
        /// HTTP verb of request.
        /// </summary>
        public string Verb { get; set; }

        /// <summary>
        /// QUERY parameters of request.
        /// </summary>
        public Dictionary<string, string> Query { get; set; } = new Dictionary<string, string>();

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

        /// <summary>
        /// Payload of request.
        /// </summary>
        public Node Payload { get; set; } = new Node();
    }
}
