/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2021, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using magic.node;
using magic.node.extensions;
using magic.signals.contracts;
using magic.endpoint.contracts.poco;

namespace magic.endpoint.services.slots.misc
{
    /// <summary>
    /// [response.status.set] slot for modifying the HTTP status code of the response.
    /// </summary>
    [Slot(Name = "response.status.set")]
    public class SetStatusCode : ISlot
    {
        /// <summary>
        /// Implementation of your slot.
        /// </summary>
        /// <param name="signaler">Signaler used to raise signal.</param>
        /// <param name="input">Arguments to your slot.</param>
        public void Signal(ISignaler signaler, Node input)
        {
            var response = signaler.Peek<MagicResponse>("http.response");
            response.Result = input.GetEx<int>();
        }
    }
}
