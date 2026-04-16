using System.Collections.Generic;
using NUnit.Framework;

namespace UniClaude.Editor.Tests
{
    [TestFixture]
    public class IndexFilterSettingsTests
    {
        [Test]
        public void IsPackageIncluded_LocalPackage_DefaultTrue()
        {
            var settings = new UniClaudeSettings();
            var pkg = new PackageInfo { Name = "com.arcforge.ui", IsLocal = true };

            Assert.IsTrue(IndexFilterSettings.IsPackageIncluded(pkg, settings));
        }

        [Test]
        public void IsPackageIncluded_RegistryPackage_DefaultFalse()
        {
            var settings = new UniClaudeSettings();
            var pkg = new PackageInfo { Name = "com.unity.ugui", IsLocal = false };

            Assert.IsFalse(IndexFilterSettings.IsPackageIncluded(pkg, settings));
        }

        [Test]
        public void IsPackageIncluded_LocalPackageExplicitlyExcluded_ReturnsFalse()
        {
            var settings = new UniClaudeSettings();
            settings.PackageIndexOverrides["com.arcforge.ui"] = false;
            var pkg = new PackageInfo { Name = "com.arcforge.ui", IsLocal = true };

            Assert.IsFalse(IndexFilterSettings.IsPackageIncluded(pkg, settings));
        }

        [Test]
        public void IsPackageIncluded_RegistryPackageExplicitlyIncluded_ReturnsTrue()
        {
            var settings = new UniClaudeSettings();
            settings.PackageIndexOverrides["com.unity.ugui"] = true;
            var pkg = new PackageInfo { Name = "com.unity.ugui", IsLocal = false };

            Assert.IsTrue(IndexFilterSettings.IsPackageIncluded(pkg, settings));
        }

        [Test]
        public void IsPathExcluded_NoExclusions_ReturnsFalse()
        {
            var settings = new UniClaudeSettings();

            Assert.IsFalse(IndexFilterSettings.IsPathExcluded("Assets/Scripts/Player.cs", settings));
        }

        [Test]
        public void IsPathExcluded_MatchingPrefix_ReturnsTrue()
        {
            var settings = new UniClaudeSettings();
            settings.ExcludedFolders.Add("Assets/ThirdParty");

            Assert.IsTrue(IndexFilterSettings.IsPathExcluded("Assets/ThirdParty/SomeSDK/Foo.cs", settings));
        }

        [Test]
        public void IsPathExcluded_NonMatchingPrefix_ReturnsFalse()
        {
            var settings = new UniClaudeSettings();
            settings.ExcludedFolders.Add("Assets/ThirdParty");

            Assert.IsFalse(IndexFilterSettings.IsPathExcluded("Assets/Scripts/Player.cs", settings));
        }

        [Test]
        public void IsPathExcluded_TrailingSlashNormalized()
        {
            var settings = new UniClaudeSettings();
            settings.ExcludedFolders.Add("Assets/ThirdParty/");

            Assert.IsTrue(IndexFilterSettings.IsPathExcluded("Assets/ThirdParty/Foo.cs", settings));
        }

        [Test]
        public void IsPathExcluded_MultipleExclusions_MatchesAny()
        {
            var settings = new UniClaudeSettings();
            settings.ExcludedFolders.Add("Assets/ThirdParty");
            settings.ExcludedFolders.Add("Assets/Plugins/OldSDK");

            Assert.IsTrue(IndexFilterSettings.IsPathExcluded("Assets/ThirdParty/Foo.cs", settings));
            Assert.IsTrue(IndexFilterSettings.IsPathExcluded("Assets/Plugins/OldSDK/Bar.cs", settings));
            Assert.IsFalse(IndexFilterSettings.IsPathExcluded("Assets/Plugins/GoodSDK/Baz.cs", settings));
        }

        [Test]
        public void IsPackageFolderSkipped_TestsDir_ReturnsTrue()
        {
            Assert.IsTrue(IndexFilterSettings.IsPackageFolderSkipped("Tests"));
            Assert.IsTrue(IndexFilterSettings.IsPackageFolderSkipped("Tests~"));
        }

        [Test]
        public void IsPackageFolderSkipped_SamplesDir_ReturnsTrue()
        {
            Assert.IsTrue(IndexFilterSettings.IsPackageFolderSkipped("Samples"));
            Assert.IsTrue(IndexFilterSettings.IsPackageFolderSkipped("Samples~"));
        }

        [Test]
        public void IsPackageFolderSkipped_RuntimeDir_ReturnsFalse()
        {
            Assert.IsFalse(IndexFilterSettings.IsPackageFolderSkipped("Runtime"));
            Assert.IsFalse(IndexFilterSettings.IsPackageFolderSkipped("Editor"));
        }
    }
}
