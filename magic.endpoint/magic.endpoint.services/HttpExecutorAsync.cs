/*
 * Magic Cloud, copyright Aista, Ltd. See the attached LICENSE file for details.
 */

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
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
        readonly IStreamService _streamService;
        readonly IRootResolver _rootResolver;
        readonly IHttpArgumentsHandler _argumentsHandler;

        /*
         * Registered Content-Type payload handlers, responsible for handling requests and parametrising invocation
         * according to Content-Type specified by caller.
         */
        internal static readonly Dictionary<string, string> _mimeTypes =
            new Dictionary<string, string>
        {
            {
                "json", "application/json"
            },
            {
                "css", "text/css"
            },
            {
                "js", "application/javascript"
            },
            {
                "jpeg", "image/jpeg"
            },
            {
                "jpg", "image/jpeg"
            },
            {
                "png", "image/png"
            },
            {
                "giv", "image/giv"
            },
            {
                "svg", "image/svg"
            },
            {
                "md", "text/markdown"
            },
        };

        /// <summary>
        /// Creates an instance of your type.
        /// </summary>
        /// <param name="signaler">Signaler necessary to execute endpoint.</param>
        /// <param name="fileService">Needed to resolve endpoint files.</param>
        /// <param name="streamService">Needed to resolve endpoint files.</param>
        /// <param name="rootResolver">Needed to resolve root folder names.</param>
        /// <param name="argumentsHandler">Needed to attach arguments to endpoint invocation.</param>
        public HttpExecutorAsync(
            ISignaler signaler,
            IFileService fileService,
            IStreamService streamService,
            IRootResolver rootResolver,
            IHttpArgumentsHandler argumentsHandler)
        {
            _signaler = signaler;
            _fileService = fileService;
            _streamService = streamService;
            _rootResolver = rootResolver;
            _argumentsHandler = argumentsHandler;
        }

        /// <inheritdoc/>
        public async Task<MagicResponse> ExecuteAsync(MagicRequest request)
        {
            // Checking if this is a Magic API type of URL.
            if (request.URL.StartsWith("magic/"))
                return await ExecuteEndpointAsync(request); // API invocation

            // Request is either for a statica file or a mixin file.
            if (request.Verb != "get")
                return new MagicResponse { Result = 404 };

            // Checking if this is a mixin file. Mixin files cannot have "." in their URLs.
            if (!request.URL.Contains("."))
                return await ServeMixinFileAsync(request); // Mixin file.

            // Statically served file.
            return await ServeStaticFileAsync(request);
        }

        #region [ -- Private helper methods -- ]

        /*
         * Serves a mixin file.
         */
        async Task<MagicResponse> ServeMixinFileAsync(MagicRequest request)
        {
            // Getting mixin file and sanity checking request.
            var file = await GetMixinFile(request.URL);
            if (file == null)
                return new MagicResponse { Content = "Not found", Result = 404 };

            // Executing mixing file.
            var lambda = new Node("mixin", file);
            foreach (var idx in request.Query)
            {
                lambda.Add(new Node(idx.Key, idx.Value));
            }

            // Creating our result wrapper, wrapping whatever the endpoint wants to return to the client.
            var response = new MagicResponse();
            response.Headers["Content-Type"] = "text/html";
            await _signaler.ScopeAsync("http.request", request, async () =>
            {
                await _signaler.ScopeAsync("http.response", response, async () =>
                {
                    await _signaler.SignalAsync("mixin", lambda);
                });
            });
            response.Content = lambda.Value;
            return response;
        }

        /*
         * Returns the filename for the mixin file matching the specified URL, if any.
         */
        async Task<string> GetMixinFile(string url)
        {
            // Sanity checking invocation, eliminating dplicated requests.
            if (url.EndsWith("index"))
                return null; // Illegal request to avoid duplicated content.

            // Trying to resolve to explicitly specified file first.
            if (!string.IsNullOrEmpty(url) && await _fileService.ExistsAsync(_rootResolver.AbsolutePath("/etc/www/" + url + ".html")))
                return "/etc/www/" + url + ".html";

            // Trying to see if index.html file exists within folder, assuming last parts of URL is a folder.
            if (await _fileService.ExistsAsync(_rootResolver.AbsolutePath("/etc/www/" + (url == "" ? url : url + "/") + "index.html")))
                return "/etc/www/" + (url == "" ? url : url + "/") + "index.html";

            // Trying to find an index file inside specified folder.
            var splits = url.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            // Removing last entity such that we can check if index file exists.
            splits = splits.Take(splits.Length - 1).ToArray();

            // Creating the filepath to index file inside specified folder, and checking if it sxists.
            var joins = string.Join("/", splits);
            url = "/etc/www/" + (joins == "" ? joins : joins + "/") + "default.html";
            if (await _fileService.ExistsAsync(_rootResolver.AbsolutePath(url)))
                return url;

            // Returning result to caller
            return null;
        }

        /*
         * Serves a static file.
         */
        async Task<MagicResponse> ServeStaticFileAsync(MagicRequest request)
        {
            /*
             * Sanity checking invocation to prevent caller from accessing private folder,
             * implying folders with "." in their names. Only filenames are allowed to contain ".",
             * implying the last entity once we split on "/".
             */
            var splits = request.URL.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var idx in splits.Take(Math.Max(0, splits.Length - 1)))
            {
                if (idx.Contains("."))
                    return new MagicResponse { Result = 404, Content = "Not found" };
            }

            // Transforming to absolute path and verifying file exists.
            var absPath = _rootResolver.AbsolutePath("/etc/www/" + request.URL);
            if (!await _fileService.ExistsAsync(absPath))
                return new MagicResponse { Result = 404, Content = "Not found" };

            // Statically served file.
            var result = new MagicResponse();
            var ext = splits.Last().Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries).Last();
            result.Headers["Content-Type"] = _mimeTypes[ext];
            result.Content = await _streamService.OpenFileAsync(_rootResolver.AbsolutePath("/etc/www/" + request.URL));
            return result;
        }

        /*
         * Executes an API endpoint type of URL
         */
        async Task<MagicResponse> ExecuteEndpointAsync(MagicRequest request)
        {
            // Normalising URL according to expectations.
            var url = request.URL.Substring(6);

            // Making sure we never resolve to anything outside of "modules/" and "system/" folder.
            if (!url.StartsWith("modules/") && !url.StartsWith("system/"))
                return new MagicResponse { Result = 401 };

            // Normalising URL property in request to make sure we're backwards compatible.
            request.URL = url;

            // Figuring out file to execute, and doing some basic sanity checking.
            var path = Utilities.GetEndpointFilePath(_rootResolver, url, request.Verb);
            if (!await _fileService.ExistsAsync(path))
                return new MagicResponse { Result = 404 };

            // Creating our lambda object by loading Hyperlambda file.
            var lambda = HyperlambdaParser.Parse(await _fileService.LoadAsync(path));

            // Applying interceptors.
            lambda = await ApplyInterceptors(lambda, url);

            // Attaching arguments.
            _argumentsHandler.Attach(lambda, request.Query, request.Payload);

            // Invoking method responsible for actually executing lambda object.
            return await ExecuteAsync(lambda, request);
        }

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
