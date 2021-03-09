/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2020, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System.Linq;
using System.Collections.Generic;
using magic.node;
using magic.signals.contracts;

namespace magic.endpoint.services.slots.cookies
{
    /// <summary>
    /// [request.cookies.list] slot for listing all cookies associated with the request.
    /// </summary>
    [Slot(Name = "request.cookies.list")]
    public class ListCookies : ISlot
    {
        /// <summary>
        /// Implementation of your slot.
        /// </summary>
        /// <param name="signaler">Signaler used to raise signal.</param>
        /// <param name="input">Arguments to your slot.</param>
        public void Signal(ISignaler signaler, Node input)
        {
            var headers = signaler.Peek<IEnumerable<(string Key, string Value)>>("http.request.cookies");
            input.AddRange(headers.Select(x => new Node(x.Key, x.Value)));
        }
    }
}
