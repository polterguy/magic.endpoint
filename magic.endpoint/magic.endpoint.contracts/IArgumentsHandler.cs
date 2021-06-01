/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2021, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System.Collections.Generic;
using magic.node;

namespace magic.endpoint.contracts
{
    /// <summary>
    /// Service interface for handling arguments.
    /// </summary>
    public interface IArgumentsHandler
    {
        /// <summary>
        /// Responsible for attaching incoming arguments to invocation of endpoint,
        /// sanity checking arguments in the process
        /// </summary>
        /// <param name="lambda">Lambda object to execute.</param>
        /// <param name="query">Query parameters to attach to execution lambda.</param>
        /// <param name="payload">Payload to attach to execution lambda.</param>
        void Attach(Node lambda, IEnumerable<(string Name, string Value)> query, Node payload);
    }
}
