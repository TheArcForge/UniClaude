using NUnit.Framework;
using UniClaude.Editor.Installer;

namespace UniClaude.Editor.Tests.Installer
{
    [TestFixture]
    public class InstallerBridgeTests
    {
        [Test]
        public void BuildArgs_ToNinja_OrdersFlagsAndValues()
        {
            var args = InstallerBridge.BuildArgs(
                InstallerBridge.Subcommand.ToNinja,
                projectRoot: "/proj",
                gitUrl: "https://example.com/repo.git");
            CollectionAssert.AreEqual(
                new[] { "to-ninja", "--project-root", "/proj", "--git-url", "https://example.com/repo.git" },
                args);
        }

        [Test]
        public void BuildArgs_ToStandard_HasProjectRootAndUnityInfo()
        {
            var args = InstallerBridge.BuildArgs(
                InstallerBridge.Subcommand.ToStandard,
                projectRoot: "/proj",
                gitUrl: null);
            Assert.AreEqual("to-standard", args[0]);
            Assert.AreEqual("--project-root", args[1]);
            Assert.AreEqual("/proj", args[2]);
            Assert.AreEqual("--unity-pid", args[3]);
            Assert.IsTrue(int.TryParse(args[4], out _), "unity-pid is an integer");
            Assert.AreEqual("--unity-app-path", args[5]);
            Assert.IsFalse(string.IsNullOrEmpty(args[6]), "unity-app-path is present");
        }
    }
}
