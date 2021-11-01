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
using magic.node.extensions.hyperlambda;

namespace magic.endpoint.tests
{
    public class EndpointTests
    {
        [Fact]
        public async Task SimpleGet()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IExecutorAsync)) as IExecutorAsync;

            var result = await executor.ExecuteGetAsync(
                "modules/foo-1",
                null,
                new List<(string, string)>(),
                new List<(string, string)>(),
                "localhost",
                "http");

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
            var executor = svc.GetService(typeof(IExecutorAsync)) as IExecutorAsync;

            var result = await executor.ExecuteGetAsync(
                "modules/not-existing",
                null,
                new List<(string, string)>(),
                new List<(string, string)>(),
                "localhost",
                "http");

            Assert.Equal(404, result.Result);
        }

        [Fact]
        public async Task GetWithoutHeader_Throws()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IExecutorAsync)) as IExecutorAsync;

            await Assert.ThrowsAsync<HyperlambdaException>(
                async () => await executor.ExecuteGetAsync(
                    "modules/request-header",
                    null,
                    new List<(string Name, string Value)>(),
                    new List<(string, string)>(),
                    "localhost",
                    "http"));
        }

        [Fact]
        public async Task GetWithHeader()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IExecutorAsync)) as IExecutorAsync;

            var headers = new List<(string Name, string Value)>();
            headers.Add(("foo", "bar"));

            var result = await executor.ExecuteGetAsync(
                "modules/request-header",
                null,
                headers,
                new List<(string, string)>(),
                "localhost",
                "http");

            Assert.Equal(200, result.Result);
            Assert.Equal("success", result.Content);
        }

        [Fact]
        public async Task GetWithCookie()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IExecutorAsync)) as IExecutorAsync;

            var cookies = new List<(string Name, string Value)>();
            cookies.Add(("foo", "bar"));

            var result = await executor.ExecuteGetAsync(
                "modules/request-cookie",
                null,
                new List<(string, string)>(),
                cookies,
                "localhost",
                "http");

            Assert.Equal(200, result.Result);
            Assert.Equal("success", result.Content);
        }

        [Fact]
        public async Task EchoHeaders()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IExecutorAsync)) as IExecutorAsync;

            var headers = new List<(string Name, string Value)>();
            headers.Add(("foo1", "bar1"));
            headers.Add(("foo2", "bar2"));

            var result = await executor.ExecuteGetAsync(
                "modules/echo-headers",
                null,
                headers,
                new List<(string, string)>(),
                "localhost",
                "http");

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
            var executor = svc.GetService(typeof(IExecutorAsync)) as IExecutorAsync;

            var cookies = new List<(string Name, string Value)>();
            cookies.Add(("foo1", "bar1"));
            cookies.Add(("foo2", "bar2"));

            var result = await executor.ExecuteGetAsync(
                "modules/echo-cookies",
                null,
                new List<(string, string)>(),
                cookies,
                "localhost",
                "http");

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
            var executor = svc.GetService(typeof(IExecutorAsync)) as IExecutorAsync;

            await Assert.ThrowsAsync<HyperlambdaException>(
                async () => await executor.ExecuteGetAsync(
                    "modules/throws",
                    null,
                    new List<(string, string)>(),
                    new List<(string, string)>(),
                    "localhost",
                    "http"));
        }

        [Fact]
        public async Task SimpleGetStringValue()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IExecutorAsync)) as IExecutorAsync;

            var result = await executor.ExecuteGetAsync(
                "modules/foo-2",
                null,
                new List<(string, string)>(),
                new List<(string, string)>(),
                "localhost",
                "http");

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
            var executor = svc.GetService(typeof(IExecutorAsync)) as IExecutorAsync;

            // Notice, executor will convert arguments according to [.arguments] declaration.
            var input = new List<(string, string)>();
            input.Add(("input1", "foo"));
            input.Add(("input2", "5"));
            input.Add(("input3", "true"));

            var result = await executor.ExecuteGetAsync(
                "modules/echo",
                input,
                new List<(string, string)>(),
                new List<(string, string)>(),
                "localhost",
                "http");

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
            var executor = svc.GetService(typeof(IExecutorAsync)) as IExecutorAsync;

            // Notice, executor will convert arguments according to [.arguments] declaration.
            var input = new List<(string, string)>();
            input.Add(("input1", "foo"));

            var result = await executor.ExecuteGetAsync(
                "modules/echo",
                input, new List<(string, string)>(),
                new List<(string, string)>(),
                "localhost",
                "http");

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
            var executor = svc.GetService(typeof(IExecutorAsync)) as IExecutorAsync;

            // Notice, executor will convert arguments according to [.arguments] declaration.
            var input = new List<(string, string)>();
            input.Add(("inputXXX", "foo"));

            await Assert.ThrowsAsync<ArgumentException>(
                async () => await executor.ExecuteGetAsync(
                    "modules/echo",
                    input, new List<(string, string)>(),
                    new List<(string, string)>(),
                    "localhost",
                    "http"));
        }

        [Fact]
        public async Task GetBadInputSameArgumentTwice_Throws()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IExecutorAsync)) as IExecutorAsync;

            // Notice, executor will convert arguments according to [.arguments] declaration.
            var input = new List<(string, string)>();
            input.Add(("input1", "foo1"));
            input.Add(("input1", "foo2"));

            await Assert.ThrowsAsync<ArgumentException>(
                async () => await executor.ExecuteGetAsync(
                    "modules/echo",
                    input,
                    new List<(string, string)>(),
                    new List<(string, string)>(),
                    "localhost",
                    "http"));
        }

        [Fact]
        public async Task GetArgumentNoDeclaration()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IExecutorAsync)) as IExecutorAsync;

            // Notice, executor will convert arguments according to [.arguments] declaration.
            var input = new List<(string, string)>();
            input.Add(("inputXXX", "foo"));
            var result = await executor.ExecuteGetAsync(
                "modules/echo-no-declaration",
                input,
                new List<(string, string)>(),
                new List<(string, string)>(),
                "localhost",
                "http");

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
            var executor = svc.GetService(typeof(IExecutorAsync)) as IExecutorAsync;

            var result = await executor.ExecuteGetAsync(
                "modules/status",
                null,
                new List<(string, string)>(),
                new List<(string, string)>(),
                "localhost",
                "http");

            Assert.Equal(201, result.Result);
        }

        [Fact]
        public async Task GetHttpHeader()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IExecutorAsync)) as IExecutorAsync;

            var result = await executor.ExecuteGetAsync(
                "modules/header",
                null,
                new List<(string, string)>(),
                new List<(string, string)>(),
                "localhost",
                "http");

            Assert.Single(result.Headers);
            Assert.Equal("bar", result.Headers["foo"]);
        }

        [Fact]
        public async Task SimpleDelete()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IExecutorAsync)) as IExecutorAsync;

            var result = await executor.ExecuteDeleteAsync(
                "modules/foo-1",
                null,
                new List<(string, string)>(),
                new List<(string, string)>(),
                "localhost",
                "http");

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
            var executor = svc.GetService(typeof(IExecutorAsync)) as IExecutorAsync;

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

            var result = await executor.ExecutePostAsync(
                "modules/echo",
                null,
                input,
                new List<(string, string)>(),
                new List<(string, string)>(),
                "localhost",
                "http");

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
            var executor = svc.GetService(typeof(IExecutorAsync)) as IExecutorAsync;

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

            var result = await executor.ExecutePutAsync(
                "modules/echo",
                null,
                input,
                new List<(string, string)>(),
                new List<(string, string)>(),
                "localhost",
                "http");

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
            var executor = svc.GetService(typeof(IExecutorAsync)) as IExecutorAsync;

            var input = HyperlambdaParser.Parse(@"
input1:foo
input2:int:5");

            var result = await executor.ExecutePostAsync(
                "modules/echo",
                null,
                input,
                new List<(string, string)>(),
                new List<(string, string)>(),
                "localhost",
                "http");

            Assert.Equal(200, result.Result);
            Assert.Empty(result.Headers);
            var j = result.Content as JObject;
            Assert.NotNull(j);
            Assert.Equal(2, j.Count);
            Assert.Equal("foo", j["input1"].Value<string>());
            Assert.Equal(5, j["input2"].Value<int>());
        }
    }
}
