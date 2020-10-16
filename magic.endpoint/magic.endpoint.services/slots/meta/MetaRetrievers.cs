/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2020, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Linq;
using System.Collections.Generic;
using magic.node;
using magic.node.extensions;

namespace magic.endpoint.services.slots.meta
{
    /*
     * Built in meta data retrievers.
     */
    internal static class MetaRetrievers
    {
        /*
         * Returns meta data for a CRUD type of endpoint, if the
         * endpoint is a CRUD endpoint, and not of type GET.
         */
        internal static IEnumerable<Node> EndpointType(
            Node lambda,
            string verb,
            Node arguments)
        {
            var crudType = GetCrudEndpointType(lambda);
            yield return new Node("type", crudType);
        }

        /*
         * Returns meta data for a CRUD type of endpoint, if the
         * endpoint is a CRUD endpoint, and its type is GET.
         *
         * This is specially handled, to accommodate for "count" type
         * of CRUD endpoints.
         */
        internal static IEnumerable<Node> CrudEndpointGet(
            Node lambda,
            string verb,
            Node arguments)
        {
            var crudType = GetCrudEndpointType(lambda);
            if (crudType == "crud-count" && verb == "get")
            {
                // count(*) endpoint.
                yield return new Node("returns", null, new Node[] { new Node("count", "long") });
                yield return new Node("array", false);
            }
            else if (crudType == "crud-read" && verb == "get")
            {
                // Read endpoint.
                var resultNode = new Node("returns");
                resultNode.AddRange(
                    lambda
                        .Children
                        .FirstOrDefault(x => x.Name.EndsWith(".connect"))?
                        .Children
                        .FirstOrDefault(x => x.Name.EndsWith(".read"))?
                        .Children
                        .FirstOrDefault(x => x.Name == "columns")?
                        .Children
                        .Select(x => x.Clone()) ?? Array.Empty<Node>());
                if (arguments != null)
                {
                    foreach (var idx in resultNode.Children)
                    {
                        // Doing lookup for [.arguments][xxx.eq] to figure out type of object.
                        idx.Value = arguments.Children.FirstOrDefault(x => x.Name == idx.Name + ".eq")?.Value;
                    }
                }
                yield return resultNode;
                yield return new Node("array", true);
            }
        }

        /*
         * Returns what Content-Type endpoint produces, if this is
         * possible to retrieve.
         */
        internal static IEnumerable<Node> ContentType(
            Node lambda,
            string verb,
            Node arguments)
        {
            var x = new Expression("*/response.headers.add/*/Content-Type");
            var result = x.Evaluate(lambda);

            /*
             * If there are no Content-Type declarations in endpoint, it will default
             * to application/json
             */
            if (!result.Any())
                yield return new Node("Content-Type", "application/json");

            /*
             * If there are multiple nodes, no Content-Type can positively be deducted,
             * since it might be a result of branching.
             */
            if (result.Count() == 1)
                yield return new Node("Content-Type", result.First().GetEx<string>());
        }

        /*
         * Returns what Content-Type endpoint consumes, if this is
         * possible to retrieve.
         */
        internal static IEnumerable<Node> Accepts(
            Node lambda,
            string verb,
            Node arguments)
        {
            var x = new Expression("*/.accept");
            var result = x.Evaluate(lambda);

            /*
             * If there are no Content-Type declarations in endpoint, it will default
             * to application/json
             */
            if (!result.Any())
                yield return new Node("Accept", "application/json");

            /*
             * If there are multiple nodes, no Content-Type can positively be deducted,
             * since it might be a result of branching.
             */
            if (result.Count() == 1)
                yield return new Node("Accept", result.First().GetEx<string>());
        }

        #region [ -- Private helper methods -- ]

        /*
         * Helper method to retrieve return node for CRUD endpoints.
         */
        static string GetCrudEndpointType(Node lambda)
        {
            return lambda
                .Children
                .FirstOrDefault(x => x.Name == ".type" && x.Get<string>().StartsWith("crud-"))?.Get<string>();
        }

        #endregion
    }
}
