using NUnit.Framework;
using UniClaude.Editor.VersionTracker;

namespace UniClaude.Editor.Tests.VersionTracker
{
    [TestFixture]
    public class ManifestEditorTests
    {
        const string PkgName = "com.arcforge.uniclaude";

        [Test]
        public void Inspect_TagPinned_ReturnsTagPinnedKind()
        {
            var json = @"{""dependencies"":{""com.arcforge.uniclaude"":""https://github.com/TheArcForge/UniClaude.git#v0.2.0""}}";
            var r = ManifestEditor.Inspect(json, PkgName);
            Assert.AreEqual(ManifestEditor.EntryKind.TagPinned, r.Kind);
            Assert.AreEqual("v0.2.0", r.CurrentTag);
        }

        [Test]
        public void Inspect_NoTagSuffix_ReturnsFloating()
        {
            var json = @"{""dependencies"":{""com.arcforge.uniclaude"":""https://github.com/TheArcForge/UniClaude.git""}}";
            var r = ManifestEditor.Inspect(json, PkgName);
            Assert.AreEqual(ManifestEditor.EntryKind.Floating, r.Kind);
        }

        [Test]
        public void Inspect_BranchSuffix_ReturnsFloating()
        {
            var json = @"{""dependencies"":{""com.arcforge.uniclaude"":""https://github.com/TheArcForge/UniClaude.git#main""}}";
            var r = ManifestEditor.Inspect(json, PkgName);
            Assert.AreEqual(ManifestEditor.EntryKind.Floating, r.Kind);
        }

        [Test]
        public void Inspect_Missing_ReturnsMissing()
        {
            var json = @"{""dependencies"":{""com.unity.other"":""1.0.0""}}";
            var r = ManifestEditor.Inspect(json, PkgName);
            Assert.AreEqual(ManifestEditor.EntryKind.Missing, r.Kind);
        }

        [Test]
        public void Inspect_SemverRegistry_ReturnsFloating()
        {
            var json = @"{""dependencies"":{""com.arcforge.uniclaude"":""0.2.0""}}";
            var r = ManifestEditor.Inspect(json, PkgName);
            Assert.AreEqual(ManifestEditor.EntryKind.Floating, r.Kind);
        }

        [Test]
        public void RewriteTag_ReplacesTag_PreservesRestOfFile()
        {
            var json = @"{
  ""dependencies"": {
    ""com.unity.ugui"": ""2.0.0"",
    ""com.arcforge.uniclaude"": ""https://github.com/TheArcForge/UniClaude.git#v0.2.0""
  }
}";
            var updated = ManifestEditor.RewriteTag(json, PkgName, "v0.3.0");
            StringAssert.Contains("com.arcforge.uniclaude", updated);
            StringAssert.Contains("UniClaude.git#v0.3.0", updated);
            StringAssert.DoesNotContain("#v0.2.0", updated);
            StringAssert.Contains("\"com.unity.ugui\": \"2.0.0\"", updated);
        }

        [Test]
        public void RewriteTag_NonTagPinned_Throws()
        {
            var json = @"{""dependencies"":{""com.arcforge.uniclaude"":""https://github.com/TheArcForge/UniClaude.git""}}";
            Assert.Throws<System.InvalidOperationException>(
                () => ManifestEditor.RewriteTag(json, PkgName, "v0.3.0"));
        }
    }
}
