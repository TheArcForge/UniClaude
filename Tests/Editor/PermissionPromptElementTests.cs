using NUnit.Framework;
using Newtonsoft.Json.Linq;
using UniClaude.Editor;
using UniClaude.Editor.UI;

namespace UniClaude.Editor.Tests
{
    [TestFixture]
    public class PermissionPromptElementTests
    {
        [Test]
        public void FormatToolContext_Edit_ShowsFilePath()
        {
            var input = new JObject { ["file_path"] = "/Assets/Scripts/Player.cs" };
            var result = PermissionPromptElement.FormatToolContext("Edit", input);
            Assert.AreEqual("File: /Assets/Scripts/Player.cs", result);
        }

        [Test]
        public void FormatToolContext_Write_ShowsFilePathAndSize()
        {
            var input = new JObject
            {
                ["file_path"] = "/Assets/test.cs",
                ["content"] = "using System;\n\npublic class Test { }",
            };
            var result = PermissionPromptElement.FormatToolContext("Write", input);
            Assert.That(result, Does.StartWith("File: /Assets/test.cs"));
            Assert.That(result, Does.Contain("chars"));
        }

        [Test]
        public void FormatToolContext_Read_ShowsFilePath()
        {
            var input = new JObject { ["file_path"] = "/Assets/data.json" };
            var result = PermissionPromptElement.FormatToolContext("Read", input);
            Assert.AreEqual("File: /Assets/data.json", result);
        }

        [Test]
        public void FormatToolContext_Bash_ShowsCommand()
        {
            var input = new JObject { ["command"] = "git status" };
            var result = PermissionPromptElement.FormatToolContext("Bash", input);
            Assert.AreEqual("Command: git status", result);
        }

        [Test]
        public void FormatToolContext_Bash_TruncatesLongCommand()
        {
            var longCmd = new string('x', 300);
            var input = new JObject { ["command"] = longCmd };
            var result = PermissionPromptElement.FormatToolContext("Bash", input);
            Assert.That(result.Length, Is.LessThanOrEqualTo(215)); // "Command: " + 200 + "..."
        }

        [Test]
        public void FormatToolContext_Grep_ShowsPatternAndPath()
        {
            var input = new JObject { ["pattern"] = "TODO", ["path"] = "Assets/" };
            var result = PermissionPromptElement.FormatToolContext("Grep", input);
            Assert.AreEqual("Search: TODO in Assets/", result);
        }

        [Test]
        public void FormatToolContext_Glob_ShowsPattern()
        {
            var input = new JObject { ["pattern"] = "**/*.cs" };
            var result = PermissionPromptElement.FormatToolContext("Glob", input);
            Assert.AreEqual("Pattern: **/*.cs", result);
        }

        [Test]
        public void FormatToolContext_UnknownTool_ShowsGenericSummary()
        {
            var input = new JObject { ["foo"] = "bar", ["baz"] = 42 };
            var result = PermissionPromptElement.FormatToolContext("SomeNewTool", input);
            Assert.That(result, Does.Contain("foo"));
        }

        [Test]
        public void FormatToolContext_NullInput_ReturnsEmpty()
        {
            var result = PermissionPromptElement.FormatToolContext("Edit", null);
            Assert.AreEqual("", result);
        }
    }
}
