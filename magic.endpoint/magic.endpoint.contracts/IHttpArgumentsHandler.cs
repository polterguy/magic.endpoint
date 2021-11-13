﻿/*
 * Magic Cloud, copyright Aista, Ltd. See the attached LICENSE file for details.
 */

using System.Collections.Generic;
using magic.node;

namespace magic.endpoint.contracts
{
    /// <summary>
    /// Service interface for handling arguments.
    /// </summary>
    public interface IHttpArgumentsHandler
    {
        /// <summary>
        /// Responsible for attaching incoming arguments to invocation of endpoint,
        /// sanity checking arguments and converting arguments in the process.
        /// </summary>
        /// <param name="lambda">Lambda object to execute.</param>
        /// <param name="query">Query parameters to attach to execution lambda.</param>
        /// <param name="payload">Payload to attach to execution lambda.</param>
        void Attach(
            Node lambda,
            Dictionary<string, string> query,
            Node payload);
    }
}
