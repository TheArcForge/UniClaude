using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UniClaude.Editor;

namespace UniClaude.Editor.Tests
{
    [TestFixture]
    public class SlashCommandRegistryTests
    {
        SlashCommandRegistry _registry;
        List<string> _executed;

        [SetUp]
        public void SetUp()
        {
            _registry = new SlashCommandRegistry();
            _executed = new List<string>();

            _registry.RegisterLocal("clear", "Clear chat", args => _executed.Add($"clear:{args}"));
            _registry.RegisterLocal("help", "Show help", args => _executed.Add($"help:{args}"));
            _registry.RegisterLocal("model", "Switch model", args => _executed.Add($"model:{args}"));
        }

        [Test]
        public void RegisterLocal_AddsCommand()
        {
            Assert.AreEqual(3, _registry.Commands.Count);
        }

        [Test]
        public void RegisterLocal_SortedAlphabetically()
        {
            Assert.AreEqual("clear", _registry.Commands[0].Name);
            Assert.AreEqual("help", _registry.Commands[1].Name);
            Assert.AreEqual("model", _registry.Commands[2].Name);
        }

        [Test]
        public void RegisterLocal_Duplicate_Replaces()
        {
            _registry.RegisterLocal("clear", "New description", _ => { });
            Assert.AreEqual(3, _registry.Commands.Count);
            Assert.AreEqual("New description", _registry.Find("clear").Description);
        }

        [Test]
        public void Find_ExactMatch_ReturnsCommand()
        {
            var cmd = _registry.Find("help");
            Assert.IsNotNull(cmd);
            Assert.AreEqual("help", cmd.Name);
        }

        [Test]
        public void Find_CaseInsensitive()
        {
            var cmd = _registry.Find("HELP");
            Assert.IsNotNull(cmd);
            Assert.AreEqual("help", cmd.Name);
        }

        [Test]
        public void Find_NotFound_ReturnsNull()
        {
            Assert.IsNull(_registry.Find("unknown"));
        }

        [Test]
        public void Match_EmptyPrefix_ReturnsAll()
        {
            var matches = _registry.Match("");
            Assert.AreEqual(3, matches.Count);
        }

        [Test]
        public void Match_Prefix_FiltersCorrectly()
        {
            var matches = _registry.Match("cl");
            Assert.AreEqual(1, matches.Count);
            Assert.AreEqual("clear", matches[0].Name);
        }

        [Test]
        public void Match_NoMatch_ReturnsEmpty()
        {
            var matches = _registry.Match("xyz");
            Assert.AreEqual(0, matches.Count);
        }

        [Test]
        public void Match_CaseInsensitive()
        {
            var matches = _registry.Match("HE");
            Assert.AreEqual(1, matches.Count);
            Assert.AreEqual("help", matches[0].Name);
        }

        [Test]
        public void Parse_ValidCommand_ReturnsCommand()
        {
            var cmd = _registry.Parse("/clear");
            Assert.IsNotNull(cmd);
            Assert.AreEqual("clear", cmd.Name);
        }

        [Test]
        public void Parse_WithArgs_ReturnsCommand()
        {
            var cmd = _registry.Parse("/model opus");
            Assert.IsNotNull(cmd);
            Assert.AreEqual("model", cmd.Name);
        }

        [Test]
        public void Parse_Unknown_ReturnsNull()
        {
            Assert.IsNull(_registry.Parse("/unknown"));
        }

        [Test]
        public void Parse_NotSlash_ReturnsNull()
        {
            Assert.IsNull(_registry.Parse("hello"));
        }

        [Test]
        public void Parse_Null_ReturnsNull()
        {
            Assert.IsNull(_registry.Parse(null));
        }

        [Test]
        public void ParseArgs_NoArgs_ReturnsNull()
        {
            Assert.IsNull(SlashCommandRegistry.ParseArgs("/clear"));
        }

        [Test]
        public void ParseArgs_WithArgs_ReturnsArgs()
        {
            Assert.AreEqual("opus", SlashCommandRegistry.ParseArgs("/model opus"));
        }

        [Test]
        public void ParseArgs_WithMultipleArgs_ReturnsFull()
        {
            Assert.AreEqual("claude-opus-4-6", SlashCommandRegistry.ParseArgs("/model claude-opus-4-6"));
        }

        [Test]
        public void LocalCommand_SourceIsLocal()
        {
            var cmd = _registry.Find("clear");
            Assert.AreEqual(CommandSource.Local, cmd.Source);
        }

        [Test]
        public void ScanPackageCommands_DiscoversCommandsFromPackageDirectory()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"uniclaude-test-{Guid.NewGuid():N}");
            try
            {
                var pkgCommandsDir = Path.Combine(tempDir, "com.test.package", ".claude", "commands");
                Directory.CreateDirectory(pkgCommandsDir);
                File.WriteAllText(
                    Path.Combine(pkgCommandsDir, "test-cmd.md"),
                    "---\ndescription: A test command\n---\nDo something");

                var registry = new SlashCommandRegistry();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                registry.ScanPackageCommands(tempDir, seen);

                var cmd = registry.Find("test-cmd");
                Assert.IsNotNull(cmd);
                Assert.AreEqual("A test command", cmd.Description);
                Assert.AreEqual(CommandSource.Cli, cmd.Source);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Test]
        public void ScanPackageCommands_SkipsPackagesWithoutCommandsDir()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"uniclaude-test-{Guid.NewGuid():N}");
            try
            {
                Directory.CreateDirectory(Path.Combine(tempDir, "com.test.empty"));

                var registry = new SlashCommandRegistry();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                registry.ScanPackageCommands(tempDir, seen);

                Assert.AreEqual(0, registry.Commands.Count);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Test]
        public void ScanPackageCommands_SeenSet_PreventsOverwrite()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"uniclaude-test-{Guid.NewGuid():N}");
            try
            {
                var pkgCommandsDir = Path.Combine(tempDir, "com.test.package", ".claude", "commands");
                Directory.CreateDirectory(pkgCommandsDir);
                File.WriteAllText(
                    Path.Combine(pkgCommandsDir, "clear.md"),
                    "---\ndescription: Package clear\n---\nClear stuff");

                var registry = new SlashCommandRegistry();
                registry.RegisterLocal("clear", "Local clear", _ => { });

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "clear" };
                registry.ScanPackageCommands(tempDir, seen);

                Assert.AreEqual("Local clear", registry.Find("clear").Description);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Test]
        public void ScanPackageCommands_MultiplePackages_DiscoversAll()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"uniclaude-test-{Guid.NewGuid():N}");
            try
            {
                var cmdDirA = Path.Combine(tempDir, "com.test.a", ".claude", "commands");
                Directory.CreateDirectory(cmdDirA);
                File.WriteAllText(Path.Combine(cmdDirA, "alpha.md"), "---\ndescription: Alpha cmd\n---\nAlpha");

                var cmdDirB = Path.Combine(tempDir, "com.test.b", ".claude", "commands");
                Directory.CreateDirectory(cmdDirB);
                File.WriteAllText(Path.Combine(cmdDirB, "beta.md"), "---\ndescription: Beta cmd\n---\nBeta");

                var registry = new SlashCommandRegistry();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                registry.ScanPackageCommands(tempDir, seen);

                Assert.IsNotNull(registry.Find("alpha"));
                Assert.IsNotNull(registry.Find("beta"));
                Assert.AreEqual(2, registry.Commands.Count);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Test]
        public void ScanPackageCommands_NonexistentDir_DoesNotThrow()
        {
            var registry = new SlashCommandRegistry();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            Assert.DoesNotThrow(() =>
                registry.ScanPackageCommands("/nonexistent/path/Packages", seen));
            Assert.AreEqual(0, registry.Commands.Count);
        }
    }
}
