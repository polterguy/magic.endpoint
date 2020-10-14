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
using magic.endpoint.services.slots.meta;

namespace magic.endpoint.services.slots
{
    /// <summary>
    /// [system.endpoints] slot for returning all dynamica Hyperlambda endpoints
    /// for your application.
    /// </summary>
    [Slot(Name = "endpoints.list")]
    public class ListEndpoints : ISlot
    {
        /*
         * Resolvers for meta data.
         */
        static readonly List<Func<Node, string, Node, IEnumerable<Node>>> _endpointMetaRetrievers =
            new List<Func<Node, string, Node, IEnumerable<Node>>>
        {
            (lambda, verb, args) => MetaRetrievers.EndpointType(lambda, verb, args),
            (lambda, verb, args) => MetaRetrievers.CrudEndpointGet(lambda, verb, args),
            (lambda, verb, args) => MetaRetrievers.ContentType(lambda, verb, args),
        };

        /// <summary>
        /// Implementation of your slot.
        /// </summary>
        /// <param name="signaler">Signaler that invoked your slot.</param>
        /// <param name="input">Arguments to your slot.</param>
        public void Signal(ISignaler signaler, Node input)
        {
            input.AddRange(HandleFolder(
                Utilities.RootFolder,
                Utilities.RootFolder + "modules/").ToList());
        }

        /// <summary>
        /// Adds a meta data resolver, allowing you to inject your own meta data,
        /// depending upon the lambda structure of the content of the file reolved
        /// in your endpoint.
        /// 
        /// Your function will be invoked with the entire lambda for your file, its verb,
        /// and its arguments. The latter will be found from your file's [.arguments] node.
        /// </summary>
        /// <param name="functor">Function responsible for returning additional meta data for endpoint.</param>
        public void AddMetaDataResolver(Func<Node, string, Node, IEnumerable<Node>> functor)
        {
            _endpointMetaRetrievers.Add(functor);
        }

        #region [ -- Private helper methods -- ]

        /*
         * Recursively traverses your folder for any dynamic Hyperlambda
         * endpoints, and returns the result to caller.
         */
        IEnumerable<Node> HandleFolder(string rootFolder, string currentFolder)
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
                    foreach (var idxFile in HandleFiles(rootFolder, idxFolder))
                    {
                        yield return idxFile;
                    }

                    // Recursively retrieving inner folders of currently iterated folder.
                    foreach (var idx in HandleFolder(rootFolder, idxFolder))
                    {
                        yield return idx;
                    }
                }
            }
        }

        /*
         * Returns all fildes from current folder that matches some HTTP verb.
         */
        IEnumerable<Node> HandleFiles(string rootFolder, string folder)
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
                            yield return GetFileMetaData(entities[0], entities[1], idxFile);
                            break;
                    }
                }
            }
        }

        /*
         * Returns a single node, representing the endpoint given
         * as verb/filename/path, and its associated meta information.
         */
        Node GetFileMetaData(
            string path,
            string verb,
            string filename)
        {
            // Creating our result node, and making sure we return path and verb.
            var result = new Node("");
            result.Add(new Node("path", "magic/" + path.Replace("\\", "/"))); // Must add "Route" parts.
            result.Add(new Node("verb", verb));

            /*
             * We need to inspect content of file to retrieve meta information about it,
              such as authorization, description, etc.
             */
            using (var stream = File.OpenRead(filename))
            {
                var lambda = new Parser(stream).Lambda();

                // Extracting different existing components from file.
                var args = GetInputArguments(lambda);
                result.AddRange(new Node[] {
                    args,
                    GetAuthorization(lambda),
                    GetDescription(lambda),
                }.Where(x => x!= null));
                result.AddRange(GetEndpointCustomInformation(lambda, verb, args));
            }

            // Returning results to caller.
            return result;
        }

        /*
         * Extracts arguments, if existing.
         */
        static Node GetInputArguments(Node lambda)
        {
            var result = new Node("input");
            var args = lambda.Children.FirstOrDefault(x => x.Name == ".arguments");
            if (args != null)
                result.AddRange(args.Children.Select(x => x.Clone()));
            return result.Children.Any() ? result : null;
        }

        /*
         * Extracts authorization for executing Hyperlambda file.
         */
        static Node GetAuthorization(Node lambda)
        {
            Node result = new Node("auth");
            foreach (var idx in lambda.Children)
            {
                if (idx.Name == "auth.ticket.verify")
                {
                    result.AddRange(
                        idx.GetEx<string>()?
                        .Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => new Node("", x.Trim())) ?? Array.Empty<Node>());
                }
            }
            return result.Children.Any() ? result : null;
        }

        /*
         * Extracts description, if existing.
         */
        static Node GetDescription(Node lambda)
        {
            var result = lambda.Children.FirstOrDefault(x => x.Name == ".description")?.Get<string>();
            if (!string.IsNullOrEmpty(result))
                return new Node("description", result);
            return null;
        }

        /*
         * Extracts custom information from endpoint,
         * which depends upon what type of endpoint this is.
         */
        static IEnumerable<Node> GetEndpointCustomInformation(
            Node lambda,
            string verb,
            Node args)
        {
            foreach (var idxResult in _endpointMetaRetrievers.SelectMany(x => x(lambda, verb, args)))
            {
                yield return idxResult;
            }
        }

        #endregion
    }
}
