/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2021, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System.Collections.Generic;

namespace magic.endpoint.contracts
{
    /// <summary>
    /// Class to help manipulate the HTTP response, by for instance allowing you to 
    /// add/modify its HTTP headers, etc.
    /// </summary>
    public class HttpResponse
    {
        /// <summary>
        /// Response HTTP headers that will be returned with HTTP response back to the client.
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// List of cookies that will be returned to client.
        /// </summary>
        public List<Cookie> Cookies { get; set; } = new List<Cookie>();

        /// <summary>
        /// The resulting HTTP response code.
        /// </summary>
        public int Result { get; set; } = 200;

        /// <summary>
        /// The actual content of your response.
        /// 
        /// Notice, if you return a stream, the stream will be copied directly
        /// to the response stream, without being loaded into memory in its entirety.
        public object Content { get; set; }
    }
}
