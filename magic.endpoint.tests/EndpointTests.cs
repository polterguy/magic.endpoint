/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2020, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Threading.Tasks;
using Xunit;
using Newtonsoft.Json.Linq;
using magic.endpoint.contracts;
using System.Collections.Generic;

namespace magic.endpoint.tests
{
    public class EndpointTests
    {
        [Fact]
        public async Task SimpleGet()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IExecutorAsync)) as IExecutorAsync;
            var result = await executor.ExecuteGetAsync("foo-1", null);
            Assert.Equal(200, result.Result);
            Assert.Empty(result.Headers);
            var j = result.Content as JObject;
            Assert.NotNull(j);
            Assert.Equal("hello world", j["result"].Value<string>());
        }

        [Fact]
        public async Task SimpleGetStringValue()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IExecutorAsync)) as IExecutorAsync;
            var result = await executor.ExecuteGetAsync("foo-2", null);
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
            var result = await executor.ExecuteGetAsync("echo", input);
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
            var result = await executor.ExecuteGetAsync("echo", input);
            Assert.Equal(200, result.Result);
            Assert.Empty(result.Headers);
            var j = result.Content as JObject;
            Assert.NotNull(j);
            Assert.Single(j);
            Assert.Equal("foo", j["input1"].Value<string>());
        }

        [Fact]
        public async Task Get_Throws()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IExecutorAsync)) as IExecutorAsync;

            // Notice, GET will convert its arguments.
            var input = new List<(string, string)>();
            input.Add(("inputXXX", "foo"));
            await Assert.ThrowsAsync<ArgumentException>(async () => await executor.ExecuteGetAsync("echo", input));
        }

        [Fact]
        public async Task GetArgumentNoDeclaration()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IExecutorAsync)) as IExecutorAsync;

            // Notice, GET will convert its arguments.
            var input = new List<(string, string)>();
            input.Add(("inputXXX", "foo"));
            var result = await executor.ExecuteGetAsync("echo-no-declaration", input);

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
            var result = await executor.ExecuteGetAsync("status", null);
            Assert.Equal(201, result.Result);
        }

        [Fact]
        public async Task GetHttpHeader()
        {
            var svc = Common.Initialize();
            var executor = svc.GetService(typeof(IExecutorAsync)) as IExecutorAsync;
            var result = await executor.ExecuteGetAsync("header", null);
            Assert.Single(result.Headers);
            Assert.Equal("bar", result.Headers["foo"]);
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
            var result = await executor.ExecutePostAsync("echo", null, input);
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
            var result = await executor.ExecutePostAsync("echo", null, input);
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
