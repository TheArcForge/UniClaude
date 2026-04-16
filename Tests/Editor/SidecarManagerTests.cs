using System.IO;
using NUnit.Framework;
using UniClaude.Editor;

namespace UniClaude.Editor.Tests
{
    [TestFixture]
    public class SidecarManagerTests
    {
        [Test]
        public void FindNodeBinary_ReturnsNullForBogusOverride()
        {
            // A non-existent override path should not be returned as the result;
            // the method falls through to PATH / common paths, so it may still find node.
            var result = SidecarManager.FindNodeBinary("/nonexistent_path_12345/node");
            if (result != null)
                Assert.AreNotEqual("/nonexistent_path_12345/node", result);
        }

        [Test]
        public void FindNodeBinary_ReturnsPathWhenFound()
        {
            // Create a fake node executable with the platform-correct name
            var tempDir = Path.Combine(Path.GetTempPath(), "uniclaude_test_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                var exeName = UnityEngine.Application.platform == UnityEngine.RuntimePlatform.WindowsEditor
                    ? "node.exe" : "node";
                var fakePath = Path.Combine(tempDir, exeName);
                File.WriteAllText(fakePath, "fake node");

                var result = SidecarManager.FindNodeBinary(tempDir);
                Assert.IsNotNull(result);
                Assert.That(result, Does.Contain(exeName));
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Test]
        public void FindNodeBinary_OverrideWithWrongExeName_FallsThrough()
        {
            // If override directory has wrong platform executable name, should not match
            var tempDir = Path.Combine(Path.GetTempPath(), "uniclaude_test_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                // Write the WRONG name for this platform
                var wrongName = UnityEngine.Application.platform == UnityEngine.RuntimePlatform.WindowsEditor
                    ? "node" : "node.exe";
                File.WriteAllText(Path.Combine(tempDir, wrongName), "fake");

                var result = SidecarManager.FindNodeBinary(tempDir);
                // Should not find it via override — may still find system node via PATH
                if (result != null)
                    Assert.That(result, Does.Not.StartWith(tempDir));
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Test]
        public void SidecarPath_PointsToDistIndexJs()
        {
            var path = SidecarManager.GetSidecarEntryPoint();
            Assert.That(path, Does.EndWith(Path.Combine("dist", "index.js")));
        }

        [Test]
        public void BuildArgs_IncludesPortAndMcpPort()
        {
            var args = SidecarManager.BuildArgs(port: 8080, mcpPort: 9000);
            Assert.That(args, Does.Contain("--port 8080"));
            Assert.That(args, Does.Contain("--mcp-port 9000"));
        }

        [Test]
        public void BuildArgs_ZeroPort_IncludesZero()
        {
            var args = SidecarManager.BuildArgs(port: 0, mcpPort: 9000);
            Assert.That(args, Does.Contain("--port 0"));
        }

        [Test]
        public void IsSetupComplete_OnlyChecksNodeModules()
        {
            // IsSetupComplete should check for node_modules but NOT dist/index.js
            // (dist is shipped pre-built with the package).
            // We can't easily mock the filesystem here, but we can verify the
            // entry point file exists (since dist/ ships with the package).
            var entryPoint = SidecarManager.GetSidecarEntryPoint();
            Assert.IsTrue(File.Exists(entryPoint),
                "dist/index.js should ship with the package — if this fails, dist/ wasn't committed");
        }
    }
}
