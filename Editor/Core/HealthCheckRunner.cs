using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UniClaude.Editor.MCP;
using UniClaude.Editor.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UniClaude.Editor
{
    /// <summary>
    /// Orchestrates health check execution: setup, step-by-step prompt execution,
    /// result collection, teardown, and scorecard generation.
    /// </summary>
    public class HealthCheckRunner
    {
        const string TestFolder = "Assets/UniClaudeHealthCheck";
        const string PingFileName = "ping.txt";
        const float StepTimeoutSeconds = 60f;

        static readonly string SystemPrompt =
            "You are running a UniClaude health check. Execute the requested action using " +
            "exactly one MCP tool call from the uniclaude-unity MCP server. Do NOT use your " +
            "built-in tools (Read, Write, Edit, Bash). Only use the MCP tools. Do not ask " +
            "clarifying questions. Do not explain your reasoning. Just call the tool and " +
            "report the result briefly.";

        readonly SidecarClient _client;
        readonly ChatPanel _chatPanel;
        readonly string _model;
        readonly string _effort;
        readonly List<HealthCheckStep> _steps;
        readonly List<HealthCheckResult> _results = new();

        string _originalScenePath;
        bool _cancelled;

        // Per-step event tracking
        string _lastToolFired;
        bool _lastToolSuccess;
        string _lastToolError;
        bool _turnComplete;
        bool _turnError;
        string _turnErrorMessage;

        /// <summary>
        /// Creates a new health check runner.
        /// </summary>
        /// <param name="client">The connected sidecar client.</param>
        /// <param name="chatPanel">Chat panel for posting status messages.</param>
        /// <param name="model">Current model setting.</param>
        /// <param name="effort">Current effort setting.</param>
        /// <param name="steps">The list of steps to execute.</param>
        public HealthCheckRunner(
            SidecarClient client,
            ChatPanel chatPanel,
            string model,
            string effort,
            List<HealthCheckStep> steps)
        {
            _client = client;
            _chatPanel = chatPanel;
            _model = model;
            _effort = effort;
            _steps = steps;
        }

        /// <summary>
        /// Cancels the health check after the current step finishes.
        /// </summary>
        public void Cancel()
        {
            _cancelled = true;
        }

        /// <summary>
        /// Runs the full health check sequence: setup, execute all steps, teardown, post scorecard.
        /// </summary>
        public async Task RunAsync()
        {
            try
            {
                Setup();
                _chatPanel.AddSystemMessage($"Running health check ({_steps.Count} steps)...\n");

                for (int i = 0; i < _steps.Count; i++)
                {
                    if (_cancelled)
                    {
                        _chatPanel.AddSystemMessage("Health check cancelled.");
                        break;
                    }

                    // Wait for any previous query to fully close before starting next step
                    await WaitForQueryIdle();

                    var result = await ExecuteStep(_steps[i], i + 1);
                    _results.Add(result);
                }

                PostScorecard();
            }
            finally
            {
                Teardown();
            }
        }

        void Setup()
        {
            _originalScenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;

            if (!AssetDatabase.IsValidFolder(TestFolder))
                AssetDatabase.CreateFolder("Assets", "UniClaudeHealthCheck");

            // Create ping file for the first read test
            var pingPath = Path.Combine(
                Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
                TestFolder, PingFileName);
            File.WriteAllText(pingPath, "UniClaude health check ping");
            AssetDatabase.Refresh();

            // Open a fresh scene so tests don't pollute the user's scene
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        void Teardown()
        {
            // Remove test tag if it survived (safety net)
            RemoveTestTag("HCTestTag");

            // Delete test folder
            if (AssetDatabase.IsValidFolder(TestFolder))
                AssetDatabase.DeleteAsset(TestFolder);

            // Restore original scene
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!string.IsNullOrEmpty(_originalScenePath)
                && File.Exists(_originalScenePath))
            {
                EditorSceneManager.OpenScene(_originalScenePath);
            }

            AssetDatabase.Refresh();
        }

        static void RemoveTestTag(string tagName)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset");
            if (asset == null) return;
            var so = new SerializedObject(asset);
            var tags = so.FindProperty("tags");
            for (int i = tags.arraySize - 1; i >= 0; i--)
            {
                if (tags.GetArrayElementAtIndex(i).stringValue == tagName)
                {
                    tags.DeleteArrayElementAtIndex(i);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    return;
                }
            }
        }

        async Task WaitForQueryIdle()
        {
            var deadline = EditorApplication.timeSinceStartup + 10;
            while (EditorApplication.timeSinceStartup < deadline)
            {
                if (!await _client.IsQueryActive())
                    return;
                await Task.Delay(200);
            }
        }

        async Task<HealthCheckResult> ExecuteStep(HealthCheckStep step, int stepNumber)
        {
            var startTime = EditorApplication.timeSinceStartup;

            // Reset per-step state
            _lastToolFired = null;
            _lastToolSuccess = false;
            _lastToolError = null;
            _turnComplete = false;
            _turnError = false;
            _turnErrorMessage = null;

            // Subscribe to events — tool execution comes from the MCP server,
            // not the sidecar SSE stream (which never emits tool_executed).
            var mcpServer = MCPServer.Instance;
            if (mcpServer != null)
                mcpServer.OnToolExecuted += OnToolExecuted;
            _client.OnResult += OnResult;
            _client.OnError += OnError;

            try
            {
                await _client.StartChat(
                    step.Prompt,
                    model: _model,
                    effort: _effort,
                    sessionId: null,
                    systemPrompt: SystemPrompt,
                    autoAllowMCPTools: true,
                    planMode: false);

                // Wait for turn to complete or timeout
                var deadline = EditorApplication.timeSinceStartup + StepTimeoutSeconds;
                while (!_turnComplete && !_turnError
                       && EditorApplication.timeSinceStartup < deadline)
                {
                    await Task.Delay(100);
                }

                var duration = (float)(EditorApplication.timeSinceStartup - startTime);

                // Evaluate result
                if (_turnError)
                {
                    var result = MakeResult(step, false, _lastToolFired,
                        _turnErrorMessage ?? "Turn error", duration);
                    PostStepResult(stepNumber, result);
                    return result;
                }

                if (!_turnComplete)
                {
                    var result = MakeResult(step, false, _lastToolFired,
                        "Timeout", duration);
                    PostStepResult(stepNumber, result);
                    return result;
                }

                if (_lastToolFired == null)
                {
                    var result = MakeResult(step, false, null,
                        "No tool was called", duration);
                    PostStepResult(stepNumber, result);
                    return result;
                }

                if (_lastToolFired != step.ExpectedTool)
                {
                    var result = MakeResult(step, false, _lastToolFired,
                        $"Expected {step.ExpectedTool}, got {_lastToolFired}", duration);
                    PostStepResult(stepNumber, result);
                    return result;
                }

                if (!_lastToolSuccess)
                {
                    var result = MakeResult(step, false, _lastToolFired,
                        _lastToolError ?? "Tool reported failure", duration);
                    PostStepResult(stepNumber, result);
                    return result;
                }

                // Pass
                var passed = MakeResult(step, true, _lastToolFired, null, duration);
                PostStepResult(stepNumber, passed);
                return passed;
            }
            catch (Exception ex)
            {
                var duration = (float)(EditorApplication.timeSinceStartup - startTime);
                var result = MakeResult(step, false, null, ex.Message, duration);
                PostStepResult(stepNumber, result);
                return result;
            }
            finally
            {
                if (mcpServer != null)
                    mcpServer.OnToolExecuted -= OnToolExecuted;
                _client.OnResult -= OnResult;
                _client.OnError -= OnError;
            }
        }

        void OnToolExecuted(string tool, string argsJson, MCPToolResult result)
        {
            _lastToolFired = tool;
            _lastToolSuccess = !result.IsError;
            if (result.IsError)
                _lastToolError = result.Text;
        }

        void OnResult(SidecarEvent evt)
        {
            _turnComplete = true;
        }

        void OnError(string message)
        {
            _turnError = true;
            _turnErrorMessage = message;
        }

        static HealthCheckResult MakeResult(
            HealthCheckStep step, bool passed, string toolFired,
            string errorMessage, float duration)
        {
            return new HealthCheckResult
            {
                Step = step,
                Passed = passed,
                ToolFired = toolFired,
                ErrorMessage = errorMessage,
                Duration = duration,
            };
        }

        void PostStepResult(int stepNumber, HealthCheckResult result)
        {
            string icon;
            string detail;

            if (result.Passed)
            {
                icon = "\u2713"; // checkmark
                detail = "passed";
            }
            else if (result.ErrorMessage == "Timeout")
            {
                icon = "\u2298"; // circled dash
                detail = "timeout";
            }
            else
            {
                icon = "\u2717"; // X mark
                detail = result.ErrorMessage ?? "failed";
            }

            EditorApplication.delayCall += () =>
                _chatPanel.AddSystemMessage(
                    $"  {icon} [{stepNumber}/{_steps.Count}] {result.Step.Name} \u2014 {detail} ({result.Duration:F1}s)");
        }

        void PostScorecard()
        {
            var passed = _results.Count(r => r.Passed);
            var total = _results.Count;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"\nHealth Check Complete \u2014 {passed}/{total} passed\n");

            // Group by category
            var groups = _results
                .GroupBy(r => r.Step.Category)
                .OrderBy(g => g.Key);

            foreach (var group in groups)
            {
                var groupPassed = group.Count(r => r.Passed);
                var groupTotal = group.Count();
                var icon = groupPassed == groupTotal ? "\u2713" : "\u2717";

                var line = $"  {icon} {group.Key,-24} {groupPassed}/{groupTotal}";

                var failures = group.Where(r => !r.Passed).ToList();
                if (failures.Count > 0)
                {
                    var failNames = string.Join(", ",
                        failures.Select(f => $"{f.Step.Name}: {f.ErrorMessage}"));
                    line += $"  \u2014 {failNames}";
                }

                sb.AppendLine(line);
            }

            var totalDuration = _results.Sum(r => r.Duration);
            sb.AppendLine($"\n  Duration: {totalDuration:F0}s");

            EditorApplication.delayCall += () =>
                _chatPanel.AddSystemMessage(sb.ToString());
        }
    }
}
