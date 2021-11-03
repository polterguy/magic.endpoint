/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2021, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Xunit;
using Newtonsoft.Json.Linq;
using magic.lambda.exceptions;
using magic.endpoint.contracts;
using magic.endpoint.contracts.poco;
using magic.node.extensions.hyperlambda;

namespace magic.endpoint.tests
{
    public class EndpointTests
    {
        [Fact]
        public async Task SimpleGet()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IHttpExecutorAsync)) as IHttpExecutorAsync;

            var result = await executor.ExecuteAsync(
                new MagicRequest
                {
                    URL = "modules/foo-1",
                    Verb = "get",
                    Query = new Dictionary<string, string>(),
                    Headers = new Dictionary<string, string>(),
                    Cookies = new Dictionary<string, string>(),
                    Host = "localhost",
                    Scheme = "http"
                });

            Assert.Equal(200, result.Result);
            Assert.Empty(result.Headers);
            var j = result.Content as JObject;
            Assert.NotNull(j);
            Assert.Equal("hello world", j["result"].Value<string>());
        }

        [Fact]
        public async Task Get404()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IHttpExecutorAsync)) as IHttpExecutorAsync;

            var result = await executor.ExecuteAsync(
                new MagicRequest
                {
                    URL = "modules/not-existing",
                    Verb = "get",
                    Query = new Dictionary<string, string>(),
                    Headers = new Dictionary<string, string>(),
                    Cookies = new Dictionary<string, string>(),
                    Host = "localhost",
                    Scheme = "http"
                });

            Assert.Equal(404, result.Result);
        }

        [Fact]
        public async Task GetWithHeader()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IHttpExecutorAsync)) as IHttpExecutorAsync;

            var result = await executor.ExecuteAsync(
                new MagicRequest
                {
                    URL = "modules/request-header",
                    Verb = "get",
                    Query = new Dictionary<string, string>(),
                    Headers = new Dictionary<string, string>{ { "foo", "bar" } },
                    Cookies = new Dictionary<string, string>(),
                    Host = "localhost",
                    Scheme = "http"
                });

            Assert.Equal(200, result.Result);
            Assert.Equal("success", result.Content);
        }

        [Fact]
        public async Task GetWithCookie()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IHttpExecutorAsync)) as IHttpExecutorAsync;

            var result = await executor.ExecuteAsync(
                new MagicRequest
                {
                    URL = "modules/request-cookie",
                    Verb = "get",
                    Query = new Dictionary<string, string>(),
                    Headers = new Dictionary<string, string>(),
                    Cookies = new Dictionary<string, string>{ { "foo", "bar" } },
                    Host = "localhost",
                    Scheme = "http"
                });

            Assert.Equal(200, result.Result);
            Assert.Equal("success", result.Content);
        }

        [Fact]
        public async Task EchoHeaders()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IHttpExecutorAsync)) as IHttpExecutorAsync;

            var result = await executor.ExecuteAsync(
                new MagicRequest
                {
                    URL = "modules/echo-headers",
                    Verb = "get",
                    Query = new Dictionary<string, string>(),
                    Headers = new Dictionary<string, string>{ { "foo1", "bar1" }, { "foo2", "bar2" } },
                    Cookies = new Dictionary<string, string>(),
                    Host = "localhost",
                    Scheme = "http"
                });

            Assert.Equal(200, result.Result);
            var content = result.Content as JContainer;
            Assert.Equal(2, content.Count);
            Assert.Equal("bar1", content["foo1"].Value<string>());
            Assert.Equal("bar2", content["foo2"].Value<string>());
        }

        [Fact]
        public async Task EchoCookies()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IHttpExecutorAsync)) as IHttpExecutorAsync;

            var result = await executor.ExecuteAsync(
                new MagicRequest
                {
                    URL = "modules/echo-cookies",
                    Verb = "get",
                    Query = new Dictionary<string, string>(),
                    Headers = new Dictionary<string, string>(),
                    Cookies = new Dictionary<string, string>{ { "foo1", "bar1" }, { "foo2", "bar2" } },
                    Host = "localhost",
                    Scheme = "http"
                });

            Assert.Equal(200, result.Result);
            var content = result.Content as JContainer;
            Assert.Equal(2, content.Count);
            Assert.Equal("bar1", content["foo1"].Value<string>());
            Assert.Equal("bar2", content["foo2"].Value<string>());
        }

        [Fact]
        public async Task Get_Throws()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IHttpExecutorAsync)) as IHttpExecutorAsync;

            await Assert.ThrowsAsync<HyperlambdaException>(
                async () => await executor.ExecuteAsync(
                new MagicRequest
                {
                    URL = "modules/throws",
                    Verb = "get",
                    Query = new Dictionary<string, string>(),
                    Headers = new Dictionary<string, string>(),
                    Cookies = new Dictionary<string, string>(),
                    Host = "localhost",
                    Scheme = "http"
                }));
        }

        [Fact]
        public async Task SimpleGetStringValue()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IHttpExecutorAsync)) as IHttpExecutorAsync;

            var result = await executor.ExecuteAsync(
                new MagicRequest
                {
                    URL = "modules/foo-2",
                    Verb = "get",
                    Query = new Dictionary<string, string>(),
                    Headers = new Dictionary<string, string>(),
                    Cookies = new Dictionary<string, string>(),
                    Host = "localhost",
                    Scheme = "http"
                });

            Assert.Equal(200, result.Result);
            Assert.Empty(result.Headers);
            var j = result.Content as string;
            Assert.NotNull(j);
            Assert.Equal("hello world", j);
        }

        [Fact]
        public async Task GetEcho()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IHttpExecutorAsync)) as IHttpExecutorAsync;

            var result = await executor.ExecuteAsync(
                new MagicRequest
                {
                    URL = "modules/echo",
                    Verb = "get",
                    Query = new Dictionary<string, string>{ { "input1", "foo" }, { "input2", "5" }, { "input3", "true" } },
                    Headers = new Dictionary<string, string>(),
                    Cookies = new Dictionary<string, string>(),
                    Host = "localhost",
                    Scheme = "http"
                });

            Assert.Equal(200, result.Result);
            Assert.Empty(result.Headers);
            var j = result.Content as JObject;
            Assert.NotNull(j);
            Assert.Equal("foo", j["input1"].Value<string>());
            Assert.Equal(5, j["input2"].Value<int>());
            Assert.True(j["input3"].Value<bool>());
        }

        [Fact]
        public async Task GetEchoPartialArgumentList()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IHttpExecutorAsync)) as IHttpExecutorAsync;

            var result = await executor.ExecuteAsync(
                new MagicRequest
                {
                    URL = "modules/echo",
                    Verb = "get",
                    Query = new Dictionary<string, string>{ { "input1", "foo" } },
                    Headers = new Dictionary<string, string>(),
                    Cookies = new Dictionary<string, string>(),
                    Host = "localhost",
                    Scheme = "http"
                });

            Assert.Equal(200, result.Result);
            Assert.Empty(result.Headers);
            var j = result.Content as JObject;
            Assert.NotNull(j);
            Assert.Single(j);
            Assert.Equal("foo", j["input1"].Value<string>());
        }

        [Fact]
        public async Task GetBadInput_Throws()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IHttpExecutorAsync)) as IHttpExecutorAsync;

            await Assert.ThrowsAsync<ArgumentException>(
                async () => await executor.ExecuteAsync(
                new MagicRequest
                {
                    URL = "modules/echo",
                    Verb = "get",
                    Query = new Dictionary<string, string>{ { "inputXXX", "foo" } },
                    Headers = new Dictionary<string, string>(),
                    Cookies = new Dictionary<string, string>(),
                    Host = "localhost",
                    Scheme = "http"
                }));
        }

        [Fact]
        public async Task GetArgumentNoDeclaration()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IHttpExecutorAsync)) as IHttpExecutorAsync;

            // Notice, executor will convert arguments according to [.arguments] declaration.
            var result = await executor.ExecuteAsync(
                new MagicRequest
                {
                    URL = "modules/echo-no-declaration",
                    Verb = "get",
                    Query = new Dictionary<string, string>{ { "inputXXX", "foo" } },
                    Headers = new Dictionary<string, string>(),
                    Cookies = new Dictionary<string, string>(),
                    Host = "localhost",
                    Scheme = "http"
                });

            Assert.Equal(200, result.Result);
            Assert.Empty(result.Headers);
            var j = result.Content as JObject;
            Assert.NotNull(j);
            Assert.Equal("foo", j["inputXXX"].Value<string>());
        }

        [Fact]
        public async Task GetStatusResponse()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IHttpExecutorAsync)) as IHttpExecutorAsync;

            var result = await executor.ExecuteAsync(
                new MagicRequest
                {
                    URL = "modules/status",
                    Verb = "get",
                    Query = new Dictionary<string, string>{ { "inputXXX", "foo" } },
                    Headers = new Dictionary<string, string>(),
                    Cookies = new Dictionary<string, string>(),
                    Host = "localhost",
                    Scheme = "http"
                });

            Assert.Equal(201, result.Result);
        }

        [Fact]
        public async Task GetHttpHeader()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IHttpExecutorAsync)) as IHttpExecutorAsync;

            var result = await executor.ExecuteAsync(
                new MagicRequest
                {
                    URL = "modules/header",
                    Verb = "get",
                    Query = new Dictionary<string, string>{ { "inputXXX", "foo" } },
                    Headers = new Dictionary<string, string>(),
                    Cookies = new Dictionary<string, string>(),
                    Host = "localhost",
                    Scheme = "http"
                });

            Assert.Single(result.Headers);
            Assert.Equal("bar", result.Headers["foo"]);
        }

        [Fact]
        public async Task SimpleDelete()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IHttpExecutorAsync)) as IHttpExecutorAsync;

            var result = await executor.ExecuteAsync(
                new MagicRequest
                {
                    URL = "modules/foo-1",
                    Verb = "delete",
                    Query = new Dictionary<string, string>{ { "inputXXX", "foo" } },
                    Headers = new Dictionary<string, string>(),
                    Cookies = new Dictionary<string, string>(),
                    Host = "localhost",
                    Scheme = "http"
                });

            Assert.Equal(200, result.Result);
            Assert.Empty(result.Headers);
            var j = result.Content as JObject;
            Assert.NotNull(j);
            Assert.Equal("hello world", j["result"].Value<string>());
        }

        [Fact]
        public async Task PostEcho()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IHttpExecutorAsync)) as IHttpExecutorAsync;

            var input = HyperlambdaParser.Parse(@"
input1:foo
input2:int:5
input3:bool:true
input4
   .
      arr1:bool:true
      arr2:57
      arr3:any-object
   .
      arr1:bool:false
      arr2:int:67
      arr3:guid:4c248403-23a7-4808-988c-1be59a4a90af
input5
   obj1:foo
   obj2:true");

            var result = await executor.ExecuteAsync(
                new MagicRequest
                {
                    URL = "modules/echo",
                    Verb = "post",
                    Query = new Dictionary<string, string>(),
                    Headers = new Dictionary<string, string>(),
                    Cookies = new Dictionary<string, string>(),
                    Host = "localhost",
                    Scheme = "http",
                    Payload = input
                });

            Assert.Equal(200, result.Result);
            Assert.Empty(result.Headers);
            var j = result.Content as JObject;
            Assert.NotNull(j);
            Assert.Equal("foo", j["input1"].Value<string>());
            Assert.Equal(5, j["input2"].Value<int>());
            Assert.True(j["input3"].Value<bool>());
            Assert.NotNull(j["input4"].Value<JArray>());
            Assert.Equal(2, j["input4"].Value<JArray>().Count);
            Assert.True(j["input4"].Value<JArray>()[0]["arr1"].Value<bool>());
            Assert.Equal(57, j["input4"].Value<JArray>()[0]["arr2"].Value<int>());
            Assert.Equal("any-object", j["input4"].Value<JArray>()[0]["arr3"].Value<string>());
            Assert.False(j["input4"].Value<JArray>()[1]["arr1"].Value<bool>());
            Assert.Equal(67, j["input4"].Value<JArray>()[1]["arr2"].Value<int>());
            Assert.True(j["input4"].Value<JArray>()[1]["arr3"].Value<Guid>().ToString() != Guid.Empty.ToString());
            Assert.NotNull(j["input5"].Value<JObject>());
            Assert.Equal("foo", j["input5"].Value<JObject>()["obj1"].Value<string>());
            Assert.True(j["input5"].Value<JObject>()["obj2"].Value<bool>());
        }

        [Fact]
        public async Task PutEcho()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IHttpExecutorAsync)) as IHttpExecutorAsync;

            var input = HyperlambdaParser.Parse(@"
input1:foo
input2:int:5
input3:bool:true
input4
   .
      arr1:bool:true
      arr2:57
      arr3:any-object
   .
      arr1:bool:false
      arr2:int:67
      arr3:guid:4c248403-23a7-4808-988c-1be59a4a90af
input5
   obj1:foo
   obj2:true");

            var result = await executor.ExecuteAsync(
                new MagicRequest
                {
                    URL = "modules/echo",
                    Verb = "put",
                    Query = new Dictionary<string, string>(),
                    Headers = new Dictionary<string, string>(),
                    Cookies = new Dictionary<string, string>(),
                    Host = "localhost",
                    Scheme = "http",
                    Payload = input
                });

            Assert.Equal(200, result.Result);
            Assert.Empty(result.Headers);
            var j = result.Content as JObject;
            Assert.NotNull(j);
            Assert.Equal("foo", j["input1"].Value<string>());
            Assert.Equal(5, j["input2"].Value<int>());
            Assert.True(j["input3"].Value<bool>());
            Assert.NotNull(j["input4"].Value<JArray>());
            Assert.Equal(2, j["input4"].Value<JArray>().Count);
            Assert.True(j["input4"].Value<JArray>()[0]["arr1"].Value<bool>());
            Assert.Equal(57, j["input4"].Value<JArray>()[0]["arr2"].Value<int>());
            Assert.Equal("any-object", j["input4"].Value<JArray>()[0]["arr3"].Value<string>());
            Assert.False(j["input4"].Value<JArray>()[1]["arr1"].Value<bool>());
            Assert.Equal(67, j["input4"].Value<JArray>()[1]["arr2"].Value<int>());
            Assert.True(j["input4"].Value<JArray>()[1]["arr3"].Value<Guid>().ToString() != Guid.Empty.ToString());
            Assert.NotNull(j["input5"].Value<JObject>());
            Assert.Equal("foo", j["input5"].Value<JObject>()["obj1"].Value<string>());
            Assert.True(j["input5"].Value<JObject>()["obj2"].Value<bool>());
        }

        [Fact]
        public async Task PostEchoPartialArgumentList()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IHttpExecutorAsync)) as IHttpExecutorAsync;

            var input = HyperlambdaParser.Parse(@"
input1:foo
input2:int:5");

            var result = await executor.ExecuteAsync(
                new MagicRequest
                {
                    URL = "modules/echo",
                    Verb = "post",
                    Query = new Dictionary<string, string>(),
                    Headers = new Dictionary<string, string>(),
                    Cookies = new Dictionary<string, string>(),
                    Host = "localhost",
                    Scheme = "http",
                    Payload = input
                });

            Assert.Equal(200, result.Result);
            Assert.Empty(result.Headers);
            var j = result.Content as JObject;
            Assert.NotNull(j);
            Assert.Equal(2, j.Count);
            Assert.Equal("foo", j["input1"].Value<string>());
            Assert.Equal(5, j["input2"].Value<int>());
        }

        [Fact]
        public async Task Interceptors()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IHttpExecutorAsync)) as IHttpExecutorAsync;

            var result = await executor.ExecuteAsync(
                new MagicRequest
                {
                    URL = "modules/interceptors/foo",
                    Verb = "get",
                    Query = new Dictionary<string, string>(),
                    Headers = new Dictionary<string, string>(),
                    Cookies = new Dictionary<string, string>(),
                    Host = "localhost",
                    Scheme = "http"
                });

            Assert.Equal(200, result.Result);
            Assert.Single(result.Headers);
            var hl = result.Content as string;
            Assert.NotNull(hl);
            var lambda = HyperlambdaParser.Parse(hl);
            Assert.Equal(".foo", lambda.Children.First().Name);
            Assert.Equal("howdy", lambda.Children.First().Value);
        }

    }
}
