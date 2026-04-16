using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UniClaude.Editor;

namespace UniClaude.Editor.Tests
{
    [TestFixture]
    public class ProjectAwarenessTests
    {
        string _tempDir;
        string _indexDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "AwarenessTest_" + Path.GetRandomFileName());
            _indexDir = Path.Combine(_tempDir, "index");
            Directory.CreateDirectory(_tempDir);
            Directory.CreateDirectory(_indexDir);
            ProjectIndexStore.BaseDir = _indexDir;
        }

        [TearDown]
        public void TearDown()
        {
            ProjectAwareness.Instance?.Dispose();
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
            ProjectIndexStore.ResetBaseDir();
        }

        [Test]
        public void Initialize_SetsInstance()
        {
            var awareness = new ProjectAwareness();
            awareness.Initialize(_tempDir);

            Assert.IsNotNull(ProjectAwareness.Instance);
            Assert.AreSame(awareness, ProjectAwareness.Instance);
        }

        [Test]
        public void Dispose_ClearsInstance()
        {
            var awareness = new ProjectAwareness();
            awareness.Initialize(_tempDir);
            awareness.Dispose();

            Assert.IsNull(ProjectAwareness.Instance);
        }

        [Test]
        public void GetContext_WithScripts_ReturnsContext()
        {
            // Create a test script
            var scriptsDir = Path.Combine(_tempDir, "Assets", "Scripts");
            Directory.CreateDirectory(scriptsDir);
            File.WriteAllText(Path.Combine(scriptsDir, "Player.cs"),
                "public class Player : MonoBehaviour { public void Move() { } }");

            var awareness = new ProjectAwareness();
            awareness.Initialize(_tempDir);

            var result = awareness.GetContext("How does Player move?");

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.FormattedPrompt);
            Assert.IsTrue(result.FormattedPrompt.Contains("Project Context"));
        }

        [Test]
        public void GetContext_ZeroMaxFiles_ReturnsContextWithNoMatchedFiles()
        {
            var awareness = new ProjectAwareness();
            awareness.Initialize(_tempDir);

            var settings = new RetrievalSettings { MaxFiles = 0 };
            var result = awareness.GetContext("anything", settings);

            // With MaxFiles=0 the retriever returns no matched files,
            // but Tier 1 project summary is still generated
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Block.FileNames.Count);
            Assert.IsTrue(result.FormattedPrompt.Contains("Project Context"));
        }

        [Test]
        public void GetIndex_ReturnsCurrentIndex()
        {
            var awareness = new ProjectAwareness();
            awareness.Initialize(_tempDir);

            var index = awareness.GetIndex();
            Assert.IsNotNull(index);
        }

        [Test]
        public void HandleAssetsChanged_AddsNewEntry()
        {
            var scriptsDir = Path.Combine(_tempDir, "Assets", "Scripts");
            Directory.CreateDirectory(scriptsDir);

            var awareness = new ProjectAwareness();
            awareness.Initialize(_tempDir);

            // Simulate asset import
            var scriptPath = Path.Combine(scriptsDir, "NewScript.cs");
            File.WriteAllText(scriptPath, "public class NewScript { }");

            awareness.HandleAssetsChanged(
                imported: new[] { scriptPath },
                deleted: new string[0],
                moved: new string[0],
                movedFrom: new string[0]);

            var index = awareness.GetIndex();
            Assert.IsTrue(index.Entries.Exists(e => e.Name == "NewScript"));
        }

        [Test]
        public void PostprocessorSkipsWhenNoInstance()
        {
            // When no ProjectAwareness is active, the static Instance is null.
            // The postprocessor checks this and returns immediately.
            // After Dispose, Instance should be null.
            var awareness = new ProjectAwareness();
            awareness.Initialize(_tempDir);
            awareness.Dispose();

            Assert.IsNull(ProjectAwareness.Instance,
                "Instance should be null after Dispose — postprocessor will skip");
        }

        [Test]
        public void GetTier1Context_ReturnsSummaryWithoutRelevantFiles()
        {
            var scriptsDir = Path.Combine(_tempDir, "Assets", "Scripts");
            Directory.CreateDirectory(scriptsDir);
            File.WriteAllText(Path.Combine(scriptsDir, "Hero.cs"),
                "public class Hero : MonoBehaviour { public void Run() { } }");

            var awareness = new ProjectAwareness();
            awareness.Initialize(_tempDir);

            var tier1 = awareness.GetTier1Context();

            Assert.IsNotNull(tier1);
            Assert.IsTrue(tier1.Contains("Project Context"));
            Assert.IsTrue(tier1.Contains("Scripts: 1"));
            Assert.IsFalse(tier1.Contains("Relevant Files"));
        }

        [Test]
        public void HandleAssetsChanged_RemovesDeletedEntry()
        {
            var scriptsDir = Path.Combine(_tempDir, "Assets", "Scripts");
            Directory.CreateDirectory(scriptsDir);
            var scriptPath = Path.Combine(scriptsDir, "ToDelete.cs");
            File.WriteAllText(scriptPath, "public class ToDelete { }");

            var awareness = new ProjectAwareness();
            awareness.Initialize(_tempDir);

            // Verify it was indexed
            Assert.IsTrue(awareness.GetIndex().Entries.Exists(e => e.Name == "ToDelete"));

            // Simulate delete
            File.Delete(scriptPath);
            awareness.HandleAssetsChanged(
                imported: new string[0],
                deleted: new[] { scriptPath },
                moved: new string[0],
                movedFrom: new string[0]);

            Assert.IsFalse(awareness.GetIndex().Entries.Exists(e => e.Name == "ToDelete"));
        }

        [Test]
        public void Initialize_ScansLocalPackages()
        {
            var scriptsDir = Path.Combine(_tempDir, "Assets", "Scripts");
            Directory.CreateDirectory(scriptsDir);
            File.WriteAllText(Path.Combine(scriptsDir, "Player.cs"),
                "public class Player : MonoBehaviour { }");

            var pkgDir = Path.Combine(_tempDir, "Packages", "com.test.pkg");
            var pkgRuntime = Path.Combine(pkgDir, "Runtime");
            Directory.CreateDirectory(pkgRuntime);
            File.WriteAllText(Path.Combine(pkgDir, "package.json"), @"{
""name"": ""com.test.pkg"",
""version"": ""1.0.0"",
""displayName"": ""Test Package""
}");
            File.WriteAllText(Path.Combine(pkgRuntime, "Utility.cs"),
                "namespace TestPkg { public class Utility { public void DoThing() { } } }");

            var awareness = new ProjectAwareness();
            awareness.Initialize(_tempDir);

            var index = awareness.GetIndex();
            Assert.IsTrue(index.Entries.Exists(e => e.Name == "Player"));
            Assert.IsTrue(index.Entries.Exists(e => e.Name == "Utility" && e.Source == "com.test.pkg"));

            awareness.Dispose();
        }

        [Test]
        public void Initialize_SkipsPackageTestsAndSamplesDirectories()
        {
            var pkgDir = Path.Combine(_tempDir, "Packages", "com.test.pkg");
            Directory.CreateDirectory(Path.Combine(pkgDir, "Runtime"));
            Directory.CreateDirectory(Path.Combine(pkgDir, "Tests", "Editor"));
            Directory.CreateDirectory(Path.Combine(pkgDir, "Samples"));
            File.WriteAllText(Path.Combine(pkgDir, "package.json"), @"{
""name"": ""com.test.pkg"",
""version"": ""1.0.0"",
""displayName"": ""Test""
}");
            File.WriteAllText(Path.Combine(pkgDir, "Runtime", "Good.cs"),
                "public class Good { }");
            File.WriteAllText(Path.Combine(pkgDir, "Tests", "Editor", "BadTest.cs"),
                "public class BadTest { }");
            File.WriteAllText(Path.Combine(pkgDir, "Samples", "Sample.cs"),
                "public class Sample { }");

            Directory.CreateDirectory(Path.Combine(_tempDir, "Assets"));

            var awareness = new ProjectAwareness();
            awareness.Initialize(_tempDir);

            var index = awareness.GetIndex();
            Assert.IsTrue(index.Entries.Exists(e => e.Name == "Good"));
            Assert.IsFalse(index.Entries.Exists(e => e.Name == "BadTest"));
            Assert.IsFalse(index.Entries.Exists(e => e.Name == "Sample"));

            awareness.Dispose();
        }

        [Test]
        public void Initialize_RespectsExcludedFolders()
        {
            var scriptsDir = Path.Combine(_tempDir, "Assets", "Scripts");
            var thirdPartyDir = Path.Combine(_tempDir, "Assets", "ThirdParty");
            Directory.CreateDirectory(scriptsDir);
            Directory.CreateDirectory(thirdPartyDir);
            File.WriteAllText(Path.Combine(scriptsDir, "Player.cs"),
                "public class Player { }");
            File.WriteAllText(Path.Combine(thirdPartyDir, "Vendor.cs"),
                "public class Vendor { }");

            // Save settings with exclusion — use the same _indexDir-based settings
            UniClaudeSettings.SettingsDir = _indexDir;
            var settings = new UniClaudeSettings();
            settings.ExcludedFolders.Add("Assets/ThirdParty");
            UniClaudeSettings.Save(settings);

            var awareness = new ProjectAwareness();
            awareness.Initialize(_tempDir);

            var index = awareness.GetIndex();
            Assert.IsTrue(index.Entries.Exists(e => e.Name == "Player"));
            Assert.IsFalse(index.Entries.Exists(e => e.Name == "Vendor"));

            awareness.Dispose();
            UniClaudeSettings.ResetSettingsDir();
        }

        [Test]
        public void Initialize_RegistryPackageExcludedByDefault()
        {
            Directory.CreateDirectory(Path.Combine(_tempDir, "Assets"));
            Directory.CreateDirectory(Path.Combine(_tempDir, "Packages"));

            File.WriteAllText(Path.Combine(_tempDir, "Packages", "packages-lock.json"), @"{
""dependencies"": {
  ""com.unity.ugui"": {
    ""version"": ""2.0.0"",
    ""depth"": 0,
    ""source"": ""registry"",
    ""dependencies"": {}
  }
}
}");
            var cacheDir = Path.Combine(_tempDir, "Library", "PackageCache", "com.unity.ugui@2.0.0", "Runtime");
            Directory.CreateDirectory(cacheDir);
            File.WriteAllText(Path.Combine(cacheDir, "UGUI.cs"),
                "public class UGUI { }");

            var awareness = new ProjectAwareness();
            awareness.Initialize(_tempDir);

            var index = awareness.GetIndex();
            Assert.IsFalse(index.Entries.Exists(e => e.Name == "UGUI"));

            awareness.Dispose();
        }

        [Test]
        public void FullRebuild_IncludesPackageEntries()
        {
            Directory.CreateDirectory(Path.Combine(_tempDir, "Assets"));
            var pkgDir = Path.Combine(_tempDir, "Packages", "com.test.pkg");
            var pkgRuntime = Path.Combine(pkgDir, "Runtime");
            Directory.CreateDirectory(pkgRuntime);
            File.WriteAllText(Path.Combine(pkgDir, "package.json"), @"{
""name"": ""com.test.pkg"",
""version"": ""1.0.0"",
""displayName"": ""Test""
}");
            File.WriteAllText(Path.Combine(pkgRuntime, "Util.cs"),
                "public class Util { }");

            var awareness = new ProjectAwareness();
            awareness.Initialize(_tempDir);

            var result = awareness.FullRebuild();
            Assert.IsTrue(result.Contains("total entries"));

            var index = awareness.GetIndex();
            Assert.IsTrue(index.Entries.Exists(e => e.Name == "Util" && e.Source == "com.test.pkg"));

            awareness.Dispose();
        }

        [Test]
        public void MakeRelative_PackageEntry_UsesPackagesPrefix()
        {
            Directory.CreateDirectory(Path.Combine(_tempDir, "Assets"));
            var pkgDir = Path.Combine(_tempDir, "Packages", "com.test.pkg");
            var pkgRuntime = Path.Combine(pkgDir, "Runtime");
            Directory.CreateDirectory(pkgRuntime);
            File.WriteAllText(Path.Combine(pkgDir, "package.json"), @"{
""name"": ""com.test.pkg"",
""version"": ""1.0.0"",
""displayName"": ""Test""
}");
            File.WriteAllText(Path.Combine(pkgRuntime, "Foo.cs"),
                "public class Foo { }");

            var awareness = new ProjectAwareness();
            awareness.Initialize(_tempDir);

            var index = awareness.GetIndex();
            var entry = index.Entries.Find(e => e.Name == "Foo");
            Assert.IsNotNull(entry);
            Assert.IsTrue(entry.AssetPath.StartsWith("Packages/com.test.pkg/"));

            awareness.Dispose();
        }

        [Test]
        public void HandleAssetsChanged_RespectsExcludedFolders()
        {
            var scriptsDir = Path.Combine(_tempDir, "Assets", "Scripts");
            var thirdPartyDir = Path.Combine(_tempDir, "Assets", "ThirdParty");
            Directory.CreateDirectory(scriptsDir);
            Directory.CreateDirectory(thirdPartyDir);
            File.WriteAllText(Path.Combine(scriptsDir, "Player.cs"),
                "public class Player { }");

            // Save settings with exclusion
            UniClaudeSettings.SettingsDir = _indexDir;
            var settings = new UniClaudeSettings();
            settings.ExcludedFolders.Add("Assets/ThirdParty");
            UniClaudeSettings.Save(settings);

            var awareness = new ProjectAwareness();
            awareness.Initialize(_tempDir);

            // Simulate an asset import in the excluded folder
            var vendorFile = Path.Combine(thirdPartyDir, "Vendor.cs");
            File.WriteAllText(vendorFile, "public class Vendor { }");
            awareness.HandleAssetsChanged(new[] { vendorFile }, new string[0], new string[0], new string[0]);

            var index = awareness.GetIndex();
            Assert.IsFalse(index.Entries.Exists(e => e.Name == "Vendor"));

            awareness.Dispose();
            UniClaudeSettings.ResetSettingsDir();
        }

        [Test]
        public void FullRebuild_RespectsExcludedFolders()
        {
            var scriptsDir = Path.Combine(_tempDir, "Assets", "Scripts");
            var thirdPartyDir = Path.Combine(_tempDir, "Assets", "ThirdParty");
            Directory.CreateDirectory(scriptsDir);
            Directory.CreateDirectory(thirdPartyDir);
            File.WriteAllText(Path.Combine(scriptsDir, "Player.cs"),
                "public class Player { }");
            File.WriteAllText(Path.Combine(thirdPartyDir, "Vendor.cs"),
                "public class Vendor { }");

            UniClaudeSettings.SettingsDir = _indexDir;
            var settings = new UniClaudeSettings();
            settings.ExcludedFolders.Add("Assets/ThirdParty");
            UniClaudeSettings.Save(settings);

            var awareness = new ProjectAwareness();
            awareness.Initialize(_tempDir);
            awareness.FullRebuild();

            var index = awareness.GetIndex();
            Assert.IsTrue(index.Entries.Exists(e => e.Name == "Player"));
            Assert.IsFalse(index.Entries.Exists(e => e.Name == "Vendor"));

            awareness.Dispose();
            UniClaudeSettings.ResetSettingsDir();
        }

        [Test]
        public void HandleAssetsChanged_DetectsPackageSource()
        {
            Directory.CreateDirectory(Path.Combine(_tempDir, "Assets"));
            var pkgDir = Path.Combine(_tempDir, "Packages", "com.test.pkg");
            var pkgRuntime = Path.Combine(pkgDir, "Runtime");
            Directory.CreateDirectory(pkgRuntime);
            File.WriteAllText(Path.Combine(pkgDir, "package.json"), @"{
""name"": ""com.test.pkg"",
""version"": ""1.0.0"",
""displayName"": ""Test""
}");

            var awareness = new ProjectAwareness();
            awareness.Initialize(_tempDir);

            // Simulate a package file import
            var pkgFile = Path.Combine(pkgRuntime, "NewUtil.cs");
            File.WriteAllText(pkgFile, "public class NewUtil { }");
            awareness.HandleAssetsChanged(new[] { "Packages/com.test.pkg/Runtime/NewUtil.cs" }, new string[0], new string[0], new string[0]);

            var index = awareness.GetIndex();
            var entry = index.Entries.Find(e => e.Name == "NewUtil");
            Assert.IsNotNull(entry);
            Assert.AreEqual("com.test.pkg", entry.Source);

            awareness.Dispose();
        }

        [Test]
        public void TreeSummary_SmallBudget_SummarizesDeepFolders()
        {
            var assetsDir = Path.Combine(_tempDir, "Assets");
            var scriptsDir = Path.Combine(assetsDir, "Scripts");
            var aiDir = Path.Combine(scriptsDir, "AI");
            Directory.CreateDirectory(aiDir);

            File.WriteAllText(Path.Combine(scriptsDir, "Player.cs"), "class Player {}");
            File.WriteAllText(Path.Combine(scriptsDir, "Player.cs.meta"), "");
            File.WriteAllText(Path.Combine(scriptsDir, "Enemy.cs"), "class Enemy {}");
            File.WriteAllText(Path.Combine(scriptsDir, "Enemy.cs.meta"), "");

            for (int i = 0; i < 20; i++)
            {
                File.WriteAllText(Path.Combine(aiDir, $"Bot{i}.cs"), $"class Bot{i} {{}}");
                File.WriteAllText(Path.Combine(aiDir, $"Bot{i}.cs.meta"), "");
            }

            UniClaudeSettings.SettingsDir = _indexDir;
            var settings = new UniClaudeSettings { ContextTokenBudget = 100 };
            UniClaudeSettings.Save(settings);

            var awareness = new ProjectAwareness();
            awareness.Initialize(_tempDir);

            var tree = awareness.GetIndex().ProjectTreeSummary;
            Assert.IsNotNull(tree);

            Assert.IsTrue(tree.Contains("Assets/Scripts/AI/"), "Should mention the AI folder");
            Assert.IsFalse(tree.Contains("Bot19.cs"), "Deep files should be summarized, not listed individually");

            awareness.Dispose();
            UniClaudeSettings.ResetSettingsDir();
        }

        [Test]
        public void TreeSummary_UnlimitedBudget_ListsAllFiles()
        {
            var assetsDir = Path.Combine(_tempDir, "Assets");
            var scriptsDir = Path.Combine(assetsDir, "Scripts");
            Directory.CreateDirectory(scriptsDir);

            File.WriteAllText(Path.Combine(scriptsDir, "Player.cs"), "class Player {}");
            File.WriteAllText(Path.Combine(scriptsDir, "Player.cs.meta"), "");
            File.WriteAllText(Path.Combine(scriptsDir, "Enemy.cs"), "class Enemy {}");
            File.WriteAllText(Path.Combine(scriptsDir, "Enemy.cs.meta"), "");

            UniClaudeSettings.SettingsDir = _indexDir;
            var settings = new UniClaudeSettings { ContextTokenBudget = 0 };
            UniClaudeSettings.Save(settings);

            var awareness = new ProjectAwareness();
            awareness.Initialize(_tempDir);

            var tree = awareness.GetIndex().ProjectTreeSummary;
            Assert.IsNotNull(tree);
            Assert.IsTrue(tree.Contains("Player.cs"));
            Assert.IsTrue(tree.Contains("Enemy.cs"));

            awareness.Dispose();
            UniClaudeSettings.ResetSettingsDir();
        }
    }
}
