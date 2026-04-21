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
        public void BuildArgs_ToStandard_OmitsGitUrl()
        {
            var args = InstallerBridge.BuildArgs(
                InstallerBridge.Subcommand.ToStandard,
                projectRoot: "/proj",
                gitUrl: null);
            Assert.Contains("to-standard", args);
            CollectionAssert.DoesNotContain(args, "--git-url");
        }

        [Test]
        public void TransitionKey_Constant()
        {
            Assert.AreEqual("UniClaude.Transition", InstallerBridge.TransitionKey);
        }
    }
}
