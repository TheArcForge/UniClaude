using System.IO;
using NUnit.Framework;
using UniClaude.Editor;

namespace UniClaude.Editor.Tests
{
    [TestFixture]
    public class ConversationStoreTests
    {
        string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "uniclaude_test_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            ConversationStore.BaseDir = _tempDir;
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
            ConversationStore.ResetBaseDir();
        }

        [Test]
        public void SaveAndLoad_RoundTrip_PreservesAllFields()
        {
            var conv = new Conversation();
            conv.AddMessage(new ChatMessage(MessageRole.User, "Hello"));
            conv.AddMessage(new ChatMessage(MessageRole.Assistant, "Hi there"));
            conv.AddMessage(new ChatMessage(MessageRole.System, "Session started"));

            ConversationStore.Save(conv);
            var loaded = ConversationStore.Load(conv.Id);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(conv.Id, loaded.Id);
            Assert.AreEqual(conv.Title, loaded.Title);
            Assert.AreEqual(conv.CreatedAt, loaded.CreatedAt);
            Assert.AreEqual(conv.UpdatedAt, loaded.UpdatedAt);
            Assert.AreEqual(3, loaded.Messages.Count);
            Assert.AreEqual(MessageRole.User, loaded.Messages[0].Role);
            Assert.AreEqual("Hello", loaded.Messages[0].Content);
            Assert.AreEqual(MessageRole.Assistant, loaded.Messages[1].Role);
            Assert.AreEqual("Hi there", loaded.Messages[1].Content);
            Assert.AreEqual(MessageRole.System, loaded.Messages[2].Role);
            Assert.AreEqual("Session started", loaded.Messages[2].Content);
        }

        [Test]
        public void Load_NonexistentId_ReturnsNull()
        {
            var result = ConversationStore.Load("does_not_exist");
            Assert.IsNull(result);
        }

        [Test]
        public void Save_UpdatesIndex()
        {
            var conv = new Conversation();
            conv.AddMessage(new ChatMessage(MessageRole.User, "Test message"));

            ConversationStore.Save(conv);

            var index = ConversationStore.LoadIndex();
            Assert.AreEqual(1, index.Count);
            Assert.AreEqual(conv.Id, index[0].Id);
            Assert.AreEqual(conv.Title, index[0].Title);
            Assert.AreEqual(conv.CreatedAt, index[0].CreatedAt);
            Assert.AreEqual(conv.UpdatedAt, index[0].UpdatedAt);
            Assert.AreEqual(1, index[0].MessageCount);
        }

        [Test]
        public void Delete_RemovesFileAndIndexEntry()
        {
            var conv = new Conversation();
            conv.AddMessage(new ChatMessage(MessageRole.User, "To be deleted"));

            ConversationStore.Save(conv);
            ConversationStore.Delete(conv.Id);

            Assert.IsNull(ConversationStore.Load(conv.Id));
            var index = ConversationStore.LoadIndex();
            Assert.AreEqual(0, index.Count);
        }

        [Test]
        public void DeleteAll_ClearsEverything()
        {
            var conv1 = new Conversation();
            conv1.AddMessage(new ChatMessage(MessageRole.User, "First"));
            var conv2 = new Conversation();
            conv2.AddMessage(new ChatMessage(MessageRole.User, "Second"));

            ConversationStore.Save(conv1);
            ConversationStore.Save(conv2);
            ConversationStore.DeleteAll();

            Assert.IsNull(ConversationStore.Load(conv1.Id));
            Assert.IsNull(ConversationStore.Load(conv2.Id));
            var index = ConversationStore.LoadIndex();
            Assert.AreEqual(0, index.Count);
        }

        [Test]
        public void RebuildIndex_RegeneratesFromFiles()
        {
            var conv = new Conversation();
            conv.AddMessage(new ChatMessage(MessageRole.User, "Rebuild test"));

            ConversationStore.Save(conv);

            var indexPath = Path.Combine(_tempDir, "index.json");
            File.Delete(indexPath);

            ConversationStore.RebuildIndex();

            var index = ConversationStore.LoadIndex();
            Assert.AreEqual(1, index.Count);
            Assert.AreEqual(conv.Id, index[0].Id);
        }

        [Test]
        public void GetCacheStats_ReturnsCorrectCountAndSize()
        {
            var conv = new Conversation();
            conv.AddMessage(new ChatMessage(MessageRole.User, "Stats test"));

            ConversationStore.Save(conv);

            var (count, bytes) = ConversationStore.GetCacheStats();
            Assert.AreEqual(1, count);
            Assert.Greater(bytes, 0);
        }

        [Test]
        public void LoadIndex_CorruptIndex_RebuildsSilently()
        {
            var conv = new Conversation();
            conv.AddMessage(new ChatMessage(MessageRole.User, "Corrupt test"));

            ConversationStore.Save(conv);

            var indexPath = Path.Combine(_tempDir, "index.json");
            File.WriteAllText(indexPath, "THIS IS NOT VALID JSON!!!");

            var index = ConversationStore.LoadIndex();
            Assert.AreEqual(1, index.Count);
            Assert.AreEqual(conv.Id, index[0].Id);
        }

        [Test]
        public void MessageRole_SerializesAsString()
        {
            var conv = new Conversation();
            conv.AddMessage(new ChatMessage(MessageRole.User, "Enum test"));

            ConversationStore.Save(conv);

            var filePath = Path.Combine(_tempDir, conv.Id + ".json");
            var rawJson = File.ReadAllText(filePath);

            Assert.IsTrue(rawJson.Contains("\"User\""), "Expected MessageRole serialized as string 'User'");
            Assert.IsFalse(rawJson.Contains("\"Role\": 0"), "MessageRole should not serialize as integer");
        }

        [Test]
        public void ConversationSummary_From_ProducesCorrectSummary()
        {
            var conv = new Conversation();
            conv.AddMessage(new ChatMessage(MessageRole.User, "Summary test"));
            conv.AddMessage(new ChatMessage(MessageRole.Assistant, "Response"));

            var summary = ConversationSummary.From(conv);

            Assert.AreEqual(conv.Id, summary.Id);
            Assert.AreEqual(conv.Title, summary.Title);
            Assert.AreEqual(conv.CreatedAt, summary.CreatedAt);
            Assert.AreEqual(conv.UpdatedAt, summary.UpdatedAt);
            Assert.AreEqual(2, summary.MessageCount);
        }

        [Test]
        public void LoadIndex_SortedByUpdatedAtDescending()
        {
            var older = new Conversation();
            older.AddMessage(new ChatMessage(MessageRole.User, "Older conversation"));
            older.UpdatedAt = "2025-01-01T00:00:00.0000000Z";

            var newer = new Conversation();
            newer.AddMessage(new ChatMessage(MessageRole.User, "Newer conversation"));
            newer.UpdatedAt = "2025-01-02T00:00:00.0000000Z";

            ConversationStore.Save(older);
            ConversationStore.Save(newer);

            var index = ConversationStore.LoadIndex();
            Assert.AreEqual(2, index.Count);
            Assert.AreEqual(newer.Id, index[0].Id, "Newer conversation should be first");
            Assert.AreEqual(older.Id, index[1].Id, "Older conversation should be second");
        }

        [Test]
        public void SaveAndLoad_RoundTrip_PreservesToolCallEntries()
        {
            var conv = new Conversation();
            conv.AddMessage(new ChatMessage(MessageRole.User, "Do something"));

            var toolMsg = new ChatMessage(MessageRole.ToolCall, "Component added successfully")
            {
                ToolName = "component_add",
                IsError = false
            };
            conv.AddMessage(toolMsg);

            var errorToolMsg = new ChatMessage(MessageRole.ToolCall, "Property not found")
            {
                ToolName = "component_set_property",
                IsError = true
            };
            conv.AddMessage(errorToolMsg);

            conv.AddMessage(new ChatMessage(MessageRole.ProjectContext, "using ArcForge.UI;\nusing UnityEngine;"));
            conv.AddMessage(new ChatMessage(MessageRole.Info, "Locked domain reload (auto)"));

            var usageMsg = new ChatMessage(MessageRole.TokenUsage, "")
            {
                InputTokens = 2500,
                OutputTokens = 1200
            };
            conv.AddMessage(usageMsg);

            conv.AddMessage(new ChatMessage(MessageRole.Assistant, "Done!"));

            ConversationStore.Save(conv);
            var loaded = ConversationStore.Load(conv.Id);

            Assert.AreEqual(7, loaded.Messages.Count);

            // Tool call (success)
            Assert.AreEqual(MessageRole.ToolCall, loaded.Messages[1].Role);
            Assert.AreEqual("component_add", loaded.Messages[1].ToolName);
            Assert.AreEqual("Component added successfully", loaded.Messages[1].Content);
            Assert.IsFalse(loaded.Messages[1].IsError);

            // Tool call (error)
            Assert.AreEqual(MessageRole.ToolCall, loaded.Messages[2].Role);
            Assert.AreEqual("component_set_property", loaded.Messages[2].ToolName);
            Assert.IsTrue(loaded.Messages[2].IsError);

            // Project context
            Assert.AreEqual(MessageRole.ProjectContext, loaded.Messages[3].Role);
            Assert.AreEqual("using ArcForge.UI;\nusing UnityEngine;", loaded.Messages[3].Content);

            // Info
            Assert.AreEqual(MessageRole.Info, loaded.Messages[4].Role);
            Assert.AreEqual("Locked domain reload (auto)", loaded.Messages[4].Content);

            // Token usage
            Assert.AreEqual(MessageRole.TokenUsage, loaded.Messages[5].Role);
            Assert.AreEqual(2500, loaded.Messages[5].InputTokens);
            Assert.AreEqual(1200, loaded.Messages[5].OutputTokens);

            // Original assistant message still intact
            Assert.AreEqual(MessageRole.Assistant, loaded.Messages[6].Role);
            Assert.AreEqual("Done!", loaded.Messages[6].Content);
        }

        [Test]
        public void Save_OverwriteExisting_Succeeds()
        {
            var conv = new Conversation();
            conv.AddMessage(new ChatMessage(MessageRole.User, "Original"));

            ConversationStore.Save(conv);

            conv.AddMessage(new ChatMessage(MessageRole.Assistant, "Reply"));
            ConversationStore.Save(conv);

            var loaded = ConversationStore.Load(conv.Id);
            Assert.AreEqual(2, loaded.Messages.Count);
            Assert.AreEqual("Reply", loaded.Messages[1].Content);

            var index = ConversationStore.LoadIndex();
            Assert.AreEqual(1, index.Count, "Index should still have exactly one entry");
            Assert.AreEqual(2, index[0].MessageCount);
        }

        [Test]
        public void SaveAndLoad_BackwardsCompatible_OldRolesStillWork()
        {
            // Simulate an old save file with only User/Assistant/System
            var conv = new Conversation();
            conv.AddMessage(new ChatMessage(MessageRole.User, "old msg"));
            conv.AddMessage(new ChatMessage(MessageRole.Assistant, "old reply"));

            ConversationStore.Save(conv);
            var loaded = ConversationStore.Load(conv.Id);

            Assert.AreEqual(2, loaded.Messages.Count);
            Assert.AreEqual(MessageRole.User, loaded.Messages[0].Role);
            Assert.AreEqual(MessageRole.Assistant, loaded.Messages[1].Role);
            // New fields should be at defaults
            Assert.IsNull(loaded.Messages[0].ToolName);
            Assert.IsFalse(loaded.Messages[0].IsError);
            Assert.AreEqual(0, loaded.Messages[0].InputTokens);
        }
    }
}
