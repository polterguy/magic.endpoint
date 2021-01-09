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
                // count(*) type of endpoint.
                yield return new Node(
                    "output",
                    null,
                    new Node[]
                    {
                        new Node(".", null, new Node[]
                        {
                            new Node("name", "count"),
                            new Node("type", "long")
                        })
                    });
                yield return new Node("array", false);
            }
            else if (crudType == "crud-read" && verb == "get")
            {
                // CRUD read type of endpoint.
                var resultNode = new Node("returns");
                var enumerator = lambda.Children
                    .FirstOrDefault(x => x.Name.EndsWith(".connect"))?.Children
                    .FirstOrDefault(x => x.Name.EndsWith(".read"))?.Children
                    .FirstOrDefault(x => x.Name == "columns")?.Children;
                if (enumerator != null)
                {
                    foreach (var idx in enumerator)
                    {
                        var node = new Node(".");
                        node.Add(new Node("name", idx.Name));
                        if (arguments != null)
                        {
                            foreach (var idxType in arguments.Children)
                            {
                                foreach (var idxInnerType in idxType.Children)
                                {
                                    if (idxInnerType.Name == "name" && idxInnerType.Get<string>() == idx.Name + ".eq")
                                        node.Add(new Node("type", idxType.Children.FirstOrDefault(x => x.Name == "type")?.Value));
                                }
                            }
                        }
                        resultNode.Add(node);
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
                yield return new Node("output_type", "application/json");

            /*
             * If there are multiple nodes, no Content-Type can positively be deducted,
             * since it might be a result of branching.
             */
            if (result.Count() == 1)
                yield return new Node("output_type", result.First().GetEx<string>());
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
            // GET and DELETE endpoints don't have any type of input, since they have no payload.
            if (verb == "get" || verb == "delete")
                yield break;

            // Finding [.accept] meta node, if existing.
            var x = new Expression("*/.accept");
            var result = x.Evaluate(lambda);

            /*
             * If there are no Content-Type declarations in endpoint, it will default
             * to application/json
             */
            if (!result.Any())
                yield return new Node("input_type", "application/json");

            /*
             * If there are multiple nodes, no Content-Type can positively be deducted,
             * since it might be a result of branching.
             */
            if (result.Count() == 1)
                yield return new Node("input_type", result.First().GetEx<string>());
        }

        #region [ -- Private helper methods -- ]

        /*
         * Helper method to retrieve return node for CRUD endpoints.
         */
        static string GetCrudEndpointType(Node lambda)
        {
            return lambda
                .Children
                .FirstOrDefault(x => x.Name == ".type" && x.Get<string>().StartsWith("crud-"))?.Get<string>() ?? "custom";
        }

        #endregion
    }
}
