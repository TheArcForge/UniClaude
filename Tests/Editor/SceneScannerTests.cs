using System.IO;
using NUnit.Framework;
using UniClaude.Editor;

namespace UniClaude.Editor.Tests
{
    [TestFixture]
    public class SceneScannerTests
    {
        SceneScanner _scanner;
        string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _scanner = new SceneScanner();
            _tempDir = Path.Combine(Path.GetTempPath(), "SceneScannerTest_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        string WriteFile(string filename, string content)
        {
            var path = Path.Combine(_tempDir, filename);
            File.WriteAllText(path, content);
            return path;
        }

        [Test]
        public void CanScan_UnityFile_ReturnsTrue()
        {
            Assert.IsTrue(_scanner.CanScan("Assets/Scenes/Main.unity"));
        }

        [Test]
        public void CanScan_NonUnityFile_ReturnsFalse()
        {
            Assert.IsFalse(_scanner.CanScan("Assets/Scripts/Player.cs"));
        }

        [Test]
        public void Scan_ExtractsGameObjectNames()
        {
            var path = WriteFile("TestScene.unity", @"%YAML 1.1
--- !u!1 &100
GameObject:
  m_Name: Main Camera
--- !u!1 &200
GameObject:
  m_Name: Player
--- !u!1 &300
GameObject:
  m_Name: UI Canvas
");
            var entry = _scanner.Scan(path);

            Assert.IsNotNull(entry);
            Assert.AreEqual(AssetKind.Scene, entry.Kind);
            Assert.AreEqual("TestScene", entry.Name);
            Assert.IsTrue(entry.Summary.Contains("Main Camera"));
            Assert.IsTrue(entry.Summary.Contains("Player"));
        }

        [Test]
        public void Scan_ExtractsScriptGUIDs()
        {
            var path = WriteFile("WithScripts.unity", @"%YAML 1.1
--- !u!1 &100
GameObject:
  m_Name: Player
--- !u!114 &200
MonoBehaviour:
  m_Script: {fileID: 11500000, guid: abc123def456, type: 3}
--- !u!114 &300
MonoBehaviour:
  m_Script: {fileID: 11500000, guid: 789ghi012jkl, type: 3}
");
            var entry = _scanner.Scan(path);

            Assert.IsNotNull(entry);
            Assert.IsTrue(entry.Dependencies.Length >= 2 || entry.Summary.Contains("abc123def456"));
        }

        [Test]
        public void Scan_EmptyFile_ReturnsNull()
        {
            var path = WriteFile("Empty.unity", "");
            var entry = _scanner.Scan(path);

            Assert.IsNull(entry);
        }

        [Test]
        public void Scan_SetsLastModifiedTicks()
        {
            var path = WriteFile("Timed.unity", @"%YAML 1.1
--- !u!1 &100
GameObject:
  m_Name: Timed Object
");
            var entry = _scanner.Scan(path);

            Assert.IsNotNull(entry);
            Assert.Greater(entry.LastModifiedTicks, 0);
        }

        [Test]
        public void Kind_IsScene()
        {
            Assert.AreEqual(AssetKind.Scene, _scanner.Kind);
        }
    }
}
