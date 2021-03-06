﻿/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2021, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using magic.node;
using magic.node.extensions;
using magic.signals.contracts;
using magic.endpoint.contracts;
using magic.endpoint.services.utilities;
using magic.node.extensions.hyperlambda;

namespace magic.endpoint.services
{
    /// <summary>
    /// Implementation of IArgumentsHandler service contract, responsible for
    /// attaching arguments originating from client to lambda object being executed.
    /// </summary>
    public class ArgumentsHandler : IArgumentsHandler
    {
        /// <inheritdoc />
        public void Attach(
            Node lambda, 
            IEnumerable<(string Name, string Value)> query, 
            Node payload)
        {
            // Finding lambda object's [.arguments] declaration if existing, and making sure we remove it from lambda object.
            var declaration = lambda.Children.FirstOrDefault(x => x.Name == ".arguments");
            declaration?.UnTie();

            // [.arguments] not to insert into lambda if we have any arguments.
            var args = new Node(".arguments");

            // Checking if query parameters was supplied, and if so, attach them as arguments.
            if (query != null)
                args.AddRange(GetQueryParameters(declaration, query));

            // Checking if payload was supplied, and if so, attaching it as arguments.
            if (payload != null)
                args.AddRange(GetPayloadParameters(declaration, payload));

            // Only inserting [.arguments] node if there are any arguments.
            if (args.Children.Any())
                lambda.Insert(0, args);
        }

        #region [ -- Private helper methods -- ]

        /*
         * Converts if necessary, and attaches arguments found in
         * query parameters to args node, sanity checking that the
         * query parameter is allowed in the process.
         */
        IEnumerable<Node> GetQueryParameters(
            Node declaration,
            IEnumerable<(string Name, string Value)> queryParameters)
        {
            foreach (var idxArg in queryParameters)
            {
                // Retrieving string representation of argument.
                object value = idxArg.Value;

                /*
                 * Checking if file contains a declaration at all.
                 * This is done since by default all endpoints accepts all arguments,
                 * unless an explicit [.arguments] declaration node is declared in the file.
                 */
                if (declaration != null)
                {
                    var declarationType = declaration?
                        .Children
                        .FirstOrDefault(x => x.Name == idxArg.Name)?
                        .Get<string>() ??
                        throw new ArgumentException($"I don't know how to handle the '{idxArg.Name}' query parameter");

                    // Converting argument to expected type.
                    value = Converter.ToObject(idxArg.Value, declarationType);
                }
                yield return new Node(idxArg.Name, value);
            }
        }

        /*
         * Converts if necessary, and attaches arguments found in
         * payload to args node, sanity checking that the
         * parameter is allowed in the process.
         */
        IEnumerable<Node> GetPayloadParameters(Node declaration, Node payload)
        {
            /*
             * Checking if file contains a declaration at all.
             * This is done since by default all endpoints accepts all arguments,
             * unless an explicit [.arguments] declaration node is found.
             */
            if (declaration != null)
            {
                foreach (var idxArg in payload.Children)
                {
                    ConvertArgumentRecursively(
                        idxArg,
                        declaration.Children.FirstOrDefault(x => x.Name == idxArg.Name));
                }
            }
            return payload.Children.ToList();
        }

        /*
         * Converts the given input argument to the type specified in the
         * declaration node. Making sure the argument is allowed for the
         * endpoint.
         */
        void ConvertArgumentRecursively(Node arg, Node declaration)
        {
            // If declaration node is null here, it means endpoint has no means to handle the argument.
            if (declaration == null)
                throw new ArgumentException($"I don't know how to handle the '{arg.Name}' argument");

            var type = declaration.Get<string>();
            if (type == "*")
                return; // Turning OFF all argument sanity checking and conversion recursively below this node.

            // Making sure type declaration for argument exists.
            if (type != null && arg.Value != null)
                arg.Value = Converter.ToObject(arg.Value, type); // Converting argument, which might throw an exception if conversion is not possible

            // Recursively running through children.
            foreach (var idxChild in arg.Children)
            {
                ConvertArgumentRecursively(idxChild, declaration.Children.FirstOrDefault(x => x.Name == idxChild.Name));
            }
        }

        #endregion
    }
}
