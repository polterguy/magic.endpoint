/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2020, thomas@servergardens.com, all rights reserved.
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
using magic.node.extensions;

namespace magic.endpoint.tests
{
    public class EndpointTests
    {
        [Fact]
        public async Task SimpleGet()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IExecutorAsync)) as IExecutorAsync;
            var result = await executor.ExecuteGetAsync("foo-1", null, new List<(string, string)>());
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
            var result = await executor.ExecuteGetAsync("not-existing", null, new List<(string, string)>());
            Assert.Equal(404, result.Result);
        }

        [Fact]
        public async Task GetWithoutHeader_Throws()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IExecutorAsync)) as IExecutorAsync;
            var headers = new List<(string Name, string Value)>();
            await Assert.ThrowsAsync<ArgumentException>(async () => await executor.ExecuteGetAsync("request-header", null, headers));
        }

        [Fact]
        public async Task GetWithHeader()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IExecutorAsync)) as IExecutorAsync;
            var headers = new List<(string Name, string Value)>();
            headers.Add(("foo", "bar"));
            var result = await executor.ExecuteGetAsync("request-header", null, headers);
            Assert.Equal(200, result.Result);
        }

        [Fact]
        public async Task ListHeaders()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IExecutorAsync)) as IExecutorAsync;
            var headers = new List<(string Name, string Value)>();
            headers.Add(("foo1", "bar1"));
            headers.Add(("foo2", "bar2"));
            var result = await executor.ExecuteGetAsync("list-headers", null, headers);
            Assert.Equal(200, result.Result);
            var content = result.Content as JContainer;
            Assert.Equal(2, content.Count());
            Assert.Equal("bar1", content["foo1"].Value<string>());
            Assert.Equal("bar2", content["foo2"].Value<string>());
        }

        [Fact]
        public async Task Get_Throws()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IExecutorAsync)) as IExecutorAsync;
            await Assert.ThrowsAsync<HyperlambdaException>(async () => await executor.ExecuteGetAsync("throws", null, new List<(string, string)>()));
        }

        [Fact]
        public async Task SimpleGetStringValue()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IExecutorAsync)) as IExecutorAsync;
            var result = await executor.ExecuteGetAsync("foo-2", null, new List<(string, string)>());
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

            // Notice, GET will convert its arguments.
            var input = new List<(string, string)>();
            input.Add(("input1", "foo"));
            input.Add(("input2", "5"));
            input.Add(("input3", "true"));
            var result = await executor.ExecuteGetAsync("echo", input, new List<(string, string)>());
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

            // Notice, GET will convert its arguments.
            var input = new List<(string, string)>();
            input.Add(("input1", "foo"));
            var result = await executor.ExecuteGetAsync("echo", input, new List<(string, string)>());
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

            // Notice, GET will convert its arguments.
            var input = new List<(string, string)>();
            input.Add(("inputXXX", "foo"));
            await Assert.ThrowsAsync<ArgumentException>(async () => await executor.ExecuteGetAsync("echo", input, new List<(string, string)>()));
        }

        [Fact]
        public async Task GetBadInputSameArgumentTwice_Throws()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IExecutorAsync)) as IExecutorAsync;

            // Notice, GET will convert its arguments.
            var input = new List<(string, string)>();
            input.Add(("input1", "foo1"));
            input.Add(("input1", "foo2"));
            await Assert.ThrowsAsync<ArgumentException>(async () => await executor.ExecuteGetAsync("echo", input, new List<(string, string)>()));
        }

        [Fact]
        public async Task GetArgumentNoDeclaration()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IExecutorAsync)) as IExecutorAsync;

            // Notice, GET will convert its arguments.
            var input = new List<(string, string)>();
            input.Add(("inputXXX", "foo"));
            var result = await executor.ExecuteGetAsync("echo-no-declaration", input, new List<(string, string)>());

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
            var result = await executor.ExecuteGetAsync("status", null, new List<(string, string)>());
            Assert.Equal(201, result.Result);
        }

        [Fact]
        public async Task GetHttpHeader()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IExecutorAsync)) as IExecutorAsync;
            var result = await executor.ExecuteGetAsync("header", null, new List<(string, string)>());
            Assert.Single(result.Headers);
            Assert.Equal("bar", result.Headers["foo"]);
        }

        [Fact]
        public async Task SimpleDelete()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IExecutorAsync)) as IExecutorAsync;
            var result = await executor.ExecuteDeleteAsync("foo-1", null, new List<(string, string)>());
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
            var input = new JObject
            {
                ["input1"] = "foo",
                ["input2"] = 5,
                ["input3"] = true,
                ["input4"] = new JArray
                {
                    new JObject
                    {
                        ["arr1"] = true,
                        ["arr2"] = "57", // Conversion should occur!
                        ["arr3"] = "any-object", // Any object tolerated
                    },
                    new JObject
                    {
                        ["arr1"] = false,
                        ["arr2"] = 67,
                        ["arr3"] = Guid.NewGuid(), // Any object tolerated
                    },
                },
                ["input5"] = new JObject
                {
                    ["obj1"] = "foo",
                    ["obj2"] = "true", // Conversion should occur!
                },
            };
            var result = await executor.ExecutePostAsync("echo", null, input, new List<(string, string)>());
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
            var input = new JObject
            {
                ["input1"] = "foo",
                ["input2"] = 5,
                ["input3"] = true,
                ["input4"] = new JArray
                {
                    new JObject
                    {
                        ["arr1"] = true,
                        ["arr2"] = "57", // Conversion should occur!
                        ["arr3"] = "any-object", // Any object tolerated
                    },
                    new JObject
                    {
                        ["arr1"] = false,
                        ["arr2"] = 67,
                        ["arr3"] = Guid.NewGuid(), // Any object tolerated
                    },
                },
                ["input5"] = new JObject
                {
                    ["obj1"] = "foo",
                    ["obj2"] = "true", // Conversion should occur!
                },
            };
            var result = await executor.ExecutePutAsync("echo", null, input, new List<(string, string)>());
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
            var input = new JObject
            {
                ["input1"] = "foo",
                ["input2"] = 5,
            };
            var result = await executor.ExecutePostAsync("echo", null, input, new List<(string, string)>());
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
