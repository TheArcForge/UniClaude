using NUnit.Framework;
using UniClaude.Editor.MCP;

namespace UniClaude.Editor.Tests.MCP
{
    /// <summary>
    /// Tests for <see cref="ProjectTools"/> MCP tools covering console log retrieval,
    /// project settings queries, asset database refresh, and test runner invocation.
    /// </summary>
    public class ProjectToolsTests
    {
        // ── GetConsoleLog ──

        /// <summary>
        /// Verifies that GetConsoleLog returns a successful result even when no logs exist.
        /// </summary>
        [Test]
        public void GetConsoleLog_ReturnsSuccess()
        {
            var result = ProjectTools.GetConsoleLog(null, null);

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            Assert.That(result.Text, Does.Contain("\"entries\":"));
            Assert.That(result.Text, Does.Contain("\"count\":"));
        }

        /// <summary>
        /// Verifies that GetConsoleLog respects the count parameter.
        /// </summary>
        [Test]
        public void GetConsoleLog_WithCount_Success()
        {
            var result = ProjectTools.GetConsoleLog("5", null);

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            Assert.That(result.Text, Does.Contain("\"entries\":"));
        }

        /// <summary>
        /// Verifies that GetConsoleLog handles type filter parameter.
        /// </summary>
        [Test]
        public void GetConsoleLog_WithTypeFilter_Success()
        {
            var result = ProjectTools.GetConsoleLog(null, "Error");

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            Assert.That(result.Text, Does.Contain("\"entries\":"));
        }

        // ── ConsoleLogBuffer ──

        /// <summary>
        /// Verifies that ConsoleLogBuffer.GetRecent returns empty array when no messages logged.
        /// </summary>
        [Test]
        public void ConsoleLogBuffer_GetRecent_Empty()
        {
            // Initialize is idempotent
            ConsoleLogBuffer.Initialize();

            // Even if the buffer has data from other tests, requesting 0 should return empty
            var entries = ConsoleLogBuffer.GetRecent(0);
            Assert.AreEqual(0, entries.Length);
        }

        /// <summary>
        /// Verifies that ConsoleLogBuffer.Initialize can be called multiple times safely.
        /// </summary>
        [Test]
        public void ConsoleLogBuffer_Initialize_Idempotent()
        {
            Assert.DoesNotThrow(() =>
            {
                ConsoleLogBuffer.Initialize();
                ConsoleLogBuffer.Initialize();
                ConsoleLogBuffer.Initialize();
            });
        }

        // ── GetProjectSettings ──

        /// <summary>
        /// Verifies that GetProjectSettings reads PlayerSettings.productName successfully.
        /// </summary>
        [Test]
        public void GetProjectSettings_ProductName_ReturnsValue()
        {
            var result = ProjectTools.GetProjectSettings("PlayerSettings.productName");

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            Assert.That(result.Text, Does.Contain("\"setting\":"));
            Assert.That(result.Text, Does.Contain("\"value\":"));
            Assert.That(result.Text, Does.Contain("PlayerSettings.productName"));
        }

        /// <summary>
        /// Verifies that GetProjectSettings defaults to PlayerSettings when no class prefix is given.
        /// </summary>
        [Test]
        public void GetProjectSettings_DefaultsToPlayerSettings()
        {
            var result = ProjectTools.GetProjectSettings("companyName");

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            Assert.That(result.Text, Does.Contain("\"value\":"));
        }

        /// <summary>
        /// Verifies that GetProjectSettings returns an error with suggestions for nonexistent property.
        /// </summary>
        [Test]
        public void GetProjectSettings_NotFound_ReturnsError()
        {
            var result = ProjectTools.GetProjectSettings("PlayerSettings.nonExistentProperty_12345");

            Assert.IsTrue(result.IsError);
            Assert.That(result.Text, Does.Contain("not found"));
            Assert.That(result.Text, Does.Contain("Available properties"));
        }

        /// <summary>
        /// Verifies that GetProjectSettings errors on empty input.
        /// </summary>
        [Test]
        public void GetProjectSettings_EmptyName_ReturnsError()
        {
            var result = ProjectTools.GetProjectSettings("");

            Assert.IsTrue(result.IsError);
            Assert.That(result.Text, Does.Contain("required"));
        }

        /// <summary>
        /// Verifies that GetProjectSettings handles unknown settings class.
        /// </summary>
        [Test]
        public void GetProjectSettings_UnknownClass_ReturnsError()
        {
            var result = ProjectTools.GetProjectSettings("FakeSettings.whatever");

            Assert.IsTrue(result.IsError);
            Assert.That(result.Text, Does.Contain("not found"));
        }

        // ── RefreshAssetDatabase ──

        /// <summary>
        /// Verifies that RefreshAssetDatabase succeeds without error.
        /// </summary>
        [Test]
        public void RefreshAssetDatabase_Success()
        {
            var result = ProjectTools.RefreshAssetDatabase();

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            Assert.That(result.Text, Does.Contain("\"refreshed\": true"));
        }

        // ── RunTests ──

        /// <summary>
        /// Verifies that RunTests with a nonexistent test filter does not crash
        /// and returns a result (either empty results or a status message).
        /// </summary>
        [Test]
        public void RunTests_WithFilter_DoesNotThrow()
        {
            // This test verifies the tool handles gracefully when no tests match.
            // TestRunnerApi may return immediately with zero results.
            Assert.DoesNotThrow(() =>
            {
                var result = ProjectTools.RunTests("NonExistentTest_ZZZZZ_99999", null);
                Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            });
        }
    }
}
