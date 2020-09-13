/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2020, thomas@servergardens.com, all rights reserved.
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
            input.AddRange(AddCustomEndpoints(
                Utilities.RootFolder,
                Utilities.RootFolder + "modules/").ToList());
        }

        #region [ -- Private helper methods -- ]

        /*
         * Recursively traverses your folder for any dynamic Hyperlambda
         * endpoints, and returns the result to caller.
         */
        IEnumerable<Node> AddCustomEndpoints(string rootFolder, string currentFolder)
        {
            // Looping through each folder inside of "currentFolder".
            var folders = Directory
                .GetDirectories(currentFolder)
                .Select(x => x.Replace("\\", "/"))
                .ToList();
            folders.Sort();
            foreach (var idxFolder in folders)
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
            // Looping through each file in current folder.
            var files = Directory
                .GetFiles(folder, "*.hl")
                .Select(x => x.Replace("\\", "/"))
                .ToList();
            files.Sort();
            foreach (var idxFile in files)
            {
                // Removing the root folder, to return only relativ filename back to caller.
                var filename = idxFile.Substring(rootFolder.Length);

                // Making sure we only return files with format of "foo.xxx.hl", where xxx is some valid HTTP verb.
                var entities = filename.Split('.');
                if (entities.Length == 3)
                {
                    // Returning a Node representing the currently iterated file.
                    switch (entities[1])
                    {
                        case "delete":
                        case "put":
                        case "post":
                        case "get":
                            yield return GetFilenameMetaData(entities[0], entities[1], idxFile);
                            break;
                    }
                }
            }
        }

        /*
         * Returns a single node, representing the endpoint given
         * as verb/filename/path, and its associated meta information.
         */
        Node GetFilenameMetaData(
            string path,
            string verb,
            string filename)
        {
            // Creating our result node, and making sure we return path and verb.
            var result = new Node("");
            result.Add(new Node("path", "magic/" + path.Replace("\\", "/"))); // Must add "Route" parts.
            result.Add(new Node("verb", verb));

            // We need to inspect content of file to retrieve meta information about it, such as authorization, etc.
            using (var stream = File.OpenRead(filename))
            {
                var lambda = new Parser(stream).Lambda();

                // Extracting different existing components from file.
                var auth = ExtractAuth(lambda);
                if (auth != null)
                    result.Add(auth);
                var args = ExtractArgs(lambda);
                if (args != null)
                    result.Add(args);
                var desc = ExtractDescription(lambda);
                if (desc != null)
                    result.Add(desc);

                // Then checking to see if this is a dynamically created CRUD wrapper endpoint.
                var slotNode = lambda.Children.LastOrDefault(x => x.Name == "wait.signal");
                if (slotNode != null && slotNode.Children.Any(x => x.Name == "database") && slotNode.Children.Any(x => x.Name == "table"))
                {
                    // This is a database CRUD HTTP endpoint.
                    HandleCrudEndpoint(verb, result, args, slotNode);
                }
                else
                {
                    // Checking if this is a Custom SQL type of endpoint.
                    var sqlConnectNode = lambda.Children.LastOrDefault(x => x.Name == "wait.mysql.connect" || x.Name == "wait.mssql.connect");
                    if (sqlConnectNode != null)
                    {
                        HandleStatisticsEndpoint(result, lambda, sqlConnectNode);
                    }
                }
            }

            // Returning results to caller.
            return result;
        }

        /*
         * Extracts authorization for executing Hyperlambda file.
         */
        static Node ExtractAuth(Node lambda)
        {
            Node result = new Node("auth");
            foreach (var idx in lambda.Children)
            {
                if (idx.Name == "auth.ticket.verify")
                {
                    result.AddRange(
                        idx.GetEx<string>()?
                        .Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => new Node("", x)) ?? Array.Empty<Node>());
                }
            }
            return result.Children.Any() ? result : null;
        }

        /*
         * Extracts arguments, if existing.
         */
        static Node ExtractArgs(Node lambda)
        {
            var result = new Node("input");
            var args = lambda.Children.FirstOrDefault(x => x.Name == ".arguments");
            if (args != null)
                result.AddRange(args.Children.Select(x => x.Clone()));
            return result.Children.Any() ? result : null;
        }

        /*
         * Extracts description, if existing.
         */
        static Node ExtractDescription(Node lambda)
        {
            var result = lambda.Children.FirstOrDefault(x => x.Name == ".description");
            if (result != null)
                return new Node("description", result.Get<string>());
            return null;
        }

        /*
         * Handles a statistic endpoint.
         */
        private static void HandleStatisticsEndpoint(Node result, Node lambda, Node sqlConnectNode)
        {
            // Checking if this has a x.select type of node of some sort.
            var sqlSelectNode = sqlConnectNode.Children.LastOrDefault(x => x.Name.EndsWith(".select"));
            if (sqlSelectNode != null)
            {
                // Checking if this is a statistics type of endpoint.
                if (lambda.Children.FirstOrDefault(x => x.Name == ".is-statistics")?.Get<bool>() ?? false)
                {
                    // This is a Custom SQL type of endpoint of some sort.
                    result.Add(new Node("type", "crud-statistics"));
                }
                else
                {
                    // This is a Custom SQL type of endpoint of some sort.
                    result.Add(new Node("type", "crud-sql"));
                }
            }
        }

        /*
         * Handles a CRUD HTTP endpoint.
         */
        static void HandleCrudEndpoint(string verb, Node result, Node args, Node slotNode)
        {
            switch (verb)
            {
                case "get":
                    if (slotNode.Children.Any(x => x.Name == "columns"))
                    {
                        var resultNode = new Node("returns");
                        if (slotNode.Children.First(x => x.Name == "columns").Children.Any(x => x.Name == "count(*) as count"))
                        {
                            resultNode.Add(new Node("count", "long"));
                            result.Add(resultNode);
                            result.Add(new Node("array", false));
                            result.Add(new Node("type", "crud-count"));
                        }
                        else
                        {
                            resultNode.AddRange(slotNode.Children.First(x => x.Name == "columns").Children.Select(x => x.Clone()));
                            if (args != null)
                            {
                                foreach (var idx in resultNode.Children)
                                {
                                    // Doing lookup for [.arguments][xxx.eq] to figure out type of object.
                                    idx.Value = args.Children.FirstOrDefault(x => x.Name == idx.Name + ".eq")?.Value;
                                }
                            }
                            result.Add(resultNode);
                            result.Add(new Node("array", true));
                            result.Add(new Node("type", "crud-read"));
                        }
                    }
                    break;

                case "post":
                    result.Add(new Node("type", "crud-create"));
                    break;

                case "put":
                    result.Add(new Node("type", "crud-update"));
                    break;

                case "delete":
                    result.Add(new Node("type", "crud-delete"));
                    break;
            }
        }

        #endregion
    }
}
