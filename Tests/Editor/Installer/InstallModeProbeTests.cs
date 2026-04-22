using System.IO;
using NUnit.Framework;
using UniClaude.Editor.Installer;

namespace UniClaude.Editor.Tests.Installer
{
    [TestFixture]
    public class InstallModeProbeTests
    {
        string _tmp;

        [SetUp]
        public void Setup()
        {
            _tmp = Path.Combine(Path.GetTempPath(), "uc-probe-" + System.Guid.NewGuid());
            Directory.CreateDirectory(_tmp);
        }

        [TearDown]
        public void Tear() => Directory.Delete(_tmp, true);

        [Test]
        public void SentinelPresent_DetectsTrue()
        {
            Directory.CreateDirectory(Path.Combine(_tmp, ".git", "info"));
            File.WriteAllText(
                Path.Combine(_tmp, ".git", "info", "exclude"),
                "# UniClaude ninja-mode (managed by UniClaude — do not edit)\n" +
                "Packages/com.arcforge.uniclaude/\n");

            Assert.IsTrue(InstallModeProbe.HasSentinel(_tmp));
        }

        [Test]
        public void SentinelAbsent_DetectsFalse()
        {
            Directory.CreateDirectory(Path.Combine(_tmp, ".git", "info"));
            File.WriteAllText(Path.Combine(_tmp, ".git", "info", "exclude"), "some.tmp\n");
            Assert.IsFalse(InstallModeProbe.HasSentinel(_tmp));
        }

        [Test]
        public void NoExcludeFile_DetectsFalse()
        {
            Assert.IsFalse(InstallModeProbe.HasSentinel(_tmp));
        }

        [Test]
        public void IsGitRepo_DetectsGitDir()
        {
            Assert.IsFalse(InstallModeProbe.IsGitRepo(_tmp));
            Directory.CreateDirectory(Path.Combine(_tmp, ".git"));
            Assert.IsTrue(InstallModeProbe.IsGitRepo(_tmp));
        }
    }
}
