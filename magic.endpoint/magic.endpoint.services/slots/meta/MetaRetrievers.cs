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
        internal static IEnumerable<Node> CrudEndpoint(
            Node lambda,
            string verb,
            Node arguments)
        {
            var crudType = GetCrudEndpointType(lambda);
            if (!string.IsNullOrEmpty(crudType) && verb != "get")
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
                yield return new Node("type", "crud-count");
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
                yield return new Node("type", "crud-read");
            }
        }

        /*
         * Returns meta data for an SQL select type of endpoint,
         * possibly attaching statistics meta information, if existing.
         */
        internal static IEnumerable<Node> StatisticsEndpoint(
            Node lambda,
            string verb,
            Node arguments)
        {
            // Checking if this has a x.select type of node of some sort.
            var crudType = lambda
                .Children
                .FirstOrDefault(x => x.Name == ".type")?.Get<string>();

            if (crudType == "sql")
            {
                // Checking if this is a statistics type of endpoint.
                if (lambda.Children
                    .FirstOrDefault(x => x.Name == ".is-statistics")?
                    .Get<bool>() ?? false)
                    yield return new Node("type", "crud-statistics");
                else
                    yield return new Node("type", "crud-sql");
            }
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
