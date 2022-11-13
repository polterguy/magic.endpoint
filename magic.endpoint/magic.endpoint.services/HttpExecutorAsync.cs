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
        static readonly Dictionary<string, string> _mimeTypes =
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
                "gif", "image/gif"
            },
            {
                "svg", "image/svg+xml"
            },
            {
                "md", "text/markdown"
            },
            {
                "html", "text/html"
            },
        };

        /*
         * Associates a file extension with a MIME type.
         */
        internal static void AddMimeType(string extension, string mimeType)
        {
            _mimeTypes[extension] = mimeType;
        }

        /*
         * Returns all file extensions to IME types associations in the system.
         */
        internal static IEnumerable<(string Ext, string Mime)> GetMimeTypes()
        {
            foreach (var idx in _mimeTypes)
            {
                yield return (idx.Key, idx.Value);
            }
        }

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
                return await ExecuteEndpointAsync(request); // API invocation.

            // File request towards "/etc/www/" folder.
            return await ServeFileAsync(request);
        }

        #region [ -- Private helper methods -- ]

        /*
         * Serves a file from the "/etc/www/" folder.
         */
        async Task<MagicResponse> ServeFileAsync(MagicRequest request)
        {
            // Making sure request is legal.
            if (!Utilities.IsLegalFileRequest(request.URL))
                return new MagicResponse { Result = 404 };

            // Checking if this is a mixin file.
            if (Utilities.IsHtmlFileRequest(request.URL))
                return await ServeHtmlFileAsync(request); // HTML file, might have Hyperlambda codebehind file.

            // Statically served file.
            return await ServeStaticFileAsync("/etc/www/" + request.URL);
        }

        /*
         * Serves an HTML file that might have a Hyperlambda codebehind file associated with it.
         */
        async Task<MagicResponse> ServeHtmlFileAsync(MagicRequest request)
        {
            // Getting mixin file and sanity checking request.
            var file = await GetHtmlFilename(request.URL);
            if (file == null)
                return new MagicResponse { Content = "Not found", Result = 404 };

            // Checking if Hyperlambda codebehind file exists.
            var codebehindFile = file.Substring(0, file.Length - 5) + ".hl";
            if (await _fileService.ExistsAsync(_rootResolver.AbsolutePath(codebehindFile)))
                return await ServeDynamicPage(request, file, codebehindFile); // Codebehind file exists.

             // No codebehind file, serving file as static content file.
            return await ServeStaticFileAsync(file);
        }

        /*
         * Serves an HTML file that has an associated Hyperlambda codebehind file.
         */
        async Task<MagicResponse> ServeDynamicPage(MagicRequest request, string htmlFile, string codebehindFile)
        {
            // Creating our lambda object by loading Hyperlambda file.
            Node codebehind = new Node();
            codebehind = HyperlambdaParser.Parse(await _fileService.LoadAsync(_rootResolver.AbsolutePath(codebehindFile)));

            // Applying interceptors.
            codebehind = await ApplyHtmlInterceptors(codebehind, codebehindFile, htmlFile);

            // Attaching arguments.
            _argumentsHandler.Attach(codebehind, request.Query, request.Payload);

            // Creating our result wrapper, wrapping whatever the endpoint wants to return to the client.
            var response = new MagicResponse();
            var result = new Node();
            response.Headers["Content-Type"] = "text/html";
            await _signaler.ScopeAsync("http.request", request, async () =>
            {
                await _signaler.ScopeAsync("http.response", response, async () =>
                {
                    await _signaler.ScopeAsync("slots.result", result, async () =>
                    {
                        await _signaler.SignalAsync("eval", codebehind);
                    });
                });
            });
            response.Content = result.Value;
            return response;
        }

        /*
         * Returns the filename for the mixin file matching the specified URL, if any.
         */
        async Task<string> GetHtmlFilename(string url)
        {
            // Checking if this is a request for a folder, at which point we append "index.html" to it.
            if (url == string.Empty || url.EndsWith("/"))
                url += "index.html";
            else if (!url.EndsWith(".html"))
                url += ".html"; // Apppending ".html" to resolve correct document.

            // Trying to resolve URL as a filename request.
            if (await _fileService.ExistsAsync(_rootResolver.AbsolutePath("/etc/www/" + url)))
                return "/etc/www/" + url;

            /*
             * Traversing upwards in folder hierarchy and returning the
             * first "default.html" page we can find, if any.
             *
             * This allows you to have "wildcard resolvers" for entire folder hierarchies.
             */
            var splits = url.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (splits.Length > 0)
                splits = splits.Take(splits.Length - 1).ToArray();
            while (true)
            {
                var cur = string.Join("/", splits) + "/" + "default.html";
                if (await _fileService.ExistsAsync(_rootResolver.AbsolutePath("/etc/www/" + cur)))
                    return "/etc/www/" + cur;
                if (splits.Length == 0)
                    break;
                splits = splits.Take(splits.Length - 1).ToArray();
            }

            // Nothing found that can resolve specified URL.
            return null;
        }

        /*
         * Serves a static file.
         */
        async Task<MagicResponse> ServeStaticFileAsync(string url)
        {
            // Transforming to absolute path and verifying file exists.
            var absPath = _rootResolver.AbsolutePath(url);
            if (!await _fileService.ExistsAsync(absPath))
                return new MagicResponse { Result = 404, Content = "Not found" };

            // Creating response and returning to caller.
            var result = new MagicResponse();
            var ext = url.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries).Last();
            if (_mimeTypes.ContainsKey(ext))
                result.Headers["Content-Type"] = _mimeTypes[ext];
            else
                result.Headers["Content-Type"] = "application/octet-stream"; // Defaulting to binary content
            result.Content = await _streamService.OpenFileAsync(_rootResolver.AbsolutePath(url));
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
            lambda = await ApplyApiInterceptors(lambda, url);

            // Attaching arguments.
            _argumentsHandler.Attach(lambda, request.Query, request.Payload);

            // Invoking method responsible for actually executing lambda object.
            return await ExecuteAsync(lambda, request);
        }

        /*
         * Applies interceptors to specified Node/Lambda object.
         */
        async Task<Node> ApplyApiInterceptors(Node result, string url)
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
                    result = await ApplyApiInterceptor(result, current);

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
         * Applies interceptors to specified Node/Lambda object.
         */
        async Task<Node> ApplyHtmlInterceptors(Node result, string hlFile, string htmlFile)
        {
            // Checking to see if interceptors exists recursively upwards in folder hierarchy.
            var splits = hlFile.Split(new char [] {'/'}, StringSplitOptions.RemoveEmptyEntries);

            // Stripping away last entity (filename) of invocation.
            var folders = splits.Take(splits.Length - 1);

            // Iterating as long as we have more entities in list of folders.
            while (true)
            {
                // Checking if "current-folder/interceptor.hl" file exists.
                var current = _rootResolver.AbsolutePath(string.Join("/", folders) + "/interceptor.hl");
                if (_fileService.Exists(current))
                    result = await ApplyHtmlInterceptor(result, current, htmlFile);

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
        async Task<Node> ApplyApiInterceptor(Node lambda, string interceptorFile)
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
         * Applies the specified interceptor and returns the transformed Node/Lambda result.
         */
        async Task<Node> ApplyHtmlInterceptor(Node lambda, string interceptorFile, string html)
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
                // This logic ensures we keep existing order without any fuzz.
                // By cloning node we also support having multiple [.interceptor] nodes.
                var cur = new Node("io.file.mixin", html);
                cur.AddRange(lambda.Clone().Children);
                idxLambda.InsertBefore(cur);

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
