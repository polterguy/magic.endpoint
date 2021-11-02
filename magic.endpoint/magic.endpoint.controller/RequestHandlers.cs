/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2021, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System.IO;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using magic.node;
using magic.signals.contracts;

namespace magic.endpoint.controller
{
    /*
     * Default Content-Type request handlers responsible for parametrising IExecutorAsync invocation according
     * to Content-Type specified by client.
     */
    internal static class RequestHandlers
    {
        /*
         * Default JSON handler, simply de-serialising JSON and creating a
         * Node/Lambda argument collection.
         */
        internal static async Task<Node> JsonHandler(ISignaler signaler, HttpRequest request)
        {
            // Figuring out encoding of request.
            var encoding = request.ContentType?
                .Split(';')
                .Select(x => x.Trim())
                .FirstOrDefault(x => x.StartsWith("char-set"))?
                .Split('=')
                .Skip(1)
                .FirstOrDefault()?
                .Trim('"') ?? "utf-8";

            // Reading body as JSON from request, now with correctly applied encoding.
            var args = new Node("", request.Body);
            args.Add(new Node("encoding", encoding));
            await signaler.SignalAsync("json2lambda-stream", args);
            return args;
        }

        /*
         * URL encoded handler, de-serialising URL encoded data from body and
         * creating a Node/Lambda argument collection.
         */
        internal static async Task<Node> UrlEncodedHandler(ISignaler signaler, HttpRequest request)
        {
            // URL encoded transmission, reading arguments as such.
            var collection = await request.ReadFormAsync();
            var args = new Node();
            foreach (var idx in collection)
            {
                args.Add(new Node(idx.Key, idx.Value.ToString()));
            }
            return args;
        }

        /*
         * Multipart (MIME) form data handler, de-serialising MIME, but avoids reading files into memory,
         * and rather keeping these as raw streams, building a Node/Lambda argument collection.
         */
        internal static async Task<Node> FormDataHandler(ISignaler signaler, HttpRequest request)
        {
            // MIME content, reading arguments as such.
            var collection = await request.ReadFormAsync();
            var args = new Node();
            foreach (var idx in collection)
            {
                args.Add(new Node(idx.Key, idx.Value.ToString()));
            }

            // Notice, we don't read files into memory, but simply transfer these as Stream objects to Hyperlambda.
            foreach (var idxFile in collection.Files)
            {
                var fileStream = idxFile.OpenReadStream();
                var tmp = new Node("file");
                tmp.Add(new Node("name", idxFile.FileName));
                tmp.Add(new Node("stream", fileStream));
                args.Add(tmp);
            }
            return args;
        }

        /*
         * Hyperlambda handler, reading Hyperlambda as raw string, creating a Node/Lambda collection
         * where the Hyperlambda is passed in as a [body] argument.
         */
        internal static async Task<Node> HyperlambdaHandler(ISignaler signaler, HttpRequest request)
        {
            // Figuring out encoding of request.
            var encoding = request.ContentType?
                .Split(';')
                .Select(x => x.Trim())
                .FirstOrDefault(x => x.StartsWith("char-set"))?
                .Split('=')
                .Skip(1)
                .FirstOrDefault()?
                .Trim('"') ?? "utf-8";

            // Reading stream as Hyperlambda, using encoding provided by caller, defaulting to UTF8.
            var args = new Node();
            using (var reader = new StreamReader(request.Body, Encoding.GetEncoding(encoding)))
            {  
                var payload = await reader.ReadToEndAsync();
                args.Add(new Node("body", payload));
            }
            return args;
        }
    }
}
