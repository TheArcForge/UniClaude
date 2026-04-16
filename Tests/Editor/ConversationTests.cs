using NUnit.Framework;
using UniClaude.Editor;

namespace UniClaude.Editor.Tests
{
    [TestFixture]
    public class ConversationTests
    {
        [Test]
        public void Constructor_GeneratesId()
        {
            var conv = new Conversation();
            Assert.IsNotNull(conv.Id);
            Assert.IsNotEmpty(conv.Id);
        }

        [Test]
        public void Constructor_DefaultTitle()
        {
            var conv = new Conversation();
            Assert.AreEqual("New Chat", conv.Title);
        }

        [Test]
        public void Constructor_EmptyMessages()
        {
            var conv = new Conversation();
            Assert.IsNotNull(conv.Messages);
            Assert.AreEqual(0, conv.Messages.Count);
        }

        [Test]
        public void Constructor_TwoConversations_DifferentIds()
        {
            var a = new Conversation();
            var b = new Conversation();
            Assert.AreNotEqual(a.Id, b.Id);
        }

        [Test]
        public void AddMessage_AppendsToList()
        {
            var conv = new Conversation();
            conv.AddMessage(new ChatMessage(MessageRole.User, "hello"));

            Assert.AreEqual(1, conv.Messages.Count);
            Assert.AreEqual("hello", conv.Messages[0].Content);
            Assert.AreEqual(MessageRole.User, conv.Messages[0].Role);
        }

        [Test]
        public void AddMessage_UpdatesTimestamp()
        {
            var conv = new Conversation();
            var before = conv.UpdatedAt;
            conv.AddMessage(new ChatMessage(MessageRole.User, "test"));

            Assert.AreNotEqual(before, conv.UpdatedAt);
        }

        [Test]
        public void AddMessage_FirstUserMessage_SetsTitle()
        {
            var conv = new Conversation();
            conv.AddMessage(new ChatMessage(MessageRole.User, "Create a player controller"));

            Assert.AreEqual("Create a player controller", conv.Title);
        }

        [Test]
        public void AddMessage_LongFirstMessage_TruncatesTitle()
        {
            var conv = new Conversation();
            var longMsg = new string('x', 100);
            conv.AddMessage(new ChatMessage(MessageRole.User, longMsg));

            Assert.IsTrue(conv.Title.Length <= 60);
            Assert.IsTrue(conv.Title.EndsWith("..."));
        }

        [Test]
        public void AddMessage_SystemMessage_DoesNotSetTitle()
        {
            var conv = new Conversation();
            conv.AddMessage(new ChatMessage(MessageRole.System, "CLI detected"));

            Assert.AreEqual("New Chat", conv.Title);
        }

        [Test]
        public void AddMessage_SecondUserMessage_DoesNotChangeTitle()
        {
            var conv = new Conversation();
            conv.AddMessage(new ChatMessage(MessageRole.User, "First message"));
            conv.AddMessage(new ChatMessage(MessageRole.User, "Second message"));

            Assert.AreEqual("First message", conv.Title);
        }
    }
}
