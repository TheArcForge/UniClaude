using System.IO;
using NUnit.Framework;
using UniClaude.Editor.MCP;

namespace UniClaude.Editor.Tests.MCP
{
    /// <summary>
    /// Tests for <see cref="FileTools"/> MCP tool methods.
    /// Uses a temporary directory under Library/UniClaude/test-temp/ for all file operations.
    /// </summary>
    public class FileToolsTests
    {
        /// <summary>
        /// The project-relative path to the temp directory used by all tests.
        /// </summary>
        const string TempRelativeDir = "Library/UniClaude/test-temp";

        /// <summary>
        /// The absolute path to the temp directory.
        /// </summary>
        string _tempAbsoluteDir;

        [SetUp]
        public void SetUp()
        {
            _tempAbsoluteDir = Path.Combine(
                Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..")),
                TempRelativeDir);

            // Ensure a clean temp directory for each test
            if (Directory.Exists(_tempAbsoluteDir))
                Directory.Delete(_tempAbsoluteDir, true);
            Directory.CreateDirectory(_tempAbsoluteDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempAbsoluteDir))
                Directory.Delete(_tempAbsoluteDir, true);
        }

        [Test]
        public void ReadFile_ReturnsContent()
        {
            var relativePath = TempRelativeDir + "/read-test.txt";
            var absolutePath = Path.Combine(_tempAbsoluteDir, "read-test.txt");
            File.WriteAllText(absolutePath, "Hello, World!\nLine 2\nLine 3");

            var result = FileTools.ReadFile(relativePath);

            Assert.IsFalse(result.IsError);
            Assert.That(result.Text, Does.Contain("Hello, World!"));
            Assert.That(result.Text, Does.Contain("\"lineCount\": 3"));
        }

        [Test]
        public void ReadFile_CRLFLineEndings_CountsCorrectly()
        {
            var relativePath = TempRelativeDir + "/crlf-test.txt";
            var absolutePath = Path.Combine(_tempAbsoluteDir, "crlf-test.txt");
            File.WriteAllText(absolutePath, "Line 1\r\nLine 2\r\nLine 3");

            var result = FileTools.ReadFile(relativePath);

            Assert.IsFalse(result.IsError);
            Assert.That(result.Text, Does.Contain("\"lineCount\": 3"));
        }

        [Test]
        public void ReadFile_MixedLineEndings_CountsCorrectly()
        {
            var relativePath = TempRelativeDir + "/mixed-eol.txt";
            var absolutePath = Path.Combine(_tempAbsoluteDir, "mixed-eol.txt");
            File.WriteAllText(absolutePath, "Line 1\nLine 2\r\nLine 3\nLine 4");

            var result = FileTools.ReadFile(relativePath);

            Assert.IsFalse(result.IsError);
            Assert.That(result.Text, Does.Contain("\"lineCount\": 4"));
        }

        [Test]
        public void ReadFile_NotFound_SuggestsSimilar()
        {
            // Create a file with a similar name
            File.WriteAllText(Path.Combine(_tempAbsoluteDir, "config.json"), "{}");
            File.WriteAllText(Path.Combine(_tempAbsoluteDir, "config.yaml"), "key: value");

            var result = FileTools.ReadFile(TempRelativeDir + "/confg.json");

            Assert.IsTrue(result.IsError);
            Assert.That(result.Text, Does.Contain("File not found"));
            Assert.That(result.Text, Does.Contain("Did you mean"));
            Assert.That(result.Text, Does.Contain("config.json"));
        }

        [Test]
        public void WriteFile_CreatesFile()
        {
            var relativePath = TempRelativeDir + "/write-test.txt";
            var absolutePath = Path.Combine(_tempAbsoluteDir, "write-test.txt");

            var result = FileTools.WriteFile(relativePath, "file content here");

            Assert.IsFalse(result.IsError);
            Assert.IsTrue(File.Exists(absolutePath));
            Assert.AreEqual("file content here", File.ReadAllText(absolutePath));
            Assert.That(result.Text, Does.Contain("bytesWritten"));
        }

        [Test]
        public void WriteFile_CreatesDirectories()
        {
            var relativePath = TempRelativeDir + "/nested/deep/dir/file.txt";
            var absolutePath = Path.Combine(_tempAbsoluteDir, "nested", "deep", "dir", "file.txt");

            var result = FileTools.WriteFile(relativePath, "nested content");

            Assert.IsFalse(result.IsError);
            Assert.IsTrue(File.Exists(absolutePath));
            Assert.AreEqual("nested content", File.ReadAllText(absolutePath));
        }

        [Test]
        public void CreateScript_MonoBehaviourTemplate()
        {
            var relativePath = TempRelativeDir + "/PlayerController.cs";
            var absolutePath = Path.Combine(_tempAbsoluteDir, "PlayerController.cs");

            var result = FileTools.CreateScript(relativePath, "MonoBehaviour", null);

            Assert.IsFalse(result.IsError);
            Assert.IsTrue(File.Exists(absolutePath));

            var content = File.ReadAllText(absolutePath);
            Assert.That(content, Does.Contain(": MonoBehaviour"));
            Assert.That(content, Does.Contain("class PlayerController"));
            Assert.That(content, Does.Contain("using UnityEngine;"));
        }

        [Test]
        public void CreateScript_CustomContent()
        {
            var relativePath = TempRelativeDir + "/Custom.cs";
            var absolutePath = Path.Combine(_tempAbsoluteDir, "Custom.cs");
            var customContent = "namespace Test { public static class Custom { } }";

            var result = FileTools.CreateScript(relativePath, "custom", customContent);

            Assert.IsFalse(result.IsError);
            Assert.IsTrue(File.Exists(absolutePath));
            Assert.AreEqual(customContent, File.ReadAllText(absolutePath));
        }

        [Test]
        public void ModifyScript_ReplacesText()
        {
            var relativePath = TempRelativeDir + "/Modify.cs";
            var absolutePath = Path.Combine(_tempAbsoluteDir, "Modify.cs");
            File.WriteAllText(absolutePath, "public class Foo\n{\n    int health = 100;\n}\n");

            var result = FileTools.ModifyScript(relativePath, "int health = 100", "int health = 200");

            Assert.IsFalse(result.IsError);
            var newContent = File.ReadAllText(absolutePath);
            Assert.That(newContent, Does.Contain("int health = 200"));
            Assert.That(newContent, Does.Not.Contain("int health = 100"));
            Assert.That(result.Text, Does.Contain("diff"));
        }

        [Test]
        public void ModifyScript_NotFound_ShowsSnippet()
        {
            var relativePath = TempRelativeDir + "/Snippet.cs";
            var absolutePath = Path.Combine(_tempAbsoluteDir, "Snippet.cs");
            File.WriteAllText(absolutePath, "public class Bar { int x = 1; }");

            var result = FileTools.ModifyScript(relativePath, "nonexistent string", "replacement");

            Assert.IsTrue(result.IsError);
            Assert.That(result.Text, Does.Contain("oldString not found"));
            Assert.That(result.Text, Does.Contain("public class Bar"));
        }

        [Test]
        public void DeleteFile_RemovesFileAndMeta()
        {
            var relativePath = TempRelativeDir + "/ToDelete.cs";
            var absolutePath = Path.Combine(_tempAbsoluteDir, "ToDelete.cs");
            var metaPath = absolutePath + ".meta";

            File.WriteAllText(absolutePath, "content");
            File.WriteAllText(metaPath, "meta content");

            var result = FileTools.DeleteFile(relativePath);

            Assert.IsFalse(result.IsError);
            Assert.IsFalse(File.Exists(absolutePath));
            Assert.IsFalse(File.Exists(metaPath));
            Assert.That(result.Text, Does.Contain("\"metaDeleted\": true"));
        }

        [Test]
        public void FindFiles_MatchesPattern()
        {
            // Create several files
            File.WriteAllText(Path.Combine(_tempAbsoluteDir, "alpha.cs"), "");
            File.WriteAllText(Path.Combine(_tempAbsoluteDir, "beta.cs"), "");
            File.WriteAllText(Path.Combine(_tempAbsoluteDir, "gamma.txt"), "");

            var subDir = Path.Combine(_tempAbsoluteDir, "sub");
            Directory.CreateDirectory(subDir);
            File.WriteAllText(Path.Combine(subDir, "delta.cs"), "");

            var result = FileTools.FindFiles(TempRelativeDir + "/**/*.cs");

            Assert.IsFalse(result.IsError);
            Assert.That(result.Text, Does.Contain("alpha.cs"));
            Assert.That(result.Text, Does.Contain("beta.cs"));
            Assert.That(result.Text, Does.Contain("delta.cs"));
            Assert.That(result.Text, Does.Not.Contain("gamma.txt"));
        }
    }
}
