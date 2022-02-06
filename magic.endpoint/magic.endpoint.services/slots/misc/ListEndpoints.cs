/*
 * Magic Cloud, copyright Aista, Ltd. See the attached LICENSE file for details.
 */

using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using magic.node;
using magic.node.contracts;
using magic.node.extensions;
using magic.signals.contracts;
using magic.endpoint.services.utilities;
using magic.node.extensions.hyperlambda;
using magic.endpoint.services.slots.meta;

namespace magic.endpoint.services.slots.misc
{
    /// <summary>
    /// [endpoints.list] slot for returning all dynamica Hyperlambda endpoints
    /// for your application, in addition to their meta information.
    /// </summary>
    [Slot(Name = "endpoints.list")]
    public class ListEndpoints : ISlot, ISlotAsync
    {
        /*
         * Resolvers for meta data.
         *
         * These are the default meta retrievers, but can easily be extended by creating your 
         * own meta resolvers using the static `AddMetaDataResolver` method.
         */
        static readonly List<Func<Node, string, Node, IEnumerable<Node>>> _endpointMetaRetrievers =
            new List<Func<Node, string, Node, IEnumerable<Node>>>
        {
            (lambda, verb, args) => MetaRetrievers.EndpointType(lambda, verb, args),
            (lambda, verb, args) => MetaRetrievers.CrudEndpointMetaGet(lambda, verb, args),
            (lambda, verb, args) => MetaRetrievers.ContentType(lambda, verb, args),
            (lambda, verb, args) => MetaRetrievers.Accepts(lambda, verb, args),
        };

        readonly IRootResolver _rootResolver;
        readonly IFolderService _folderService;
        readonly IFileService _fileService;
        List<string> _folders;
        IEnumerable<(string Filename, string Content)> _content;

        /// <summary>
        /// Creates an instance of your object
        /// </summary>
        /// <param name="rootResolver">Needed to resolve root folder path for files and folders.</param>
        /// <param name="folderService">Needed to resolver folders in system.</param>
        /// <param name="fileService">Needed to resolve files in system.</param>
        public ListEndpoints(
            IRootResolver rootResolver,
            IFolderService folderService,
            IFileService fileService)
        {
            _rootResolver = rootResolver;
            _folderService = folderService;
            _fileService = fileService;
        }

        /// <summary>
        /// Implementation of your slot.
        /// </summary>
        /// <param name="signaler">Signaler that invoked your slot.</param>
        /// <param name="input">Arguments to your slot.</param>
        /// <returns>Awaitable task</returns>
        public async Task SignalAsync(ISignaler signaler, Node input)
        {
            _folders = await _folderService.ListFoldersRecursivelyAsync(_rootResolver.AbsolutePath("/"));
            _content = (await _fileService
                .LoadRecursivelyAsync(_rootResolver.AbsolutePath("/"), ".hl"))
                .Select(x => (x.Filename, Encoding.UTF8.GetString(x.Content)));

            input.AddRange(
                HandleFiles(
                    _rootResolver.DynamicFiles,
                    _rootResolver.AbsolutePath("system/")));
            input.AddRange(
                HandleFiles(
                    _rootResolver.DynamicFiles,
                    _rootResolver.AbsolutePath("modules/")));
        }

        /// <summary>
        /// Implementation of your slot.
        /// </summary>
        /// <param name="signaler">Signaler that invoked your slot.</param>
        /// <param name="input">Arguments to your slot.</param>
        /// <returns>Awaitable task</returns>
        public void Signal(ISignaler signaler, Node input)
        {
            _folders = _folderService.ListFoldersRecursively(_rootResolver.AbsolutePath("/"));
            _content = (_fileService
                .LoadRecursively(_rootResolver.AbsolutePath("/"), ".hl"))
                .Select(x => (x.Filename, Encoding.UTF8.GetString(x.Content)));

            input.AddRange(
                HandleFiles(
                    _rootResolver.DynamicFiles,
                    _rootResolver.AbsolutePath("system/")));
            input.AddRange(
                HandleFiles(
                    _rootResolver.DynamicFiles,
                    _rootResolver.AbsolutePath("modules/")));
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
         * Returns all fildes from current folder that matches some HTTP verb.
         */
        List<Node> HandleFiles(string rootFolder, string folder)
        {
            // Buffer to hold result.
            var result = new List<Node>();

            // Looping through each file in current folder.
            var files = _content.Where(x => x.Filename.StartsWith(folder));
            foreach (var idxFile in files)
            {
                // Removing the root folder, to return only relativ filename back to caller.
                var filename = idxFile.Filename.Substring(rootFolder.Length);

                // Making sure we only return files with format of "foo.xxx.hl", where xxx is some valid HTTP verb.
                var entities = filename.Split('.');
                if (entities.Length == 3)
                {
                    // Returning a Node representing the currently iterated file.
                    switch (entities[1])
                    {
                        case "delete":
                        case "put":
                        case "patch":
                        case "post":
                        case "get":
                        case "socket":
                            result.Add(GetFileMetaData(entities[0], entities[1], idxFile.Filename));
                            break;
                    }
                }
            }

            // Returning result to caller.
            return result;
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
            result.Add(new Node("path", "magic/" + path)); // Must add "Route" parts.
            result.Add(new Node("verb", verb));

            // Ensuring we don't crash the whole meta retrieval bugger if there is an error in one of our endpoints.
            try
            {
                /*
                 * We need to inspect content of file to retrieve meta information about it,
                 * such as authorization, description, etc.
                 */
                var lambda = HyperlambdaParser.Parse(_content.FirstOrDefault(x => x.Filename == filename).Content);

                // Extracting different existing components from file.
                var args = GetInputArguments(lambda, verb);
                result.AddRange(new Node[] {
                    args,
                    GetAuthorization(lambda),
                    GetDescription(lambda),
                }.Where(x => x!= null));

                result.AddRange(GetEndpointCustomInformation(lambda, verb, args));
            }
            catch(Exception error)
            {
                result.Add(new Node("error", error.Message));
            }

            // Returning results to caller.
            return result;
        }

        /*
         * Extracts arguments, if existing.
         */
        static Node GetInputArguments(Node lambda, string verb)
        {
            var result = new Node("input");
            var args = lambda.Children.FirstOrDefault(x => x.Name == ".arguments");
            if (args != null)
            {
                foreach (var idx in args.Children)
                {
                    var node = new Node(".");
                    node.Add(new Node("name", idx.Name));
                    node.Add(new Node("type", idx.Value));
                    if (verb == "post" || verb == "put")
                    {
                        // Attaching foreign key information if possible.
                        var fkNodes = lambda
                            .Children
                            .FirstOrDefault(x => x.Name == ".foreign-keys")?
                            .Children
                            .Where(x => x.Children.Any(x2 => x2.Name == "column" && x2.GetEx<string>() == idx.Name)) ?? Array.Empty<Node>();
                        foreach (var idxFk in fkNodes.Select(x => x.Children))
                        {
                            var fkNode = new Node("lookup");
                            fkNode.Add(new Node("table", idxFk.FirstOrDefault(x => x.Name == "table")?.GetEx<string>()));
                            fkNode.Add(new Node("key", idxFk.FirstOrDefault(x => x.Name == "foreign_column")?.GetEx<string>()));
                            fkNode.Add(new Node("name", idxFk.FirstOrDefault(x => x.Name == "foreign_name")?.GetEx<string>()));
                            fkNode.Add(new Node("long", idxFk.FirstOrDefault(x => x.Name == "long")?.GetEx<bool>()));
                            node.Add(fkNode);
                        }
                    }
                    result.Add(node);
                }
            }
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
                        .Select(x => new Node("", x.Trim())) ?? new Node[] { new Node("", "*") });
                }
            }
            return result.Children.Any() ? result : null;
        }

        /*
         * Extracts description, if existing.
         */
        static Node GetDescription(Node lambda)
        {
            var result = lambda
                .Children
                .FirstOrDefault(x => x.Name == ".description")?
                .Get<string>();

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
