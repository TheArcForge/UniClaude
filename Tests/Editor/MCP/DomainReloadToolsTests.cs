using NUnit.Framework;
using UniClaude.Editor.MCP;

namespace UniClaude.Editor.Tests.MCP
{
    /// <summary>
    /// Tests for <see cref="DomainReloadTools"/> MCP tools.
    /// </summary>
    public class DomainReloadToolsTests
    {
        [Test]
        public void EndScriptEditing_AutoMode_ForcesUnlock()
        {
            // With Auto strategy (default), EndScriptEditing should not return
            // "No action needed" anymore — it should force-unlock.
            var result = DomainReloadTools.EndScriptEditing();

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            StringAssert.DoesNotContain("No action needed", result.Text);
            StringAssert.Contains("unlocked", result.Text.ToLowerInvariant());
        }

        [Test]
        public void RecompileScripts_ReturnsCompilationStatus()
        {
            var result = DomainReloadTools.RecompileScripts();
            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
        }
    }
}
