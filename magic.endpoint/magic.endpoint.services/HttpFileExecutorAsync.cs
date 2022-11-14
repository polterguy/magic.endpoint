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
    public class HttpFileExecutorAsync : IHttpExecutorAsync
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
            { "json", "application/json" },
            { "css", "text/css" },
            { "txt", "text/plain" },
            { "js", "application/javascript" },
            { "jpeg", "image/jpeg" },
            { "jpg", "image/jpeg" },
            { "png", "image/png" },
            { "gif", "image/gif" },
            { "svg", "image/svg+xml" },
            { "md", "text/markdown" },
            { "html", "text/html" },
        };

        /*
         * Associates a file extension with a MIME type.
         */
        internal static void AddMimeType(string extension, string mimeType)
        {
            lock (_mimeTypes)
            {
                _mimeTypes[extension] = mimeType;
            }
        }

        /*
         * Returns all file extensions to IME types associations in the system.
         */
        internal static IEnumerable<(string Ext, string Mime)> GetMimeTypes()
        {
            lock (_mimeTypes)
            {
                foreach (var idx in _mimeTypes)
                {
                    yield return (idx.Key, idx.Value);
                }
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
        public HttpFileExecutorAsync(
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
            // Making sure request is legal.
            if (!Utilities.IsLegalFileRequest(request.URL))
                return new MagicResponse { Result = 404 };

            // Checking if this is a mixin file.
            if (Utilities.IsHtmlFileRequest(request.URL))
                return await ServeHtmlFileAsync(request); // HTML file, might have Hyperlambda codebehind file.

            // Statically served file.
            return await ServeStaticFileAsync("/etc/www/" + request.URL);
        }

        #region [ -- Private helper methods -- ]

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

            // Creating our initial lambda object.
            var lambda = new Node("");
            var mixin = new Node("io.file.mixin", htmlFile);
            mixin.AddRange(codebehind.Children);
            lambda.Add(mixin);
            lambda.Add(new Node("return", new Expression("-")));

            // Applying interceptors.
            lambda = await Utilities.ApplyInterceptors(_rootResolver, _fileService, lambda, codebehindFile);

            // Attaching arguments.
            _argumentsHandler.Attach(lambda, request.Query, request.Payload);

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
                        await _signaler.SignalAsync("eval", lambda);
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
            if (url.EndsWith("/"))
                url += "index.html";
            else if (!url.EndsWith(".html"))
                url += ".html"; // Apppending ".html" to resolve correct document.
            if (url.StartsWith("/"))
                url = url.Substring(1);

            // Trying to resolve URL as a direct filename request.
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

        #endregion
    }
}
