using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UniClaude.Editor;

namespace UniClaude.Editor.Tests
{
    [TestFixture]
    public class ProjectIndexStoreTests
    {
        string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "IndexStoreTest_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDir);
            ProjectIndexStore.BaseDir = _tempDir;
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
            ProjectIndexStore.ResetBaseDir();
        }

        [Test]
        public void SaveAndLoad_RoundTrip()
        {
            var index = new ProjectIndex
            {
                ProjectName = "TestProject",
                UnityVersion = "6000.3",
                Entries = new List<IndexEntry>
                {
                    new IndexEntry
                    {
                        AssetPath = "Assets/Scripts/Player.cs",
                        Kind = AssetKind.Script,
                        Name = "Player",
                        Symbols = new[] { "Player", "Move" },
                        Dependencies = new string[0],
                        Summary = "Player : MonoBehaviour",
                        LastModifiedTicks = DateTime.UtcNow.Ticks
                    }
                }
            };

            ProjectIndexStore.Save(index);
            var loaded = ProjectIndexStore.Load();

            Assert.IsNotNull(loaded);
            Assert.AreEqual("TestProject", loaded.ProjectName);
            Assert.AreEqual(1, loaded.Entries.Count);
            Assert.AreEqual("Player", loaded.Entries[0].Name);
            Assert.AreEqual(AssetKind.Script, loaded.Entries[0].Kind);
        }

        [Test]
        public void Load_NoCache_ReturnsNull()
        {
            var loaded = ProjectIndexStore.Load();
            Assert.IsNull(loaded);
        }

        [Test]
        public void Load_CorruptCache_ReturnsNull()
        {
            Directory.CreateDirectory(_tempDir);
            File.WriteAllText(Path.Combine(_tempDir, "index.json"), "NOT VALID JSON{{{");

            var loaded = ProjectIndexStore.Load();
            Assert.IsNull(loaded);
        }

        [Test]
        public void Delete_RemovesCacheFile()
        {
            var index = new ProjectIndex { ProjectName = "Test", Entries = new List<IndexEntry>() };
            ProjectIndexStore.Save(index);

            Assert.IsTrue(File.Exists(Path.Combine(_tempDir, "index.json")));

            ProjectIndexStore.Delete();

            Assert.IsFalse(File.Exists(Path.Combine(_tempDir, "index.json")));
        }

        [Test]
        public void GetCacheStats_ReturnsCorrectSize()
        {
            var index = new ProjectIndex
            {
                ProjectName = "Test",
                Entries = new List<IndexEntry>
                {
                    new IndexEntry
                    {
                        AssetPath = "test.cs",
                        Kind = AssetKind.Script,
                        Name = "Test",
                        Symbols = new[] { "Test" },
                        Dependencies = new string[0],
                        Summary = "Test class"
                    }
                }
            };

            ProjectIndexStore.Save(index);
            var (exists, bytes) = ProjectIndexStore.GetCacheStats();

            Assert.IsTrue(exists);
            Assert.Greater(bytes, 0);
        }

        [Test]
        public void GetCacheStats_NoCache_ReturnsFalse()
        {
            var (exists, bytes) = ProjectIndexStore.GetCacheStats();

            Assert.IsFalse(exists);
            Assert.AreEqual(0, bytes);
        }

        [Test]
        public void SaveAndLoad_PreservesLastModifiedTicks()
        {
            var ticks = DateTime.UtcNow.Ticks;
            var index = new ProjectIndex
            {
                ProjectName = "Test",
                Entries = new List<IndexEntry>
                {
                    new IndexEntry
                    {
                        AssetPath = "test.cs",
                        Kind = AssetKind.Script,
                        Name = "Test",
                        Symbols = new[] { "Test" },
                        Dependencies = new string[0],
                        Summary = "Test",
                        LastModifiedTicks = ticks
                    }
                }
            };

            ProjectIndexStore.Save(index);
            var loaded = ProjectIndexStore.Load();

            Assert.AreEqual(ticks, loaded.Entries[0].LastModifiedTicks,
                "LastModifiedTicks must survive round-trip for staleness detection");
        }

        [Test]
        public void Save_AssetKind_SerializesAsString()
        {
            var index = new ProjectIndex
            {
                ProjectName = "Test",
                Entries = new List<IndexEntry>
                {
                    new IndexEntry
                    {
                        AssetPath = "test.cs",
                        Kind = AssetKind.Script,
                        Name = "Test",
                        Symbols = new string[0],
                        Dependencies = new string[0],
                        Summary = "Test"
                    }
                }
            };

            ProjectIndexStore.Save(index);
            var raw = File.ReadAllText(Path.Combine(_tempDir, "index.json"));

            Assert.IsTrue(raw.Contains("\"Script\""), "AssetKind should serialize as string");
        }
    }
}
