/*
 * Magic, Copyright(c) Thomas Hansen 2019 - thomas@gaiasoul.com
 * Licensed as Affero GPL unless an explicitly proprietary license has been obtained.
 */

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using magic.node;
using magic.node.extensions;
using magic.signals.contracts;
using magic.endpoint.services.utilities;
using magic.node.extensions.hyperlambda;

namespace magic.endpoint.services.slots
{
    /// <summary>
    /// [system.endpoints] slot for returning all dynamica Hyperlambda endpoints
    /// for your application.
    /// </summary>
    [Slot(Name = "endpoints.list")]
    public class ListEndpoints : ISlot
    {
        readonly IConfiguration _configuration;

        /// <summary>
        /// Creates an instance of your type.
        /// </summary>
        /// <param name="configuration">Configuration of your application.</param>
        public ListEndpoints(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// Implementation of your slot.
        /// </summary>
        /// <param name="signaler">Signaler that invoked your slot.</param>
        /// <param name="input">Arguments to your slot.</param>
        public void Signal(ISignaler signaler, Node input)
        {
            var rootFolder = Utilities.GetRootFolder(_configuration);
            input.AddRange(AddCustomEndpoints(
                rootFolder,
                rootFolder).ToList());
        }

        #region [ -- Private helper methods -- ]

        /*
         * Recursively traverses your folder for any dynamic Hyperlambda
         * endpoints, and returns the result to caller.
         */
        IEnumerable<Node> AddCustomEndpoints(string rootFolder, string currentFolder)
        {
            foreach (var idxFolder in Directory.GetDirectories(currentFolder))
            {
                var folder = idxFolder.Substring(rootFolder.Length);
                if (Utilities.IsLegalHttpName(folder))
                {
                    foreach (var idxVerb in GetVerbForFolder(rootFolder, idxFolder))
                    {
                        yield return idxVerb;
                    }

                    // Recursively retrieving inner folder.
                    foreach (var idx in AddCustomEndpoints(rootFolder, idxFolder))
                    {
                        yield return idx;
                    }
                }
            }
        }

        /*
         * Returns all fildes from current folder that matches some HTTP verb.
         */
        IEnumerable<Node> GetVerbForFolder(string rootFolder, string folder)
        {
            var folderFiles = Directory.GetFiles(folder, "*.hl")
                .Select(x => x.Replace("\\", "/"));

            foreach (var idxFile in folderFiles)
            {
                var filename = idxFile
                    .Substring(rootFolder.Length);

                var entities = filename.Split('.');
                if (entities.Length == 3)
                {
                    if (entities[1] == "delete")
                        yield return GetPath(entities[0], "delete", idxFile);
                    else if (entities[1] == "get")
                        yield return GetPath(entities[0], "get", idxFile);
                    else if (entities[1] == "post")
                        yield return GetPath(entities[0], "post", idxFile);
                    else if (entities[1] == "put")
                        yield return GetPath(entities[0], "put", idxFile);
                }
            }
            yield break;
        }

        /*
         * Returns a single node, representing the endpoint given
         * as verb/filename/path.
         */
        Node GetPath(string path, string verb, string filename)
        {
            /*
             * Creating our result node, and making sure we return path and verb.
             */
            var result = new Node("");
            result.Add(new Node("path", "magic/" + path)); // Must add "Route" parts.
            result.Add(new Node("verb", verb));

            /*
             * Reading the file, to figure out what type of authorization the
             * currently traversed endpoint has.
             */
            var auth = new Node("auth");
            using (var stream = File.OpenRead(filename))
            {
                var lambda = new Parser(stream).Lambda();
                foreach (var idx in lambda.Children)
                {
                    if (idx.Name == "auth.ticket.verify")
                    {
                        foreach (var idxRole in idx.GetEx<string>()
                            .Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            auth.Add(new Node("", idxRole));
                        }
                    }
                }
            }
            result.Add(auth);

            // Returning results to caller.
            return result;
        }

        #endregion
    }
}
