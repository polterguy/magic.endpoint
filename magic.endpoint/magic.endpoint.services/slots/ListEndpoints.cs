/*
 * Magic, Copyright(c) Thomas Hansen 2019, thomas@gaiasoul.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
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
        /// <summary>
        /// Implementation of your slot.
        /// </summary>
        /// <param name="signaler">Signaler that invoked your slot.</param>
        /// <param name="input">Arguments to your slot.</param>
        public void Signal(ISignaler signaler, Node input)
        {
            input.AddRange(AddCustomEndpoints(Utilities.RootFolder, Utilities.RootFolder).ToList());
        }

        #region [ -- Private helper methods -- ]

        /*
         * Recursively traverses your folder for any dynamic Hyperlambda
         * endpoints, and returns the result to caller.
         */
        IEnumerable<Node> AddCustomEndpoints(string rootFolder, string currentFolder)
        {
            // Looping through each folder inside of "currentFolder".
            foreach (var idxFolder in Directory.GetDirectories(currentFolder).Select(x => x.Replace("\\", "/")))
            {
                // Making sure files within this folder is legally resolved.
                var folder = idxFolder.Substring(rootFolder.Length);
                if (Utilities.IsLegalHttpName(folder))
                {
                    // Retrieves all files inside of currently iterated folder.
                    foreach (var idxFile in GetDynamicFiles(rootFolder, idxFolder))
                    {
                        yield return idxFile;
                    }

                    // Recursively retrieving inner folders of currently iterated folder.
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
        IEnumerable<Node> GetDynamicFiles(string rootFolder, string folder)
        {
            /*
             * Retrieving all Hyperlambda files inside of folder, making sure we
             * substitute all "windows slashes" with forward slash.
             */
            var folderFiles = Directory.GetFiles(folder, "*.hl").Select(x => x.Replace("\\", "/"));

            // Looping through each file in currently iterated folder.
            foreach (var idxFile in folderFiles)
            {
                /*
                 * This will remove the root folder parts of the path to the file,
                 * which we're not interested in.
                 */
                var filename = idxFile.Substring(rootFolder.Length);

                /*
                 * Verifying this is an HTTP file, which implies it must
                 * have the structure of "path.HTTP-VERB.hl", for instance "foo.get.hl".
                 */
                var entities = filename.Split('.');
                if (entities.Length == 3)
                {
                    // Returning a Node representing the currently iterated file.
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
            result.Add(new Node("path", "magic/" + path.Replace("\\", "/"))); // Must add "Route" parts.
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
                            auth.Add(new Node("", idxRole.Trim()));
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
