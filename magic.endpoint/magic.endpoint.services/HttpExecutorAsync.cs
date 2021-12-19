/*
 * Magic Cloud, copyright Aista, Ltd. See the attached LICENSE file for details.
 */

using System;
using System.Linq;
using System.Threading.Tasks;
using magic.node;
using magic.node.contracts;
using magic.node.extensions;
using magic.signals.contracts;
using magic.endpoint.contracts;
using magic.endpoint.contracts.poco;
using magic.endpoint.services.utilities;
using magic.node.extensions.hyperlambda;

namespace magic.endpoint.services
{
    /// <summary>
    /// Implementation of IExecutor service contract, allowing you to
    /// execute a dynamically created Hyperlambda endpoint.
    /// </summary>
    public class HttpExecutorAsync : IHttpExecutorAsync
    {
        readonly ISignaler _signaler;
        readonly IFileService _fileService;
        readonly IRootResolver _rootResolver;
        readonly IHttpArgumentsHandler _argumentsHandler;

        /// <summary>
        /// Creates an instance of your type.
        /// </summary>
        /// <param name="signaler">Signaler necessary to execute endpoint.</param>
        /// <param name="fileService">Needed to resolve endpoint files.</param>
        /// <param name="rootResolver">Needed to resolve root folder names.</param>
        /// <param name="argumentsHandler">Needed to attach arguments to endpoint invocation.</param>
        public HttpExecutorAsync(
            ISignaler signaler,
            IFileService fileService,
            IRootResolver rootResolver,
            IHttpArgumentsHandler argumentsHandler)
        {
            _signaler = signaler;
            _fileService = fileService;
            _rootResolver = rootResolver;
            _argumentsHandler = argumentsHandler;
        }

        /// <inheritdoc/>
        public async Task<MagicResponse> ExecuteAsync(MagicRequest request)
        {
            // Sanity checking invocation
            if (string.IsNullOrEmpty(request.URL))
                return new MagicResponse { Result = 404 };

            // Making sure we never resolve to anything outside of "modules/" and "system/" folder.
            if (!request.URL.StartsWith("modules/") && !request.URL.StartsWith("system/"))
                return new MagicResponse { Result = 401 };

            // Figuring out file to execute, and doing some basic sanity checking.
            var path = Utilities.GetEndpointFilePath(_rootResolver, request.URL, request.Verb);
            if (!_fileService.Exists(path))
                return new MagicResponse { Result = 404 };

            // Creating our lambda object by loading Hyperlambda file.
            var lambda = HyperlambdaParser.Parse(await _fileService.LoadAsync(path));

            // Applying interceptors.
            lambda = await ApplyInterceptors(lambda, request.URL);

            // Attaching arguments.
            _argumentsHandler.Attach(lambda, request.Query, request.Payload);

            // Invoking method responsible for actually executing lambda object.
            return await ExecuteAsync(lambda, request);
        }

        #region [ -- Private helper methods -- ]

        /*
         * Applies interceptors to specified Node/Lambda object.
         */
        async Task<Node> ApplyInterceptors(Node result, string url)
        {
            // Checking to see if interceptors exists recursively upwards in folder hierarchy.
            var splits = url.Split(new char [] {'/'}, StringSplitOptions.RemoveEmptyEntries);

            // Stripping away last entity (filename) of invocation.
            var folders = splits.Take(splits.Length - 1);

            // Iterating as long as we have more entities in list of folders.
            while (true)
            {
                // Checking if "current-folder/interceptor.hl" file exists.
                var current = _rootResolver.AbsolutePath(string.Join("/", folders) + "/interceptor.hl");
                if (_fileService.Exists(current))
                    result = await ApplyInterceptor(result, current);

                // Checking if we're done, and at root folder, at which point we break while loop.
                if (!folders.Any())
                    break; // We're done, no more interceptors!

                // Traversing upwards in hierarchy to be able to nest interceptors upwards in hierarchy.
                folders = folders.Take(folders.Count() - 1);
            }

            // Returning result to caller.
            return result;
        }

        /*
         * Applies the specified interceptor and returns the transformed Node/Lambda result.
         */
        async Task<Node> ApplyInterceptor(Node lambda, string interceptorFile)
        {
            // Getting interceptor lambda.
            var interceptNode = HyperlambdaParser.Parse(await _fileService.LoadAsync(interceptorFile));

            // Moving [.arguments] from endpoint lambda to the top of interceptor lambda if existing.
            var args = lambda
                .Children
                .Where(x =>
                    x.Name == ".arguments" ||
                    x.Name == ".description" ||
                    x.Name == ".type" ||
                    x.Name == "auth.ticket.verify" ||
                    x.Name.StartsWith("validators."));

            // Notice, reversing arguments nodes makes sure we apply arguments in order of appearance.
            foreach (var idx in args.Reverse().ToList())
            {
                interceptNode.Insert(0, idx); // Notice, will detach the argument from its original position!
            }

            // Moving endpoint Lambda to position before any [.interceptor] node found in interceptor lambda.
            foreach (var idxLambda in new Expression("**/.interceptor").Evaluate(interceptNode).ToList())
            {
                // Iterating through each node in current result and injecting before currently iterated [.lambda] node.
                foreach (var idx in lambda.Children)
                {
                    // This logic ensures we keep existing order without any fuzz.
                    // By cloning node we also support having multiple [.interceptor] nodes.
                    idxLambda.InsertBefore(idx.Clone());
                }

                // Removing currently iterated [.interceptor] node in interceptor lambda object.
                idxLambda.Parent.Remove(idxLambda);
            }

            // Returning interceptor Node/Lambda which is now the root of the execution Lambda object.
            return interceptNode;
        }

        /*
         * Method responsible for actually executing lambda object after file has been loaded,
         * interceptors and arguments have been applied, and transforming result of invocation
         * to a MagicResponse.
         */
        async Task<MagicResponse> ExecuteAsync(Node lambda, MagicRequest request)
        {
            // Creating our result wrapper, wrapping whatever the endpoint wants to return to the client.
            var result = new Node();
            var response = new MagicResponse();
            try
            {
                await _signaler.ScopeAsync("http.request", request, async () =>
                {
                    await _signaler.ScopeAsync("http.response", response, async () =>
                    {
                        await _signaler.ScopeAsync("slots.result", result, async () =>
                        {
                            await _signaler.SignalAsync("eval", lambda);
                        });
                    });
                });
                response.Content = GetReturnValue(response, result);
                return response;
            }
            catch
            {
                if (result.Value is IDisposable disposable)
                    disposable.Dispose();
                if (response.Content is IDisposable disposable2 && !object.ReferenceEquals(response.Content, result.Value))
                    disposable2.Dispose();
                throw;
            }
        }

        /*
         * Creates a returned payload of some sort and returning to caller.
         */
        object GetReturnValue(MagicResponse httpResponse, Node lambda)
        {
            /*
             * An endpoint can return either a Node/Lambda hierarchy or a simple value.
             * First we check if endpoint returned a simple value, at which point we convert it to
             * a string. Notice, we're prioritising simple values, implying if return node has a
             * simple value, none of its children nodes will be returned.
             */
            if (lambda.Value != null)
            {
                // IDisposables (Streams e.g.) are automatically disposed by ASP.NET Core.
                if (lambda.Value is IDisposable || lambda.Value is byte[])
                    return lambda.Value;

                return lambda.Get<string>();
            }
            else if (lambda.Children.Any())
            {
                // Checking if we should return content as Hyperlambda.
                if (httpResponse.Headers.TryGetValue("Content-Type", out var val) && val == "application/x-hyperlambda")
                    return HyperlambdaGenerator.GetHyperlambda(lambda.Children);

                // Defaulting to returning content as JSON by converting from Lambda to JSON.
                var convert = new Node();
                convert.AddRange(lambda.Children.ToList());
                _signaler.Signal(".lambda2json-raw", convert);
                return convert.Value;
            }
            return null; // No content
        }

        #endregion
    }
}
