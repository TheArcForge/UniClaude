using System;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace UniClaude.Editor.MCP
{
    /// <summary>
    /// MCP tools for project-level operations: run tests, read console log,
    /// query project settings, and refresh the AssetDatabase.
    /// </summary>
    public static class ProjectTools
    {
        /// <summary>
        /// Runs unit tests matching an optional filter and returns pass/fail/skip counts
        /// with failure details. Uses the TestRunnerApi to execute tests on the main thread.
        /// </summary>
        /// <param name="filter">Optional test name filter (e.g. "MyTests" or "MyNamespace.MyClass.MyMethod"). Runs all tests if empty.</param>
        /// <param name="testMode">Test mode: "EditMode", "PlayMode", or "All" (default "EditMode").</param>
        /// <returns>Test results with pass/fail/skip counts and failure messages.</returns>
        [MCPTool("project_run_tests", "Run unit tests with optional name filter. Returns pass/fail/skip counts and failure details.")]
        public static MCPToolResult RunTests(
            [MCPToolParam("Test name filter (e.g. 'MyTests'). Empty = all tests.")] string filter,
            [MCPToolParam("Test mode: EditMode, PlayMode, or All (default EditMode)")] string testMode)
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();

            var mode = TestMode.EditMode;
            if (!string.IsNullOrEmpty(testMode))
            {
                switch (testMode.ToLowerInvariant())
                {
                    case "playmode":
                        mode = TestMode.PlayMode;
                        break;
                    case "all":
                        mode = TestMode.EditMode | TestMode.PlayMode;
                        break;
                }
            }

            var executionFilter = new Filter
            {
                testMode = mode
            };

            if (!string.IsNullOrEmpty(filter))
                executionFilter.testNames = new[] { filter };

            var callbacks = new TestCallbacks();
            api.RegisterCallbacks(callbacks);

            try
            {
                api.Execute(new ExecutionSettings(executionFilter));

                // TestRunnerApi.Execute for EditMode runs synchronously on the main thread,
                // so results are available immediately after Execute returns.
                var results = callbacks.Results;

                if (results == null)
                {
                    return MCPToolResult.Success(new
                    {
                        status = "executed",
                        note = "Tests were dispatched. EditMode tests run synchronously; PlayMode tests run asynchronously and results may not be captured here."
                    });
                }

                var passed = results.PassCount;
                var failed = results.FailCount;
                var skipped = results.SkipCount;
                var total = passed + failed + skipped;

                var failures = callbacks.Failures
                    .Select(f => new { name = f.name, message = f.message })
                    .ToArray();

                return MCPToolResult.Success(new
                {
                    total,
                    passed,
                    failed,
                    skipped,
                    failures,
                    duration = results.Duration
                });
            }
            finally
            {
                api.UnregisterCallbacks(callbacks);
                UnityEngine.Object.DestroyImmediate(api);
            }
        }

        /// <summary>
        /// Returns recent entries from the Unity console log ring buffer.
        /// </summary>
        /// <param name="count">Number of recent entries to return (default 50, max 200).</param>
        /// <param name="typeFilter">Optional filter: "Error", "Warning", "Log", or empty for all types.</param>
        /// <returns>Array of log entries with type, message, and stack trace.</returns>
        [MCPTool("project_get_console_log", "Get recent Unity console log entries with optional type filter (Error, Warning, Log)")]
        public static MCPToolResult GetConsoleLog(
            [MCPToolParam("Number of entries to return (default 50, max 200)")] string count,
            [MCPToolParam("Filter by type: Error, Warning, Log, or empty for all")] string typeFilter)
        {
            int n = 50;
            if (!string.IsNullOrEmpty(count) && int.TryParse(count, out var parsed))
                n = Math.Max(1, Math.Min(parsed, 200));

            var entries = ConsoleLogBuffer.GetRecent(n);

            LogType? filterType = null;
            if (!string.IsNullOrEmpty(typeFilter))
            {
                switch (typeFilter.ToLowerInvariant())
                {
                    case "error":
                        filterType = LogType.Error;
                        break;
                    case "warning":
                        filterType = LogType.Warning;
                        break;
                    case "log":
                        filterType = LogType.Log;
                        break;
                }
            }

            var filtered = filterType.HasValue
                ? entries.Where(e => e.type == filterType.Value).ToArray()
                : entries;

            var result = filtered.Select(e => new
            {
                type = e.type.ToString(),
                message = e.message,
                stackTrace = TruncateStackTrace(e.stackTrace)
            }).ToArray();

            return MCPToolResult.Success(new
            {
                entries = result,
                count = result.Length,
                totalBuffered = entries.Length
            });
        }

        /// <summary>
        /// Reads a named project setting from PlayerSettings or EditorSettings via reflection.
        /// Supports dot notation (e.g. "PlayerSettings.productName", "EditorSettings.serializationMode").
        /// </summary>
        /// <param name="settingName">The setting to read (e.g. "PlayerSettings.productName" or just "productName").</param>
        /// <returns>The setting value, or an error if not found.</returns>
        [MCPTool("project_get_settings", "Read a project setting by name. " +
            "Important settings to check early: 'PlayerSettings.activeInputHandler' (0=Both, 1=Input Manager/Legacy, 2=Input System/New), " +
            "'PlayerSettings.colorSpace' (Linear/Gamma), 'PlayerSettings.scriptingBackend', " +
            "'EditorSettings.serializationMode'. Examples: 'PlayerSettings.productName', 'PlayerSettings.activeInputHandler'.")]
        public static MCPToolResult GetProjectSettings(
            [MCPToolParam("Setting name, e.g. 'PlayerSettings.productName' or 'productName'", required: true)]
            string settingName)
        {
            if (string.IsNullOrWhiteSpace(settingName))
                return MCPToolResult.Error("Setting name is required.");

            // Parse "Class.Property" or just "Property"
            string className;
            string propertyName;

            var dotIndex = settingName.LastIndexOf('.');
            if (dotIndex >= 0)
            {
                className = settingName.Substring(0, dotIndex);
                propertyName = settingName.Substring(dotIndex + 1);
            }
            else
            {
                className = "PlayerSettings";
                propertyName = settingName;
            }

            // Try to find the settings class
            Type settingsType = null;
            switch (className)
            {
                case "PlayerSettings":
                    settingsType = typeof(PlayerSettings);
                    break;
                case "EditorSettings":
                    settingsType = typeof(EditorSettings);
                    break;
                default:
                    // Try to find by name in UnityEditor namespace
                    settingsType = typeof(EditorSettings).Assembly.GetType("UnityEditor." + className);
                    break;
            }

            if (settingsType == null)
                return MCPToolResult.Error(
                    $"Settings class '{className}' not found. Supported: PlayerSettings, EditorSettings.");

            // Try static property
            var prop = settingsType.GetProperty(propertyName,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (prop != null)
            {
                var value = prop.GetValue(null);
                return MCPToolResult.Success(new
                {
                    setting = settingName,
                    value = value?.ToString(),
                    type = prop.PropertyType.Name
                });
            }

            // Try static field
            var field = settingsType.GetField(propertyName,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (field != null)
            {
                var value = field.GetValue(null);
                return MCPToolResult.Success(new
                {
                    setting = settingName,
                    value = value?.ToString(),
                    type = field.FieldType.Name
                });
            }

            // List available properties as suggestions
            var available = settingsType
                .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Select(p => p.Name)
                .OrderBy(n => n)
                .Take(30)
                .ToArray();

            return MCPToolResult.Error(
                $"Property '{propertyName}' not found on {className}.\n\nAvailable properties (first 30):\n  " +
                string.Join("\n  ", available));
        }

        /// <summary>
        /// Forces a full refresh of the Unity AssetDatabase.
        /// </summary>
        /// <returns>Confirmation that the refresh was triggered.</returns>
        [MCPTool("project_refresh_assets", "Force a full refresh of the Unity AssetDatabase")]
        public static MCPToolResult RefreshAssetDatabase()
        {
            AssetDatabase.Refresh();
            return MCPToolResult.Success(new { refreshed = true });
        }

        /// <summary>
        /// Truncates a stack trace to the first 5 lines to keep results concise.
        /// </summary>
        /// <param name="stackTrace">The full stack trace string.</param>
        /// <returns>The truncated stack trace.</returns>
        static string TruncateStackTrace(string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace)) return "";
            var lines = stackTrace.Split('\n');
            if (lines.Length <= 5) return stackTrace;
            return string.Join("\n", lines.Take(5)) + "\n... (truncated)";
        }

        /// <summary>
        /// Callbacks for TestRunnerApi that capture test run results and individual failures.
        /// </summary>
        class TestCallbacks : ICallbacks
        {
            /// <summary>Gets the overall test run results, or null if not yet completed.</summary>
            public ITestResultAdaptor Results { get; private set; }

            /// <summary>Gets the list of individual test failure details.</summary>
            public System.Collections.Generic.List<(string name, string message)> Failures { get; }
                = new System.Collections.Generic.List<(string, string)>();

            public void RunStarted(ITestAdaptor testsToRun) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                Results = result;
            }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.TestStatus == TestStatus.Failed)
                {
                    Failures.Add((result.Test.FullName, result.Message ?? "No message"));
                }
            }
        }
    }

    /// <summary>
    /// Thread-safe ring buffer that captures Unity console log messages.
    /// Persists across domain reloads via SessionState.
    /// Subscribes eagerly at Editor startup via [InitializeOnLoadMethod].
    /// </summary>
    public static class ConsoleLogBuffer
    {
        const int BufferSize = 200;
        const string SessionStateKey = "UniClaude.ConsoleBuffer";

        static (LogType type, string message, string stackTrace)[] _buffer
            = new (LogType, string, string)[BufferSize];
        static int _head;
        static int _count;
        static bool _subscribed;
        static bool _dirty;

        /// <summary>
        /// Initializes the buffer: restores from SessionState and subscribes to log events.
        /// Called automatically via [InitializeOnLoadMethod] and safe to call manually.
        /// </summary>
        [InitializeOnLoadMethod]
        public static void Initialize()
        {
            RestoreFromSessionState();

            if (!_subscribed)
            {
                _subscribed = true;
                Application.logMessageReceivedThreaded += OnLogReceived;
                EditorApplication.update += FlushToSessionState;
            }
        }

        static void OnLogReceived(string message, string stackTrace, LogType type)
        {
            lock (_buffer)
            {
                _buffer[_head] = (type, message, stackTrace);
                _head = (_head + 1) % _buffer.Length;
                if (_count < _buffer.Length) _count++;
                _dirty = true;
            }
        }

        /// <summary>
        /// Called once per Editor frame. Writes buffer to SessionState if dirty.
        /// </summary>
        static void FlushToSessionState()
        {
            if (!_dirty) return;
            _dirty = false;
            SaveToSessionState();
        }

        /// <summary>
        /// Returns the most recent log entries, ordered oldest to newest.
        /// </summary>
        /// <param name="count">Maximum number of entries to return.</param>
        /// <returns>Array of log entries ordered oldest-first.</returns>
        public static (LogType type, string message, string stackTrace)[] GetRecent(int count)
        {
            lock (_buffer)
            {
                var n = Math.Min(count, _count);
                var result = new (LogType, string, string)[n];
                for (int i = 0; i < n; i++)
                {
                    var idx = (_head - n + i + _buffer.Length) % _buffer.Length;
                    result[i] = _buffer[idx];
                }
                return result;
            }
        }

        /// <summary>
        /// Serializes the current buffer to SessionState for domain reload survival.
        /// </summary>
        internal static void SaveToSessionState()
        {
            lock (_buffer)
            {
                var entries = GetRecent(_count);
                var jsonEntries = new string[entries.Length];
                for (int i = 0; i < entries.Length; i++)
                {
                    var e = entries[i];
                    // Manual JSON to avoid Newtonsoft dependency in this hot path
                    var msg = EscapeJson(e.message ?? "");
                    var st = EscapeJson(TruncateForStorage(e.stackTrace ?? ""));
                    jsonEntries[i] = $"{{\"t\":{(int)e.type},\"m\":\"{msg}\",\"s\":\"{st}\"}}";
                }
                var json = "[" + string.Join(",", jsonEntries) + "]";
                SessionState.SetString(SessionStateKey, json);
            }
        }

        /// <summary>
        /// Restores the buffer from SessionState.
        /// </summary>
        internal static void RestoreFromSessionState()
        {
            var json = SessionState.GetString(SessionStateKey, "");
            if (string.IsNullOrEmpty(json) || json == "[]")
                return;

            try
            {
                // Minimal JSON array parsing — entries are {t:int, m:string, s:string}
                var entries = JsonConvert.DeserializeObject<LogEntry[]>(json);
                if (entries == null) return;

                lock (_buffer)
                {
                    foreach (var e in entries)
                    {
                        _buffer[_head] = ((LogType)e.t, e.m, e.s);
                        _head = (_head + 1) % _buffer.Length;
                        if (_count < _buffer.Length) _count++;
                    }
                }
            }
            catch (Exception)
            {
                // Corrupted session state — start fresh
            }
        }

        /// <summary>
        /// Clears the in-memory buffer. Used for testing.
        /// </summary>
        internal static void ClearBuffer()
        {
            lock (_buffer)
            {
                Array.Clear(_buffer, 0, _buffer.Length);
                _head = 0;
                _count = 0;
            }
        }

        static string EscapeJson(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");

        static string TruncateForStorage(string s) =>
            s.Length > 500 ? s.Substring(0, 500) : s;

        [Serializable]
        class LogEntry
        {
            public int t;
            public string m;
            public string s;
        }
    }
}
