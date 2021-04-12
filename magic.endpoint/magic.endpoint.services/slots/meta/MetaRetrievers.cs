/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2021, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System.Linq;
using System.Collections.Generic;
using magic.node;
using magic.node.extensions;

namespace magic.endpoint.services.slots.meta
{
    /*
     * Internal data retriever helper class helping you to retrieve
     * meta information associated with yout Hyperlambda endpoint, such
     * as input arguments, Content-Type, Accept value, type of CRUD endpoint, 
     * description, etc.
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
        internal static IEnumerable<Node> CrudEndpointMetaGet(
            Node lambda,
            string verb,
            Node arguments)
        {
            if (verb == "get")
            {
                var crudType = GetCrudEndpointType(lambda);
                switch (crudType)
                {
                    case "crud-count":

                        // Count(*) type of endpoint.
                        foreach (var idx in GetCountEndpointMeta())
                        {
                            yield return idx;
                        }
                        break;

                    case "crud-read":

                        // CRUD read type of endpoint.
                        foreach (var idx in GetReadEndpointMeta(lambda, arguments))
                        {
                            yield return idx;
                        }
                        break;
                }
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
            var x = new Expression("**/response.headers.add/*/Content-Type");
            var result = x.Evaluate(lambda);

            /*
             * If there are no Content-Type declarations in endpoint, it will default
             * to application/json
             */
            if (!result.Any())
                yield return new Node("produces", "application/json");

            /*
             * If there are multiple nodes, no Content-Type can positively be deducted,
             * since it might be a result of branching.
             */
            if (result.Count() == 1)
                yield return new Node("produces", result.First().GetEx<string>());
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
                yield return new Node("consumes", "application/json");

            /*
             * If there are multiple nodes, no Content-Type can positively be deducted,
             * since it might be a result of branching.
             */
            if (result.Count() == 1)
                yield return new Node("consumes", result.First().GetEx<string>());
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

        /*
         * Helper method to retrieve meta information for count(*) type of endpoints.
         */
        static IEnumerable<Node> GetCountEndpointMeta()
        {
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

        /*
         * Helper method to retrieve meta information for count(*) type of endpoints.
         */
        static IEnumerable<Node> GetReadEndpointMeta(Node lambda, Node arguments)
        {
            var resultNode = new Node("output");
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

        #endregion
    }
}
