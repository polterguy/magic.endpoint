/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2020, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System.Linq;
using System.Collections.Generic;
using magic.node;
using magic.signals.contracts;

namespace magic.endpoint.services.slots
{
    /// <summary>
    /// [http.response.headers.add] slot for adding a Response HTTP header that will be
    /// returned back to the client as an HTTP header.
    /// </summary>
    [Slot(Name = "http.request.headers.list")]
    public class ListHeaders : ISlot
    {
        /// <summary>
        /// Implementation of your slot.
        /// </summary>
        /// <param name="signaler">Signaler used to raise signal.</param>
        /// <param name="input">Arguments to your slot.</param>
        public void Signal(ISignaler signaler, Node input)
        {
            var headers = signaler.Peek<IEnumerable<(string Key, string Value)>>("http.request.headers");
            input.AddRange(headers.Select(x => new Node(x.Key, x.Value)));
        }
    }
}
