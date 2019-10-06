/*
 * Magic, Copyright(c) Thomas Hansen 2019 - thomas@gaiasoul.com
 * Licensed as Affero GPL unless an explicitly proprietary license has been obtained.
 */

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Swashbuckle.AspNetCore.Swagger;
using magic.node;
using magic.signals.contracts;
using magic.endpoint.services.utilities;

namespace magic.endpoint.services
{
    /// <summary>
    /// [.swagger-dox.generic] slot for creating Swagger documentation from
    /// custom Hyperlambda endpoints.
    /// </summary>
    [Slot(Name = ".swagger-dox.generic")]
    public class SwaggerDox : ISlot
    {
        readonly IConfiguration _configuration;

        /// <summary>
        /// Creates an instance of your type.
        /// </summary>
        /// <param name="configuration">Configuration of your application.</param>
        public SwaggerDox(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// Implementation of slot.
        /// </summary>
        /// <param name="signaler">Signaler used to raise signal.</param>
        /// <param name="input">Arguments to slot.</param>
        public void Signal(ISignaler signaler, Node input)
        {
            var doc = input.Value as SwaggerDocument;
            var toRemove = new List<string>(doc.Paths.Keys.Where(x => x == "/api/hl/{url}"));
            foreach (var idx in toRemove)
            {
                doc.Paths.Remove(idx);
            }
            var rootFolder = Utilities.GetRootFolder(_configuration);
            AddCustomEndpoints(doc, rootFolder,rootFolder);
        }

        #region [ -- Private helper methods -- ]

        /*
         * Recusrively adds all dynamic Hyperlambda endpoints by traversing
         * the root folder on disc, to look for Hyperlambda endpoints.
         */
        void AddCustomEndpoints(
            SwaggerDocument doc,
            string rootFolder,
            string currentFolder)
        {
            foreach (var idx in Directory.GetDirectories(currentFolder))
            {
                var folder = "/" + idx.Substring(rootFolder.Length);
                if (Utilities.IsLegalHttpName(folder))
                    AddAllVerbs(doc, rootFolder, idx);
            }
        }

        /*
         * Adds all HTTP verbs in the specified folder.
         */
        void AddAllVerbs(
            SwaggerDocument doc,
            string rootFolder,
            string folder)
        {
            foreach (var idxFile in Directory.GetFiles(folder, "*.hl"))
            {
                var filename = "/" + idxFile.Substring(rootFolder.Length).Replace("\\", "/");
                if (Utilities.IsLegalHttpName(filename.Substring(0, filename.IndexOf(".", StringComparison.InvariantCulture))))
                {
                    var fileInfo = new FileInfo(filename);
                    var splits = fileInfo.Name.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
                    if (splits.Length == 3)
                    {
                        switch (splits[1])
                        {
                            case "get":
                            case "put":
                            case "post":
                            case "delete":
                                AddVerb(doc, splits[1], filename);
                                break;
                        }
                    }
                }
            }
        }

        /*
         * Adds one single verb for the specified filename to the Swagger doc.
         */
        private void AddVerb(
            SwaggerDocument doc, 
            string verb, 
            string filename)
        {
            // Figuring out which key to use, and making sure we put an item into dictionary for URL.
            var itemType = filename.Substring(0, filename.IndexOf(".", StringComparison.InvariantCulture)); ;
            var key = "/api/hl" + itemType;
            if (!doc.Paths.ContainsKey(key))
            {
                var p = new PathItem();
                doc.Paths[key] = p;
            }

            // Retrieving existing item from path.
            var item = doc.Paths[key];

            // Creating our operation item.
            var tag = filename.Substring(0, filename.IndexOf(".", StringComparison.InvariantCulture)).Trim('/');
            var operation = new Operation
            {
                Tags = new List<string> { tag },
            };

            // Figuring out the type of operation this is.
            switch (verb)
            {
                case "get":
                    operation.Produces = new List<string> { "application/json" };
                    operation.Description = $"Returns '{itemType}' from the server";
                    item.Get = operation;
                    break;

                case "delete":
                    operation.Produces = new List<string> { "application/json" };
                    operation.Description = $"Deletes '{itemType}' from the server";
                    item.Delete = operation;
                    break;

                case "put":
                    operation.Consumes = new List<string> { "application/json" };
                    operation.Produces = new List<string> { "application/json" };
                    operation.Description = $"Updates an existing '{itemType}' on the server";
                    item.Put = operation;
                    break;

                case "post":
                    operation.Consumes = new List<string> { "application/json" };
                    operation.Produces = new List<string> { "application/json" };
                    operation.Description = $"Creates a new '{itemType}' on the server";
                    item.Post = operation;
                    break;
            }
        }

        #endregion
    }
}
