// Tests/Editor/AttachmentManagerTests.cs
using System.IO;
using NUnit.Framework;
using UniClaude.Editor.UI.Input;

namespace UniClaude.Editor.Tests
{
    [TestFixture]
    public class AttachmentManagerTests
    {
        string _tempDir;
        AttachmentManager _manager;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "UniClaudeTestAttachments");
            Directory.CreateDirectory(_tempDir);
            _manager = new AttachmentManager(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            _manager.ClearAll();
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        // ── Whitelist ──

        [Test]
        public void AddFile_CsFile_Accepted()
        {
            var path = CreateTempFile("Test.cs", "class Test {}");
            _manager.AddFile(path);
            Assert.AreEqual(1, _manager.Attachments.Count);
            Assert.AreEqual("Test.cs", _manager.Attachments[0].FileName);
        }

        [Test]
        public void AddFile_PngFile_Accepted()
        {
            var path = CreateTempFile("icon.png", "fakepng");
            _manager.AddFile(path);
            Assert.AreEqual(1, _manager.Attachments.Count);
        }

        [Test]
        public void AddFile_UnityFile_Accepted()
        {
            var path = CreateTempFile("Scene.unity", "fakescene");
            _manager.AddFile(path);
            Assert.AreEqual(1, _manager.Attachments.Count);
        }

        [Test]
        public void AddFile_FbxFile_Rejected()
        {
            var path = CreateTempFile("model.fbx", "fakefbx");
            _manager.AddFile(path);
            Assert.AreEqual(0, _manager.Attachments.Count);
        }

        [Test]
        public void AddFile_WavFile_Rejected()
        {
            var path = CreateTempFile("sound.wav", "fakewav");
            _manager.AddFile(path);
            Assert.AreEqual(0, _manager.Attachments.Count);
        }

        // ── Duplicate Detection ──

        [Test]
        public void AddFile_SamePathTwice_OnlyOneAttachment()
        {
            var path = CreateTempFile("Test.cs", "class Test {}");
            _manager.AddFile(path);
            _manager.AddFile(path);
            Assert.AreEqual(1, _manager.Attachments.Count);
        }

        // ── Size Warning ──

        [Test]
        public void AddFile_SmallFile_NotFlaggedAsLarge()
        {
            var path = CreateTempFile("small.cs", "x");
            _manager.AddFile(path);
            Assert.IsFalse(_manager.Attachments[0].IsLargeFile);
        }

        [Test]
        public void AddFile_LargeFile_FlaggedAsLarge()
        {
            var path = CreateTempFile("big.cs", new string('x', 600_000));
            _manager.AddFile(path);
            Assert.IsTrue(_manager.Attachments[0].IsLargeFile);
        }

        // ── Remove ──

        [Test]
        public void Remove_RemovesFromList()
        {
            var path = CreateTempFile("Test.cs", "class Test {}");
            _manager.AddFile(path);
            _manager.Remove(_manager.Attachments[0]);
            Assert.AreEqual(0, _manager.Attachments.Count);
        }

        // ── ClearAll ──

        [Test]
        public void ClearAll_RemovesAllAttachments()
        {
            _manager.AddFile(CreateTempFile("a.cs", "a"));
            _manager.AddFile(CreateTempFile("b.cs", "b"));
            _manager.ClearAll();
            Assert.AreEqual(0, _manager.Attachments.Count);
        }

        // ── Events ──

        [Test]
        public void AddFile_FiresOnAttachmentsChanged()
        {
            bool fired = false;
            _manager.OnAttachmentsChanged += () => fired = true;
            _manager.AddFile(CreateTempFile("Test.cs", "x"));
            Assert.IsTrue(fired);
        }

        [Test]
        public void Remove_FiresOnAttachmentsChanged()
        {
            _manager.AddFile(CreateTempFile("Test.cs", "x"));
            bool fired = false;
            _manager.OnAttachmentsChanged += () => fired = true;
            _manager.Remove(_manager.Attachments[0]);
            Assert.IsTrue(fired);
        }

        // ── Nonexistent File ──

        [Test]
        public void AddFile_NonexistentPath_Rejected()
        {
            _manager.AddFile("/nonexistent/path/Test.cs");
            Assert.AreEqual(0, _manager.Attachments.Count);
        }

        // ── Helpers ──

        string CreateTempFile(string name, string content)
        {
            var path = Path.Combine(_tempDir, name);
            File.WriteAllText(path, content);
            return path;
        }
    }
}
