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
        public void HandleRequest_ToolsList_ContainsProjectSearch()
        {
            var request = MakeRequest("tools/list", 13);
            var response = JObject.Parse(_dispatcher.HandleRequest(request));
            var tools = response["result"]["tools"] as JArray;
            var names = tools.Select(t => t["name"].ToString()).ToList();
            Assert.Contains("project_search", names);
        }

        [Test]
        public void HandleRequest_ToolsList_ReturnsAllDiscoveredTools()
        {
            var request = MakeRequest("tools/list", 10);
            var response = JObject.Parse(_dispatcher.HandleRequest(request));
            var tools = response["result"]["tools"] as JArray;
            // Should return all discovered tools (significantly more than the old 3)
            Assert.Greater(tools.Count, 3);
        }

        [Test]
        public void HandleRequest_ToolsList_EachToolHasSchema()
        {
            var request = MakeRequest("tools/list", 11);
            var response = JObject.Parse(_dispatcher.HandleRequest(request));
            var tools = response["result"]["tools"] as JArray;
            foreach (var tool in tools)
            {
                Assert.IsNotNull(tool["name"], "Tool missing name");
                Assert.IsNotNull(tool["description"], "Tool missing description");
                Assert.IsNotNull(tool["inputSchema"], $"Tool '{tool["name"]}' missing inputSchema");
                Assert.AreEqual("object", tool["inputSchema"]["type"]?.ToString(),
                    $"Tool '{tool["name"]}' inputSchema type should be 'object'");
            }
        }

        [Test]
        public void HandleRequest_ToolsCall_DirectDispatch_Works()
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 30,
                ["method"] = "tools/call",
                ["params"] = new JObject
                {
                    ["name"] = "project_get_console_log",
                    ["arguments"] = new JObject()
                }
            }.ToString();

            var response = JObject.Parse(_dispatcher.HandleRequest(request));
            var isError = response["result"]?["isError"]?.Value<bool>() ?? false;
            Assert.IsFalse(isError, "Expected success but got error");
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
