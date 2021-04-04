/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2021, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using magic.node;
using magic.node.extensions;
using magic.signals.contracts;
using magic.endpoint.contracts;

namespace magic.endpoint.services.slots.headers
{
    /// <summary>
    /// [request.headers.get] slot for retrieving the value of the specified HTTP header
    /// passed in by the client over the HTTP request.
    /// </summary>
    [Slot(Name = "request.headers.get")]
    public class GetHeader : ISlot
    {
        /// <summary>
        /// Implementation of your slot.
        /// </summary>
        /// <param name="signaler">Signaler used to raise signal.</param>
        /// <param name="input">Arguments to your slot.</param>
        public void Signal(ISignaler signaler, Node input)
        {
            var request = signaler.Peek<HttpRequest>("http.request");
            var key = input.GetEx<string>();
            if (request.Headers.TryGetValue(key, out string value))
                input.Value = value;
            else
                input.Value = null;
        }
    }
}
