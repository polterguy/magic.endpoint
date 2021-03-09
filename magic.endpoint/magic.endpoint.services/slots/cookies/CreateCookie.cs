/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2020, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Linq;
using magic.node;
using magic.node.extensions;
using magic.signals.contracts;
using magic.endpoint.contracts;

namespace magic.endpoint.services.slots.cookies
{
    /// <summary>
    /// [response.cookiesset] slot for returning a cookie to the client.
    /// </summary>
    [Slot(Name = "response.cookies.set")]
    public class CreateCookie : ISlot
    {
        /// <summary>
        /// Implementation of your slot.
        /// </summary>
        /// <param name="signaler">Signaler used to raise signal.</param>
        /// <param name="input">Arguments to your slot.</param>
        public void Signal(ISignaler signaler, Node input)
        {
            var cookie = new Cookie
            {
                Name = input.GetEx<string>(),
                Value = input.Children.FirstOrDefault(x => x.Name == "value")?.GetEx<string>(),
                Expires = input.Children.FirstOrDefault(x => x.Name == "expires")?.GetEx<DateTime>(),
                HttpOnly = input.Children.FirstOrDefault(x => x.Name == "http-only")?.GetEx<bool>() ?? false,
                Secure = input.Children.FirstOrDefault(x => x.Name == "secure")?.GetEx<bool>() ?? false,
                Domain = input.Children.FirstOrDefault(x => x.Name == "domain")?.GetEx<string>(),
                Path = input.Children.FirstOrDefault(x => x.Name == "path")?.GetEx<string>(),
                SameSite = input.Children.FirstOrDefault(x => x.Name == "same-site")?.GetEx<string>(),
            };
            var response = signaler.Peek<HttpResponse>("http.response");
            response.Cookies.Add(cookie);
        }
    }
}
