using System.Linq;
using UnityEditor.Compilation;
using UnityEngine;

namespace UniClaude.Editor.MCP
{
    /// <summary>
    /// Meta-tools for manual domain reload control.
    /// BeginScriptEditing locks assembly reload, EndScriptEditing unlocks and triggers compilation.
    /// Only effective when ManualReloadStrategy is active; gracefully no-ops for AutoReloadStrategy.
    /// </summary>
    public static class DomainReloadTools
    {
        /// <summary>
        /// Locks domain reload to prevent Unity from recompiling while scripts are being modified in a batch.
        /// Call <see cref="EndScriptEditing"/> when done editing to unlock and trigger compilation.
        /// No-ops gracefully when the Auto reload strategy is active.
        /// </summary>
        /// <returns>Confirmation that reload is locked, or a note that Auto strategy handles it.</returns>
        [MCPTool("BeginScriptEditing", "Lock domain reload before modifying scripts in a batch. Call EndScriptEditing when done.")]
        public static MCPToolResult BeginScriptEditing()
        {
            var strategy = MCPServer.Instance?.ActiveReloadStrategy as ManualReloadStrategy;
            if (strategy == null)
                return MCPToolResult.Success("Domain reload strategy is Auto — lock is managed automatically. No action needed.");

            strategy.Lock();
            return MCPToolResult.Success("Domain reload locked. Modify scripts freely, then call EndScriptEditing.");
        }

        /// <summary>
        /// Unlocks domain reload and triggers script compilation.
        /// Returns compilation status — checks for compile errors and reports them.
        /// In Auto mode, force-unlocks any held lock and triggers an asset refresh.
        /// </summary>
        /// <returns>Compilation result with error details if any.</returns>
        [MCPTool("EndScriptEditing", "Unlock domain reload and trigger compilation. Returns compile result.")]
        public static MCPToolResult EndScriptEditing()
        {
            var strategy = MCPServer.Instance?.ActiveReloadStrategy as ManualReloadStrategy;
            if (strategy == null)
            {
                // Auto mode: force-unlock in case auto-lock is still held, then trigger refresh
                UnityEditor.EditorApplication.UnlockReloadAssemblies();
                UnityEditor.AssetDatabase.Refresh();
                return MCPToolResult.Success("Domain reload unlocked. Scripts will recompile now.");
            }

            strategy.Unlock();

            CompilationPipeline.RequestScriptCompilation();

            if (UnityEditor.EditorUtility.scriptCompilationFailed)
            {
                var recentLogs = ConsoleLogBuffer.GetRecent(50);
                var errors = recentLogs
                    .Where(l => l.type == LogType.Error &&
                                (l.message.Contains("error CS") || l.message.Contains("CompilerError")))
                    .Select(l => l.message)
                    .ToArray();

                return MCPToolResult.Error(
                    $"Compilation failed ({errors.Length} error(s)):\n" +
                    string.Join("\n", errors.Length > 0 ? errors : new[] { "Check Unity console for details." }));
            }

            return MCPToolResult.Success("Domain reload unlocked. Compilation triggered — no errors detected.");
        }

        /// <summary>
        /// Forces script recompilation by unlocking domain reload and refreshing assets.
        /// Returns compilation status with any errors.
        /// </summary>
        /// <returns>Success with compilation status, or error with compile errors.</returns>
        [MCPTool("project_recompile_scripts", "Force script recompilation and return compilation status with any errors")]
        public static MCPToolResult RecompileScripts()
        {
            UnityEditor.EditorApplication.UnlockReloadAssemblies();
            UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.ForceUpdate);

            if (UnityEditor.EditorUtility.scriptCompilationFailed)
            {
                var recentLogs = ConsoleLogBuffer.GetRecent(50);
                var errors = recentLogs
                    .Where(l => l.type == LogType.Error &&
                                (l.message.Contains("error CS") || l.message.Contains("CompilerError")))
                    .Select(l => l.message)
                    .ToArray();

                return MCPToolResult.Error(
                    $"Compilation failed ({errors.Length} error(s)):\n" +
                    string.Join("\n", errors.Length > 0 ? errors : new[] { "Check Unity console for details." }));
            }

            return MCPToolResult.Success("Scripts compiled successfully. No errors.");
        }
    }
}
