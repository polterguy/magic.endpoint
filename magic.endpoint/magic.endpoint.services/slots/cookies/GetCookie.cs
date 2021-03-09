/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2020, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System.Linq;
using System.Collections.Generic;
using magic.node;
using magic.node.extensions;
using magic.signals.contracts;

namespace magic.endpoint.services.slots.cookies
{
    /// <summary>
    /// [request.cookies.get] slot for retrieving value of a cookie passed in through the request.
    /// </summary>
    [Slot(Name = "request.cookies.get")]
    public class GetCookie : ISlot
    {
        /// <summary>
        /// Implementation of your slot.
        /// </summary>
        /// <param name="signaler">Signaler used to raise signal.</param>
        /// <param name="input">Arguments to your slot.</param>
        public void Signal(ISignaler signaler, Node input)
        {
            var cookies = signaler.Peek<IEnumerable<(string Key, string Value)>>("http.request.cookies");
            var key = input.GetEx<string>();
            if (cookies.Any(x => x.Key == key))
                input.Value = cookies.FirstOrDefault(x => x.Key == key).Value;
            else
                input.Value = null;
        }
    }
}
