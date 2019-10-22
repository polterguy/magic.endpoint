/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using magic.node;
using magic.node.extensions;
using magic.signals.contracts;
using magic.endpoint.services.utilities;
using magic.node.extensions.hyperlambda;

namespace magic.endpoint.services.slots
{
    /// <summary>
    /// [system.endpoint] slot for retrieving the arguments and meta
    /// information your Magic endpoint can handle.
    /// </summary>
    [Slot(Name = "endpoints.get-arguments")]
    public class GetArguments : ISlot
    {
        readonly IConfiguration _configuration;

        /// <summary>
        /// Creates an instance of your type.
        /// </summary>
        /// <param name="configuration">Configuration of your application.</param>
        public GetArguments(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// Implementation of your slot.
        /// </summary>
        /// <param name="signaler">Signaler used to raise signal.</param>
        /// <param name="input">Arguments to your slot.</param>
        public void Signal(ISignaler signaler, Node input)
        {
            // Retrieving arguments to invocation.
            var url = input.Children.First(x => x.Name == "url").GetEx<string>();
            var verb = input.Children.First(x => x.Name == "verb").GetEx<string>();
            if (!Utilities.IsLegalHttpName(url))
                throw new ApplicationException($"Oops, '{url}' is not a valid HTTP URL for Magic");

            switch (verb)
            {
                case "get":
                case "delete":
                case "post":
                case "put":
                    break;
                default:
                    throw new ApplicationException($"I don't know how to '{verb}', only 'post', 'put', 'delete' and 'get'");
            }

            // Cleaning out results.
            input.Clear();

            // Figuring out what our root folder is.
            var rootFolder = Utilities.GetRootFolder(_configuration);

            // Opening file, and trying to find its [.arguments] node.
            var filename = rootFolder + url.TrimStart('/').Substring(6) + "." + verb + ".hl";
            if (!File.Exists(filename))
                throw new ApplicationException($"No endpoint found at '{url}' for verb '{verb}'");

            using (var stream = File.OpenRead(filename))
            {
                var lambda = new Parser(stream).Lambda();
                var argsNode = lambda.Children.FirstOrDefault(x => x.Name == ".arguments");
                if (argsNode == null)
                    return;

                // We have arguments in file endpoint.
                input.AddRange(argsNode.Children.ToList());
            }
        }
    }
}
