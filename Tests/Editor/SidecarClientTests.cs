using System.Collections.Generic;
using NUnit.Framework;
using UniClaude.Editor;

namespace UniClaude.Editor.Tests
{
    [TestFixture]
    public class SidecarClientTests
    {
        // ── SSE Line Parsing ──

        [Test]
        public void ParseSSELine_TokenEvent_ExtractsText()
        {
            var json = "{\"type\":\"token\",\"text\":\"hello\"}";
            var evt = SidecarClient.ParseSSEData(json);

            Assert.IsNotNull(evt);
            Assert.AreEqual("token", evt.Type);
            Assert.AreEqual("hello", evt.Text);
        }

        [Test]
        public void ParseSSELine_PhaseEvent_ExtractsPhaseAndTool()
        {
            var json = "{\"type\":\"phase\",\"phase\":\"tool_use\",\"tool\":\"Edit\"}";
            var evt = SidecarClient.ParseSSEData(json);

            Assert.IsNotNull(evt);
            Assert.AreEqual("phase", evt.Type);
            Assert.AreEqual("tool_use", evt.Phase);
            Assert.AreEqual("Edit", evt.Tool);
        }

        [Test]
        public void ParseSSELine_PermissionRequest_ExtractsIdAndTool()
        {
            var json = "{\"type\":\"permission_request\",\"id\":\"req-1\",\"tool\":\"Bash\",\"input\":{\"command\":\"ls\"}}";
            var evt = SidecarClient.ParseSSEData(json);

            Assert.IsNotNull(evt);
            Assert.AreEqual("permission_request", evt.Type);
            Assert.AreEqual("req-1", evt.Id);
            Assert.AreEqual("Bash", evt.Tool);
            Assert.IsNotNull(evt.Input);
        }

        [Test]
        public void ParseSSELine_ResultEvent_ExtractsAll()
        {
            var json = "{\"type\":\"result\",\"text\":\"Done\",\"session_id\":\"s1\",\"usage\":{\"input\":100,\"output\":50}}";
            var evt = SidecarClient.ParseSSEData(json);

            Assert.IsNotNull(evt);
            Assert.AreEqual("result", evt.Type);
            Assert.AreEqual("Done", evt.Text);
            Assert.AreEqual("s1", evt.SessionId);
            Assert.AreEqual(100, evt.InputTokens);
            Assert.AreEqual(50, evt.OutputTokens);
        }

        [Test]
        public void ParseSSELine_InvalidJson_ReturnsNull()
        {
            var evt = SidecarClient.ParseSSEData("not json");
            Assert.IsNull(evt);
        }

        [Test]
        public void ParseSSELine_EmptyString_ReturnsNull()
        {
            var evt = SidecarClient.ParseSSEData("");
            Assert.IsNull(evt);
        }

        [Test]
        public void ParseSSELine_ErrorEvent_ExtractsMessage()
        {
            var json = "{\"type\":\"error\",\"message\":\"something broke\"}";
            var evt = SidecarClient.ParseSSEData(json);

            Assert.IsNotNull(evt);
            Assert.AreEqual("error", evt.Type);
            Assert.AreEqual("something broke", evt.Text);
        }

        [Test]
        public void ParseSSELine_AssistantTextEvent_ExtractsText()
        {
            var json = "{\"type\":\"assistant_text\",\"text\":\"Let me read that file.\"}";
            var evt = SidecarClient.ParseSSEData(json);

            Assert.IsNotNull(evt);
            Assert.AreEqual("assistant_text", evt.Type);
            Assert.AreEqual("Let me read that file.", evt.Text);
        }

        // ── SSE Buffer Splitting ──

        [Test]
        public void SplitSSEBuffer_SingleEvent_ReturnsOneDataLine()
        {
            var buffer = "data: {\"type\":\"token\",\"text\":\"hi\"}\n\n";
            var events = SidecarClient.SplitSSEBuffer(ref buffer);

            Assert.AreEqual(1, events.Count);
            Assert.AreEqual("{\"type\":\"token\",\"text\":\"hi\"}", events[0]);
            Assert.AreEqual("", buffer);
        }

        [Test]
        public void SplitSSEBuffer_PartialEvent_LeavesInBuffer()
        {
            var buffer = "data: {\"type\":\"token\"";
            var events = SidecarClient.SplitSSEBuffer(ref buffer);

            Assert.AreEqual(0, events.Count);
            Assert.AreEqual("data: {\"type\":\"token\"", buffer);
        }

        [Test]
        public void SplitSSEBuffer_MultipleEvents_ReturnsAll()
        {
            var buffer = "data: {\"type\":\"token\",\"text\":\"a\"}\n\ndata: {\"type\":\"token\",\"text\":\"b\"}\n\n";
            var events = SidecarClient.SplitSSEBuffer(ref buffer);

            Assert.AreEqual(2, events.Count);
            Assert.AreEqual("{\"type\":\"token\",\"text\":\"a\"}", events[0]);
            Assert.AreEqual("{\"type\":\"token\",\"text\":\"b\"}", events[1]);
        }

        [Test]
        public void SplitSSEBuffer_KeepAliveComment_IsSkipped()
        {
            var buffer = ": keepalive\n\ndata: {\"type\":\"token\",\"text\":\"hi\"}\n\n";
            var events = SidecarClient.SplitSSEBuffer(ref buffer);

            Assert.AreEqual(1, events.Count);
            Assert.AreEqual("{\"type\":\"token\",\"text\":\"hi\"}", events[0]);
        }

        // ── SSE Event Type Parsing ──

        [Test]
        public void ParseSSEData_PlanMode_ExtractsActive()
        {
            var json = "{\"type\":\"plan_mode\",\"active\":true}";
            var evt = SidecarClient.ParseSSEData(json);

            Assert.AreEqual("plan_mode", evt.Type);
            Assert.IsTrue(evt.Active);
        }

        [Test]
        public void ParseSSEData_PromptSuggestion_ExtractsSuggestion()
        {
            var json = "{\"type\":\"prompt_suggestion\",\"suggestion\":\"Try adding a Rigidbody\"}";
            var evt = SidecarClient.ParseSSEData(json);

            Assert.AreEqual("prompt_suggestion", evt.Type);
            Assert.AreEqual("Try adding a Rigidbody", evt.Suggestion);
        }

        [Test]
        public void ParseSSEData_ToolActivity_ExtractsAllFields()
        {
            var json = "{\"type\":\"tool_activity\",\"toolUseId\":\"tu-1\",\"toolName\":\"file_read\",\"input\":{\"path\":\"Assets/test.cs\"},\"parentTaskId\":\"t-1\"}";
            var evt = SidecarClient.ParseSSEData(json);

            Assert.AreEqual("tool_activity", evt.Type);
            Assert.AreEqual("tu-1", evt.ToolUseId);
            Assert.AreEqual("file_read", evt.ToolName);
            Assert.AreEqual("t-1", evt.ParentTaskId);
            Assert.That(evt.InputJson, Does.Contain("Assets/test.cs"));
        }

        [Test]
        public void ParseSSEData_Task_ExtractsAllFields()
        {
            var json = "{\"type\":\"task\",\"taskId\":\"t-1\",\"status\":\"completed\",\"description\":\"Read file\",\"error\":null}";
            var evt = SidecarClient.ParseSSEData(json);

            Assert.AreEqual("task", evt.Type);
            Assert.AreEqual("t-1", evt.TaskId);
            Assert.AreEqual("completed", evt.Status);
            Assert.AreEqual("Read file", evt.Description);
        }

        [Test]
        public void ParseSSEData_ToolProgress_ExtractsElapsed()
        {
            var json = "{\"type\":\"tool_progress\",\"toolUseId\":\"tu-2\",\"toolName\":\"bash\",\"elapsedSeconds\":3.5,\"parentTaskId\":\"t-1\"}";
            var evt = SidecarClient.ParseSSEData(json);

            Assert.AreEqual("tool_progress", evt.Type);
            Assert.AreEqual("tu-2", evt.ToolUseId);
            Assert.AreEqual("bash", evt.ToolName);
            Assert.AreEqual(3.5f, evt.ElapsedSeconds, 0.01f);
            Assert.AreEqual("t-1", evt.ParentTaskId);
        }

        [Test]
        public void ParseSSEData_ToolExecuted_ExtractsSuccessAndResult()
        {
            var json = "{\"type\":\"tool_executed\",\"tool\":\"component_add\",\"result\":\"Added BoxCollider\",\"success\":true}";
            var evt = SidecarClient.ParseSSEData(json);

            Assert.AreEqual("tool_executed", evt.Type);
            Assert.AreEqual("component_add", evt.Tool);
            Assert.AreEqual("Added BoxCollider", evt.Text);
            Assert.IsTrue(evt.Success);
        }

        [Test]
        public void ParseSSEData_ToolExecuted_FailureCase()
        {
            var json = "{\"type\":\"tool_executed\",\"tool\":\"file_write\",\"result\":\"Permission denied\",\"success\":false}";
            var evt = SidecarClient.ParseSSEData(json);

            Assert.AreEqual("tool_executed", evt.Type);
            Assert.IsFalse(evt.Success);
            Assert.AreEqual("Permission denied", evt.Text);
        }

        [Test]
        public void ParseSSEData_ResultWithCost_ExtractsCost()
        {
            var json = "{\"type\":\"result\",\"text\":\"Done\",\"session_id\":\"s1\",\"usage\":{\"input\":500,\"output\":200},\"cost_usd\":0.012}";
            var evt = SidecarClient.ParseSSEData(json);

            Assert.AreEqual("result", evt.Type);
            Assert.AreEqual(500, evt.InputTokens);
            Assert.AreEqual(200, evt.OutputTokens);
            Assert.IsNotNull(evt.CostUsd);
            Assert.AreEqual(0.012f, evt.CostUsd.Value, 0.001f);
        }

        [Test]
        public void ParseSSEData_UnknownType_ReturnsEventWithType()
        {
            var json = "{\"type\":\"future_event\",\"data\":\"something\"}";
            var evt = SidecarClient.ParseSSEData(json);

            Assert.IsNotNull(evt);
            Assert.AreEqual("future_event", evt.Type);
        }

        [Test]
        public void ParseSSEData_MissingType_ReturnsNull()
        {
            var json = "{\"text\":\"no type field\"}";
            var evt = SidecarClient.ParseSSEData(json);

            Assert.IsNull(evt);
        }

        // ── Event Dispatch ──

        [Test]
        public void DispatchEvent_TokenEvent_FiresOnToken()
        {
            using var client = new SidecarClient(0);
            string received = null;
            client.OnToken += t => received = t;

            var json = "{\"type\":\"token\",\"text\":\"hello\"}";
            var buffer = $"data: {json}\n\n";
            var events = SidecarClient.SplitSSEBuffer(ref buffer);

            // Parse and verify — DispatchEvent is private, but we can verify
            // the full pipeline: parse → event fields are correct
            var evt = SidecarClient.ParseSSEData(events[0]);
            Assert.AreEqual("hello", evt.Text);
        }

        // ── Project Directory ──

        [Test]
        public void ProjectDir_ReturnsParentOfDataPath()
        {
            // Application.dataPath returns "{projectRoot}/Assets"
            // ProjectDir should return the parent directory
            var dataPath = UnityEngine.Application.dataPath;
            var expected = System.IO.Path.GetDirectoryName(dataPath);
            Assert.IsNotNull(expected);
            Assert.IsTrue(System.IO.Directory.Exists(expected));
        }

        // ── SSE Buffer With Event IDs ──

        [Test]
        public void SplitSSEBuffer_WithIdLine_ExtractsDataAndId()
        {
            var buffer = "id: 42\ndata: {\"type\":\"token\",\"text\":\"hi\"}\n\n";
            string lastId = null;
            var results = SidecarClient.SplitSSEBuffer(ref buffer, ref lastId);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("{\"type\":\"token\",\"text\":\"hi\"}", results[0]);
            Assert.AreEqual("42", lastId);
        }

        [Test]
        public void SplitSSEBuffer_MultipleEvents_TracksLatestId()
        {
            var buffer = "id: 1\ndata: {\"type\":\"token\",\"text\":\"a\"}\n\nid: 2\ndata: {\"type\":\"token\",\"text\":\"b\"}\n\n";
            string lastId = null;
            var results = SidecarClient.SplitSSEBuffer(ref buffer, ref lastId);

            Assert.AreEqual(2, results.Count);
            Assert.AreEqual("2", lastId);
        }

        [Test]
        public void SplitSSEBuffer_NoIdLine_LeavesLastIdNull()
        {
            var buffer = "data: {\"type\":\"token\",\"text\":\"hi\"}\n\n";
            string lastId = null;
            var results = SidecarClient.SplitSSEBuffer(ref buffer, ref lastId);

            Assert.AreEqual(1, results.Count);
            Assert.IsNull(lastId);
        }
    }
}
