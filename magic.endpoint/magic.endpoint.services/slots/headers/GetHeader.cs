/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2021, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System.Linq;
using System.Collections.Generic;
using magic.node;
using magic.node.extensions;
using magic.signals.contracts;

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
            var headers = signaler.Peek<IEnumerable<(string Key, string Value)>>("http.request.headers");
            var key = input.GetEx<string>();
            if (headers.Any(x => x.Key == key))
                input.Value = headers.FirstOrDefault(x => x.Key == key).Value;
            else
                input.Value = null;
        }
    }
}
