using NUnit.Framework;
using UniClaude.Editor;

namespace UniClaude.Editor.Tests
{
    [TestFixture]
    public class ChatMessageTests
    {
        [Test]
        public void Constructor_SetsRole()
        {
            var msg = new ChatMessage(MessageRole.User, "hello");
            Assert.AreEqual(MessageRole.User, msg.Role);
        }

        [Test]
        public void Constructor_SetsContent()
        {
            var msg = new ChatMessage(MessageRole.Assistant, "response");
            Assert.AreEqual("response", msg.Content);
        }

        [Test]
        public void Constructor_SetsTimestamp()
        {
            var msg = new ChatMessage(MessageRole.User, "test");
            Assert.IsNotNull(msg.Timestamp);
            Assert.IsNotEmpty(msg.Timestamp);
        }

        [Test]
        public void DefaultConstructor_WorksForSerialization()
        {
            var msg = new ChatMessage();
            Assert.IsNull(msg.Content);
            Assert.IsNull(msg.Timestamp);
        }

        [Test]
        public void Constructor_AcceptsToolCallRole()
        {
            var msg = new ChatMessage(MessageRole.ToolCall, "result text");
            Assert.AreEqual(MessageRole.ToolCall, msg.Role);
            Assert.AreEqual("result text", msg.Content);
        }

        [Test]
        public void Constructor_AcceptsProjectContextRole()
        {
            var msg = new ChatMessage(MessageRole.ProjectContext, "context");
            Assert.AreEqual(MessageRole.ProjectContext, msg.Role);
        }

        [Test]
        public void Constructor_AcceptsInfoRole()
        {
            var msg = new ChatMessage(MessageRole.Info, "log message");
            Assert.AreEqual(MessageRole.Info, msg.Role);
        }

        [Test]
        public void Constructor_AcceptsTokenUsageRole()
        {
            var msg = new ChatMessage(MessageRole.TokenUsage, "");
            Assert.AreEqual(MessageRole.TokenUsage, msg.Role);
        }

        [Test]
        public void ToolCall_FieldsDefaultToNullAndFalse()
        {
            var msg = new ChatMessage(MessageRole.ToolCall, "result");
            Assert.IsNull(msg.ToolName);
            Assert.IsFalse(msg.IsError);
        }

        [Test]
        public void ToolCall_CanSetOptionalFields()
        {
            var msg = new ChatMessage(MessageRole.ToolCall, "result")
            {
                ToolName = "component_add",
                IsError = true
            };
            Assert.AreEqual("component_add", msg.ToolName);
            Assert.IsTrue(msg.IsError);
        }

        [Test]
        public void TokenUsage_FieldsDefaultToZero()
        {
            var msg = new ChatMessage(MessageRole.TokenUsage, "");
            Assert.AreEqual(0, msg.InputTokens);
            Assert.AreEqual(0, msg.OutputTokens);
        }

        [Test]
        public void TokenUsage_CanSetTokenCounts()
        {
            var msg = new ChatMessage(MessageRole.TokenUsage, "")
            {
                InputTokens = 1500,
                OutputTokens = 800
            };
            Assert.AreEqual(1500, msg.InputTokens);
            Assert.AreEqual(800, msg.OutputTokens);
        }
    }
}
