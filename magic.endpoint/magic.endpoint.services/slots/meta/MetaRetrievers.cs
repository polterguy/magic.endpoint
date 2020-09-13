/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2020, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

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
        internal static IEnumerable<Node> CrudEndpointNotGet(
            Node lambda,
            string verb,
            Node arguments)
        {
            var slotNode = lambda
                .Children
                .LastOrDefault(x => x.Name == "wait.signal");

            if (slotNode != null &&
                slotNode.Children
                    .Any(x => x.Name == "database") &&
                slotNode.Children
                    .Any(x => x.Name == "table"))
            {
                switch (verb)
                {
                    case "post":
                        yield return new Node("type", "crud-create");
                        break;

                    case "put":
                        yield return new Node("type", "crud-update");
                        break;

                    case "delete":
                        yield return new Node("type", "crud-delete");
                        break;
                }
            }
        }

        /*
         * Returns meta data for a CRUD type of endpoint, if the
         * endpoint is a CRUD endpoint, and its type is GET.
         */
        internal static IEnumerable<Node> CrudEndpointGet(
            Node lambda,
            string verb,
            Node arguments)
        {
            var slotNode = lambda
                .Children
                .LastOrDefault(x => x.Name == "wait.signal");

            if (slotNode != null &&
                slotNode.Children
                    .Any(x => x.Name == "database") &&
                slotNode.Children
                    .Any(x => x.Name == "table"))
            {
                if (verb == "get" && slotNode.Children.Any(x => x.Name == "columns"))
                {
                    var resultNode = new Node("returns");
                    if (slotNode.Children
                        .First(x => x.Name == "columns")
                        .Children.Any(x => x.Name == "count(*) as count"))
                    {
                        resultNode.Add(new Node("count", "long"));
                        yield return resultNode;
                        yield return new Node("array", false);
                        yield return new Node("type", "crud-count");
                    }
                    else
                    {
                        resultNode.AddRange(
                            slotNode
                                .Children
                                .First(x => x.Name == "columns")
                                .Children
                                .Select(x => x.Clone()));
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
            var sqlSelectNode = lambda
                .Children
                .FirstOrDefault(x => x.Name.EndsWith(".connect"))?
                .Children
                .LastOrDefault(x => x.Name.EndsWith(".select"));

            if (sqlSelectNode != null)
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
    }
}
