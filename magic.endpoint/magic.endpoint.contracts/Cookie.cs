/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2021, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;

namespace magic.endpoint.contracts
{
    /// <summary>
    /// Class encapsulating a single cookie.
    /// </summary>
    public class Cookie
    {
        /// <summary>
        /// Name of cookie.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Value of cookie.
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Whether cookie should only be transmitted over a secure connection or not.
        /// </summary>
        public bool Secure { get; set; }

        /// <summary>
        /// Whether cookie should be hidden from client or not.
        /// </summary>
        public bool HttpOnly { get; set; }

        /// <summary>
        /// Date for when cookie should expire.
        /// </summary>
        public DateTime? Expires { get; set; }

        /// <summary>
        /// Domain for cookie.
        /// </summary>
        public string Domain { get; set; }

        /// <summary>
        /// Same site settings for cookie.
        /// </summary>
        public string SameSite { get; set; }

        /// <summary>
        /// Path for cookie.
        /// </summary>
        public string Path { get; set; }
    }
}
