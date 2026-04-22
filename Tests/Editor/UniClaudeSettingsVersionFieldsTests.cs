using System.IO;
using NUnit.Framework;

namespace UniClaude.Editor.Tests
{
    [TestFixture]
    public class UniClaudeSettingsVersionFieldsTests
    {
        string _tmpDir;
        string _originalDir;

        [SetUp]
        public void Setup()
        {
            _tmpDir = Path.Combine(Path.GetTempPath(), "uc-settings-" + System.Guid.NewGuid());
            Directory.CreateDirectory(_tmpDir);
            _originalDir = UniClaudeSettings.SettingsDir;
            UniClaudeSettings.SettingsDir = _tmpDir;
        }

        [TearDown]
        public void Tear()
        {
            UniClaudeSettings.SettingsDir = _originalDir;
            try { if (_tmpDir != null && Directory.Exists(_tmpDir)) Directory.Delete(_tmpDir, true); }
            catch { /* best-effort cleanup */ }
        }

        [Test]
        public void VersionFields_RoundTrip()
        {
            var s = new UniClaudeSettings
            {
                LastVersionCheckIsoUtc = "2026-04-22T10:00:00Z",
                LastKnownLatestVersion = "v0.3.0",
                LastKnownReleaseNotes = "## Changes\n- foo",
                LastKnownReleaseUrl = "https://github.com/TheArcForge/UniClaude/releases/tag/v0.3.0",
                LastKnownReleasePublishedAt = "2026-04-20T08:00:00Z",
            };
            UniClaudeSettings.Save(s);

            var loaded = UniClaudeSettings.Load();

            Assert.AreEqual("2026-04-22T10:00:00Z", loaded.LastVersionCheckIsoUtc);
            Assert.AreEqual("v0.3.0", loaded.LastKnownLatestVersion);
            Assert.AreEqual("## Changes\n- foo", loaded.LastKnownReleaseNotes);
            Assert.AreEqual("https://github.com/TheArcForge/UniClaude/releases/tag/v0.3.0", loaded.LastKnownReleaseUrl);
            Assert.AreEqual("2026-04-20T08:00:00Z", loaded.LastKnownReleasePublishedAt);
        }

        [Test]
        public void VersionFields_DefaultNull()
        {
            var s = new UniClaudeSettings();
            Assert.IsNull(s.LastVersionCheckIsoUtc);
            Assert.IsNull(s.LastKnownLatestVersion);
        }
    }
}
