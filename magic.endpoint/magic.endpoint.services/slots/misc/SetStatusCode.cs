/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2021, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using magic.node;
using magic.signals.contracts;
using magic.endpoint.contracts;

namespace magic.endpoint.services.slots.misc
{
    /// <summary>
    /// [request.host] slot for returning the host the request was decorated with.
    /// </summary>
    [Slot(Name = "request.host")]
    public class GetHost : ISlot
    {
        /// <summary>
        /// Implementation of your slot.
        /// </summary>
        /// <param name="signaler">Signaler used to raise signal.</param>
        /// <param name="input">Arguments to your slot.</param>
        public void Signal(ISignaler signaler, Node input)
        {
            var request = signaler.Peek<HttpRequest>("http.request");
            input.Value = request.Host;
        }
    }
}
