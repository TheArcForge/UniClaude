using System.IO;
using NUnit.Framework;
using UniClaude.Editor;

namespace UniClaude.Editor.Tests
{
    [TestFixture]
    public class UniClaudeSettingsTests
    {
        string _testDir;

        [SetUp]
        public void SetUp()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "UniClaudeSettingsTest_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_testDir);
            UniClaudeSettings.SettingsDir = _testDir;
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, true);
            UniClaudeSettings.ResetSettingsDir();
        }

        [Test]
        public void Load_MissingFile_ReturnsDefaults()
        {
            var settings = UniClaudeSettings.Load();
            Assert.IsNotNull(settings);
            Assert.IsNull(settings.SelectedModel);
        }

        [Test]
        public void SaveAndLoad_RoundTrip()
        {
            var settings = new UniClaudeSettings { SelectedModel = "opus" };
            UniClaudeSettings.Save(settings);

            var loaded = UniClaudeSettings.Load();
            Assert.AreEqual("opus", loaded.SelectedModel);
        }

        [Test]
        public void SelectedModel_PersistsCorrectly()
        {
            var settings = new UniClaudeSettings { SelectedModel = "haiku" };
            UniClaudeSettings.Save(settings);

            var loaded = UniClaudeSettings.Load();
            Assert.AreEqual("haiku", loaded.SelectedModel);
        }

        [Test]
        public void Load_CorruptFile_ReturnsDefaults()
        {
            File.WriteAllText(Path.Combine(_testDir, "settings.json"), "NOT VALID JSON{{{");

            var settings = UniClaudeSettings.Load();
            Assert.IsNotNull(settings);
            Assert.IsNull(settings.SelectedModel);
        }

        [Test]
        public void ProjectAwarenessSettings_PersistCorrectly()
        {
            var settings = new UniClaudeSettings
            {
                ProjectAwarenessEnabled = false
            };
            UniClaudeSettings.Save(settings);

            var loaded = UniClaudeSettings.Load();
            Assert.AreEqual(false, loaded.ProjectAwarenessEnabled);
        }

        [Test]
        public void ChatFontSize_PersistsCorrectly()
        {
            var settings = new UniClaudeSettings { ChatFontSize = "large" };
            UniClaudeSettings.Save(settings);

            var loaded = UniClaudeSettings.Load();
            Assert.AreEqual("large", loaded.ChatFontSize);
        }

        [Test]
        public void ChatFontSize_DefaultsToMedium()
        {
            var settings = new UniClaudeSettings();
            Assert.AreEqual("medium", settings.ChatFontSize);
        }

        [Test]
        public void SaveAndLoad_PackageIndexOverrides_RoundTrip()
        {
            var settings = new UniClaudeSettings();
            settings.PackageIndexOverrides["com.unity.ugui"] = true;
            settings.PackageIndexOverrides["com.arcforge.ui"] = false;
            UniClaudeSettings.Save(settings);

            var loaded = UniClaudeSettings.Load();
            Assert.AreEqual(2, loaded.PackageIndexOverrides.Count);
            Assert.IsTrue(loaded.PackageIndexOverrides["com.unity.ugui"]);
            Assert.IsFalse(loaded.PackageIndexOverrides["com.arcforge.ui"]);
        }

        [Test]
        public void SaveAndLoad_ExcludedFolders_RoundTrip()
        {
            var settings = new UniClaudeSettings();
            settings.ExcludedFolders.Add("Assets/ThirdParty");
            settings.ExcludedFolders.Add("Assets/Plugins/OldSDK");
            UniClaudeSettings.Save(settings);

            var loaded = UniClaudeSettings.Load();
            Assert.AreEqual(2, loaded.ExcludedFolders.Count);
            Assert.AreEqual("Assets/ThirdParty", loaded.ExcludedFolders[0]);
            Assert.AreEqual("Assets/Plugins/OldSDK", loaded.ExcludedFolders[1]);
        }

        [Test]
        public void Load_OldSettingsFile_DefaultsNewFieldsToEmpty()
        {
            var json = @"{ ""SelectedModel"": ""sonnet"", ""ProjectAwarenessEnabled"": true }";
            File.WriteAllText(Path.Combine(_testDir, "settings.json"), json);

            var loaded = UniClaudeSettings.Load();
            Assert.IsNotNull(loaded.PackageIndexOverrides);
            Assert.AreEqual(0, loaded.PackageIndexOverrides.Count);
            Assert.IsNotNull(loaded.ExcludedFolders);
            Assert.AreEqual(0, loaded.ExcludedFolders.Count);
        }

        [Test]
        public void ContextTokenBudget_DefaultsTo3300()
        {
            var settings = new UniClaudeSettings();
            Assert.AreEqual(3300, settings.ContextTokenBudget);
        }

        [Test]
        public void ContextTokenBudget_PersistsCorrectly()
        {
            var settings = new UniClaudeSettings { ContextTokenBudget = 5000 };
            UniClaudeSettings.Save(settings);

            var loaded = UniClaudeSettings.Load();
            Assert.AreEqual(5000, loaded.ContextTokenBudget);
        }

        [Test]
        public void ContextTokenBudget_ZeroMeansUnlimited()
        {
            var settings = new UniClaudeSettings { ContextTokenBudget = 0 };
            UniClaudeSettings.Save(settings);

            var loaded = UniClaudeSettings.Load();
            Assert.AreEqual(0, loaded.ContextTokenBudget);
        }
    }
}
