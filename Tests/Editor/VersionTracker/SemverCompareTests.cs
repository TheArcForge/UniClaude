using NUnit.Framework;
using UniClaude.Editor.VersionTracker;

namespace UniClaude.Editor.Tests.VersionTracker
{
    [TestFixture]
    public class SemverCompareTests
    {
        [TestCase("v0.3.0", 0, 3, 0)]
        [TestCase("0.3.0", 0, 3, 0)]
        [TestCase("V1.2.3", 1, 2, 3)]
        [TestCase("v10.20.30", 10, 20, 30)]
        public void TryParse_Valid(string input, int major, int minor, int patch)
        {
            Assert.IsTrue(SemverCompare.TryParse(input, out var v));
            Assert.AreEqual(major, v.Major);
            Assert.AreEqual(minor, v.Minor);
            Assert.AreEqual(patch, v.Patch);
        }

        [TestCase("")]
        [TestCase(null)]
        [TestCase("abc")]
        [TestCase("1.2")]
        [TestCase("1.2.3.4")]
        [TestCase("v1.2.3-beta")]
        public void TryParse_Invalid(string input)
        {
            Assert.IsFalse(SemverCompare.TryParse(input, out _));
        }

        [Test] public void Compare_Newer_PatchBump()   => Assert.IsTrue(SemverCompare.IsNewer("v0.2.1", "v0.2.0"));
        [Test] public void Compare_Newer_MinorBump()   => Assert.IsTrue(SemverCompare.IsNewer("v0.3.0", "v0.2.9"));
        [Test] public void Compare_Newer_MajorBump()   => Assert.IsTrue(SemverCompare.IsNewer("v1.0.0", "v0.99.99"));
        [Test] public void Compare_Equal_NotNewer()    => Assert.IsFalse(SemverCompare.IsNewer("v0.2.0", "v0.2.0"));
        [Test] public void Compare_Older_NotNewer()    => Assert.IsFalse(SemverCompare.IsNewer("v0.1.0", "v0.2.0"));
        [Test] public void Compare_MixedPrefix_Equal() => Assert.IsFalse(SemverCompare.IsNewer("0.2.0", "v0.2.0"));

        [Test]
        public void IsNewer_UnparseableLatest_ReturnsFalse()
        {
            Assert.IsFalse(SemverCompare.IsNewer("garbage", "v0.2.0"));
        }
    }
}
