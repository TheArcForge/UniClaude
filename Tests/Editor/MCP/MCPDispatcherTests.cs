using System.Linq;
using NUnit.Framework;
using UniClaude.Editor.MCP;
using Newtonsoft.Json.Linq;

namespace UniClaude.Editor.Tests.MCP
{
    public class MCPDispatcherTests
    {
        MCPDispatcher _dispatcher;

        [SetUp]
        public void SetUp()
        {
            _dispatcher = new MCPDispatcher();
        }

        [Test]
        public void HandleRequest_Initialize_ReturnsServerInfo()
        {
            var request = MakeRequest("initialize", 1);
            var response = JObject.Parse(_dispatcher.HandleRequest(request));
            Assert.AreEqual("2.0", response["jsonrpc"].ToString());
            Assert.AreEqual("uniclaude", response["result"]["serverInfo"]["name"].ToString());
        }

        [Test]
        public void HandleRequest_ToolsList_ReturnsTools()
        {
            var request = MakeRequest("tools/list", 2);
            var response = JObject.Parse(_dispatcher.HandleRequest(request));
            var tools = response["result"]["tools"] as JArray;
            Assert.IsNotNull(tools);
        }

        [Test]
        public void HandleRequest_InvalidJson_ReturnsParseError()
        {
            var response = JObject.Parse(_dispatcher.HandleRequest("{not json"));
            Assert.AreEqual(-32700, (int)response["error"]["code"]);
        }

        [Test]
        public void HandleRequest_UnknownMethod_ReturnsMethodNotFound()
        {
            var request = MakeRequest("nonexistent/method", 3);
            var response = JObject.Parse(_dispatcher.HandleRequest(request));
            Assert.AreEqual(-32601, (int)response["error"]["code"]);
        }

        [Test]
        public void HandleRequest_Notification_ReturnsNull()
        {
            var result = _dispatcher.HandleRequest("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}");
            Assert.IsNull(result);
        }

        [Test]
        public void HandleRequest_ToolsCall_UnknownTool_ReturnsError()
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 4,
                ["method"] = "tools/call",
                ["params"] = new JObject
                {
                    ["name"] = "nonexistent_tool",
                    ["arguments"] = new JObject()
                }
            }.ToString();

            var response = JObject.Parse(_dispatcher.HandleRequest(request));
            var isError = response["result"]?["isError"]?.Value<bool>();
            Assert.IsTrue(isError == true);
        }

        [Test]
        public void HandleRequest_MissingMethod_ReturnsInvalidRequest()
        {
            var response = JObject.Parse(_dispatcher.HandleRequest("{\"jsonrpc\":\"2.0\",\"id\":5}"));
            Assert.AreEqual(-32600, (int)response["error"]["code"]);
        }

        [Test]
        public void GetTools_ReturnsNonNullList()
        {
            var tools = _dispatcher.GetTools();
            Assert.IsNotNull(tools);
        }

        [Test]
        public void HandleRequest_ToolsList_ReturnsExactlyThreeTools()
        {
            var request = MakeRequest("tools/list", 10);
            var response = JObject.Parse(_dispatcher.HandleRequest(request));
            var tools = response["result"]["tools"] as JArray;
            Assert.AreEqual(3, tools.Count);
        }

        [Test]
        public void HandleRequest_ToolsList_ContainsSearchTool()
        {
            var request = MakeRequest("tools/list", 11);
            var response = JObject.Parse(_dispatcher.HandleRequest(request));
            var tools = response["result"]["tools"] as JArray;
            var names = tools.Select(t => t["name"].ToString()).ToList();
            Assert.Contains("search_unity_tools", names);
        }

        [Test]
        public void HandleRequest_ToolsList_ContainsCallTool()
        {
            var request = MakeRequest("tools/list", 12);
            var response = JObject.Parse(_dispatcher.HandleRequest(request));
            var tools = response["result"]["tools"] as JArray;
            var names = tools.Select(t => t["name"].ToString()).ToList();
            Assert.Contains("call_unity_tool", names);
        }

        [Test]
        public void HandleRequest_ToolsList_ContainsProjectSearch()
        {
            var request = MakeRequest("tools/list", 13);
            var response = JObject.Parse(_dispatcher.HandleRequest(request));
            var tools = response["result"]["tools"] as JArray;
            var names = tools.Select(t => t["name"].ToString()).ToList();
            Assert.Contains("project_search", names);
        }

        [Test]
        public void SearchTools_MaterialQuery_ReturnsMatches()
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 20,
                ["method"] = "tools/call",
                ["params"] = new JObject
                {
                    ["name"] = "search_unity_tools",
                    ["arguments"] = new JObject { ["query"] = "material shader" }
                }
            }.ToString();

            var response = JObject.Parse(_dispatcher.HandleRequest(request));
            var content = response["result"]["content"][0]["text"].ToString();
            var results = JArray.Parse(content);
            Assert.IsTrue(results.Count > 0);
            foreach (var r in results)
            {
                var name = r["name"].ToString().ToLower();
                var desc = r["description"].ToString().ToLower();
                Assert.IsTrue(name.Contains("material") || name.Contains("shader") ||
                              desc.Contains("material") || desc.Contains("shader"),
                    $"Result '{r["name"]}' doesn't match query");
            }
        }

        [Test]
        public void SearchTools_ReturnsInputSchemaForEachResult()
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 21,
                ["method"] = "tools/call",
                ["params"] = new JObject
                {
                    ["name"] = "search_unity_tools",
                    ["arguments"] = new JObject { ["query"] = "prefab create" }
                }
            }.ToString();

            var response = JObject.Parse(_dispatcher.HandleRequest(request));
            var results = JArray.Parse(response["result"]["content"][0]["text"].ToString());
            Assert.IsTrue(results.Count > 0);
            foreach (var r in results)
            {
                Assert.IsNotNull(r["inputSchema"], $"Missing inputSchema for '{r["name"]}'");
                Assert.AreEqual("object", r["inputSchema"]["type"].ToString());
            }
        }

        [Test]
        public void SearchTools_NoMatches_ReturnsCategoryHint()
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 22,
                ["method"] = "tools/call",
                ["params"] = new JObject
                {
                    ["name"] = "search_unity_tools",
                    ["arguments"] = new JObject { ["query"] = "zzzznonexistent" }
                }
            }.ToString();

            var response = JObject.Parse(_dispatcher.HandleRequest(request));
            var content = response["result"]["content"][0]["text"].ToString();
            Assert.IsTrue(content.Contains("categories"));
            Assert.IsTrue(content.Contains("Scene"));
        }

        [Test]
        public void SearchTools_EmptyQuery_ReturnsError()
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 23,
                ["method"] = "tools/call",
                ["params"] = new JObject
                {
                    ["name"] = "search_unity_tools",
                    ["arguments"] = new JObject { ["query"] = "" }
                }
            }.ToString();

            var response = JObject.Parse(_dispatcher.HandleRequest(request));
            Assert.IsTrue(response["result"]["isError"].Value<bool>());
        }

        [Test]
        public void SearchTools_ResultsCappedAtTen()
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 24,
                ["method"] = "tools/call",
                ["params"] = new JObject
                {
                    ["name"] = "search_unity_tools",
                    ["arguments"] = new JObject { ["query"] = "get set" }
                }
            }.ToString();

            var response = JObject.Parse(_dispatcher.HandleRequest(request));
            var results = JArray.Parse(response["result"]["content"][0]["text"].ToString());
            Assert.LessOrEqual(results.Count, 10);
        }

        [Test]
        public void CallUnityTool_ValidTool_DispatchesToHandler()
        {
            // project_get_console_log is a safe tool that returns results without side effects
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 30,
                ["method"] = "tools/call",
                ["params"] = new JObject
                {
                    ["name"] = "call_unity_tool",
                    ["arguments"] = new JObject
                    {
                        ["tool"] = "project_get_console_log",
                        ["input"] = "{}"
                    }
                }
            }.ToString();

            var response = JObject.Parse(_dispatcher.HandleRequest(request));
            var isError = response["result"]?["isError"]?.Value<bool>() ?? false;
            Assert.IsFalse(isError, "Expected success but got error");
        }

        [Test]
        public void CallUnityTool_UnknownTool_ReturnsHelpfulError()
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 31,
                ["method"] = "tools/call",
                ["params"] = new JObject
                {
                    ["name"] = "call_unity_tool",
                    ["arguments"] = new JObject
                    {
                        ["tool"] = "nonexistent_tool_xyz",
                        ["input"] = "{}"
                    }
                }
            }.ToString();

            var response = JObject.Parse(_dispatcher.HandleRequest(request));
            Assert.IsTrue(response["result"]["isError"].Value<bool>());
            var text = response["result"]["content"][0]["text"].ToString();
            Assert.IsTrue(text.Contains("nonexistent_tool_xyz"));
            Assert.IsTrue(text.Contains("search_unity_tools"));
        }

        [Test]
        public void CallUnityTool_InvalidJson_ReturnsError()
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 32,
                ["method"] = "tools/call",
                ["params"] = new JObject
                {
                    ["name"] = "call_unity_tool",
                    ["arguments"] = new JObject
                    {
                        ["tool"] = "project_get_console_log",
                        ["input"] = "not valid json {{"
                    }
                }
            }.ToString();

            var response = JObject.Parse(_dispatcher.HandleRequest(request));
            Assert.IsTrue(response["result"]["isError"].Value<bool>());
        }

        [Test]
        public void CallUnityTool_InputAsJObject_AlsoWorks()
        {
            // SDK might send input as an already-parsed object instead of string
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 33,
                ["method"] = "tools/call",
                ["params"] = new JObject
                {
                    ["name"] = "call_unity_tool",
                    ["arguments"] = new JObject
                    {
                        ["tool"] = "project_get_console_log",
                        ["input"] = new JObject()
                    }
                }
            }.ToString();

            var response = JObject.Parse(_dispatcher.HandleRequest(request));
            var isError = response["result"]?["isError"]?.Value<bool>() ?? false;
            Assert.IsFalse(isError, "Expected success but got error");
        }

        [Test]
        public void CallUnityTool_MissingToolParam_ReturnsError()
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 34,
                ["method"] = "tools/call",
                ["params"] = new JObject
                {
                    ["name"] = "call_unity_tool",
                    ["arguments"] = new JObject
                    {
                        ["input"] = "{}"
                    }
                }
            }.ToString();

            var response = JObject.Parse(_dispatcher.HandleRequest(request));
            Assert.IsTrue(response["result"]["isError"].Value<bool>());
        }

        [Test]
        public void CallUnityTool_MissingInputParam_TreatsAsEmptyObject()
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 35,
                ["method"] = "tools/call",
                ["params"] = new JObject
                {
                    ["name"] = "call_unity_tool",
                    ["arguments"] = new JObject
                    {
                        ["tool"] = "project_get_console_log"
                    }
                }
            }.ToString();

            var response = JObject.Parse(_dispatcher.HandleRequest(request));
            var isError = response["result"]?["isError"]?.Value<bool>() ?? false;
            Assert.IsFalse(isError, "Expected success when input param is absent");
        }

        [Test]
        public void CallUnityTool_CannotCallMetaTools()
        {
            var metaTools = new[] { "search_unity_tools", "call_unity_tool", "project_search" };
            foreach (var metaTool in metaTools)
            {
                var request = new JObject
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = 36,
                    ["method"] = "tools/call",
                    ["params"] = new JObject
                    {
                        ["name"] = "call_unity_tool",
                        ["arguments"] = new JObject
                        {
                            ["tool"] = metaTool,
                            ["input"] = "{}"
                        }
                    }
                }.ToString();

                var response = JObject.Parse(_dispatcher.HandleRequest(request));
                Assert.IsTrue(response["result"]["isError"].Value<bool>(),
                    $"Expected error when calling meta-tool '{metaTool}' through call_unity_tool");
            }
        }

        static string MakeRequest(string method, int id)
        {
            return new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["method"] = method
            }.ToString();
        }
    }
}
