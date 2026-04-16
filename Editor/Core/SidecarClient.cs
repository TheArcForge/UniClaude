using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UniClaude.Editor.UI;

namespace UniClaude.Editor
{
    /// <summary>
    /// Parsed SSE event from the sidecar.
    /// </summary>
    public class SidecarEvent
    {
        public string Type;       // token, phase, permission_request, tool_executed, assistant_text, result, error, plan_mode, prompt_suggestion, tool_activity, task, tool_progress
        public string Text;       // token text, result text, or error message
        public string Phase;      // thinking, writing, tool_use
        public string Tool;       // tool name (for phase or permission_request)
        public string Id;         // permission request ID
        public JObject Input;     // tool input (for permission_request)
        public string SessionId;  // session ID (for result)
        public int InputTokens;   // (for result)
        public int OutputTokens;  // (for result)
        public float? CostUsd;    // (for result)
        public bool Success;      // (for tool_executed)
        public bool Active;       // (for plan_mode)
        public string Suggestion; // (for prompt_suggestion)

        // tool_activity fields
        public string ToolUseId;    // (for tool_activity, tool_progress)
        public string ToolName;     // (for tool_activity, tool_progress) — distinct from Tool (phase/permission)
        public string InputJson;    // (for tool_activity) — raw JSON of tool input
        public string ParentTaskId; // (for tool_activity, tool_progress)

        // task fields
        public string TaskId;       // (for task)
        public string Status;       // (for task) — started, progress, completed, failed, stopped
        public string Description;  // (for task)
        public string Error;        // (for task)

        // tool_progress fields
        public float ElapsedSeconds; // (for tool_progress)
    }

    /// <summary>
    /// Result returned by <see cref="SidecarClient.Undo"/>.
    /// </summary>
    public class UndoResult
    {
        /// <summary>Whether the undo operation succeeded.</summary>
        public bool Success;

        /// <summary>Human-readable message describing the result.</summary>
        public string Message;
    }

    /// <summary>
    /// HTTP + SSE client for communicating with the Node.js sidecar.
    /// </summary>
    public class SidecarClient : IDisposable
    {
        static readonly HttpClient _http = new();

        readonly int _port;
        CancellationTokenSource _sseCts;
        bool _disposed;
        string _lastEventId;

        /// <summary>Last received SSE event ID, used for reconnection replay.</summary>
        public string LastEventId
        {
            get => _lastEventId;
            set => _lastEventId = value;
        }

        public event Action<string> OnToken;
        public event Action<string> OnAssistantText;
        public event Action<string, string> OnPhaseChanged;
        public event Action<SidecarEvent> OnPermissionRequest;
        public event Action<string, string, bool> OnToolExecuted;
        public event Action<SidecarEvent> OnResult;
        public event Action<string> OnError;
        public event Action OnDisconnected;
        public event Action<bool> OnPlanModeChanged;
        public event Action<string> OnPromptSuggestion;
        public event Action<SidecarEvent> OnToolActivity;
        public event Action<SidecarEvent> OnTaskEvent;
        public event Action<SidecarEvent> OnToolProgress;

        string BaseUrl => $"http://127.0.0.1:{_port}";

        /// <summary>
        /// Initializes a new <see cref="SidecarClient"/> targeting the given port.
        /// </summary>
        /// <param name="port">The port the Node.js sidecar is listening on.</param>
        public SidecarClient(int port)
        {
            _port = port;
        }

        /// <summary>
        /// Subscribes to the SSE event stream. Call before StartChat.
        /// </summary>
        public void ConnectStream()
        {
            _sseCts?.Cancel();
            _sseCts = new CancellationTokenSource();
            _ = ReadSSEStream(_sseCts.Token);
        }

        /// <summary>
        /// Starts a new chat query. Events arrive via the SSE stream.
        /// </summary>
        /// <param name="message">The user message to send.</param>
        /// <param name="model">The Claude model identifier.</param>
        /// <param name="effort">The effort level (e.g. "normal", "high").</param>
        /// <param name="sessionId">Session ID for conversation continuity, or null for a new session.</param>
        /// <param name="systemPrompt">Optional system prompt to prepend.</param>
        /// <param name="autoAllowMCPTools">When true, UniClaude MCP tools are approved without prompting.</param>
        /// <param name="planMode">When true, the agent proposes actions without executing them.</param>
        /// <param name="attachments">Optional list of file attachments to include with the message.</param>
        public async Task StartChat(string message, string model, string effort,
            string sessionId, string systemPrompt, bool autoAllowMCPTools = false,
            bool planMode = false, List<SidecarAttachment> attachments = null)
        {
            var body = new JObject
            {
                ["message"] = message,
                ["model"] = model,
                ["effort"] = effort,
                ["sessionId"] = sessionId,
                ["systemPrompt"] = systemPrompt,
                ["autoAllowMCPTools"] = autoAllowMCPTools,
                ["planMode"] = planMode,
                ["projectDir"] = System.IO.Path.GetDirectoryName(Application.dataPath),
            };

            if (attachments != null && attachments.Count > 0)
            {
                var arr = new JArray();
                foreach (var att in attachments)
                {
                    var obj = new JObject
                    {
                        ["type"] = att.Type,
                        ["fileName"] = att.FileName,
                        ["content"] = att.Content,
                    };
                    if (!string.IsNullOrEmpty(att.MediaType))
                        obj["mediaType"] = att.MediaType;
                    arr.Add(obj);
                }
                body["attachments"] = arr;
            }

            var content = new StringContent(body.ToString(), Encoding.UTF8, "application/json");
            var response = await _http.PostAsync($"{BaseUrl}/chat", content);

            if ((int)response.StatusCode == 409)
                throw new InvalidOperationException("A query is already active");

            response.EnsureSuccessStatusCode();
        }

        /// <summary>Approve a pending permission request.</summary>
        /// <param name="id">The permission request ID to approve.</param>
        /// <param name="trustForSession">If true, trust this tool for the remainder of the session.</param>
        public async Task Approve(string id, bool trustForSession, string answer = null)
        {
            var body = new JObject
            {
                ["id"] = id,
                ["type"] = trustForSession ? "allow_session" : "allow",
            };
            if (answer != null)
                body["answer"] = answer;
            var content = new StringContent(body.ToString(), Encoding.UTF8, "application/json");
            await _http.PostAsync($"{BaseUrl}/approve", content);
        }

        /// <summary>Deny a pending permission request.</summary>
        /// <param name="id">The permission request ID to deny.</param>
        public async Task Deny(string id)
        {
            var body = new JObject { ["id"] = id };
            var content = new StringContent(body.ToString(), Encoding.UTF8, "application/json");
            await _http.PostAsync($"{BaseUrl}/deny", content);
        }

        /// <summary>Returns true if the sidecar has an active query in progress.</summary>
        public async Task<bool> IsQueryActive()
        {
            try
            {
                var response = await _http.GetAsync($"{BaseUrl}/health");
                var json = await response.Content.ReadAsStringAsync();
                var obj = JObject.Parse(json);
                return obj["query_active"]?.Value<bool>() ?? false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Cancel the active query (abort generation).</summary>
        public async Task Cancel()
        {
            await _http.PostAsync($"{BaseUrl}/cancel", null);
        }

        /// <summary>Revert file changes made during the last agent turn.</summary>
        /// <returns>An <see cref="UndoResult"/> indicating success and a human-readable message.</returns>
        public async Task<UndoResult> Undo()
        {
            var response = await _http.PostAsync($"{BaseUrl}/undo", null);
            var json = await response.Content.ReadAsStringAsync();

            try
            {
                var obj = JObject.Parse(json);
                return new UndoResult
                {
                    Success = obj["success"]?.Value<bool>() ?? false,
                    Message = obj["message"]?.ToString() ?? "",
                };
            }
            catch
            {
                return new UndoResult { Success = false, Message = "Failed to parse undo response" };
            }
        }

        // ── SSE Stream Reader ──

        async Task ReadSSEStream(CancellationToken ct)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/stream");
                if (!string.IsNullOrEmpty(_lastEventId))
                    request.Headers.TryAddWithoutValidation("Last-Event-ID", _lastEventId);

                var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                var stream = await response.Content.ReadAsStreamAsync();
                var reader = new System.IO.StreamReader(stream);
                var buffer = "";

                while (!ct.IsCancellationRequested)
                {
                    var chunk = new char[4096];
                    var read = await reader.ReadAsync(chunk, 0, chunk.Length);
                    if (read == 0) break;

                    buffer += new string(chunk, 0, read);
                    var events = SplitSSEBuffer(ref buffer, ref _lastEventId);

                    foreach (var data in events)
                    {
                        var evt = ParseSSEData(data);
                        if (evt != null)
                            DispatchEvent(evt);
                    }
                }
            }
            catch (OperationCanceledException) { /* Expected on disconnect */ }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UniClaude] SSE stream error: {ex.Message}");
                OnError?.Invoke($"Connection lost: {ex.Message}");
                OnDisconnected?.Invoke();
            }
        }

        void DispatchEvent(SidecarEvent evt)
        {
            switch (evt.Type)
            {
                case "token":
                    OnToken?.Invoke(evt.Text);
                    break;
                case "phase":
                    OnPhaseChanged?.Invoke(evt.Phase, evt.Tool);
                    break;
                case "permission_request":
                    OnPermissionRequest?.Invoke(evt);
                    break;
                case "tool_executed":
                    OnToolExecuted?.Invoke(evt.Tool, evt.Text, evt.Success);
                    break;
                case "assistant_text":
                    OnAssistantText?.Invoke(evt.Text);
                    break;
                case "result":
                    OnResult?.Invoke(evt);
                    break;
                case "error":
                    OnError?.Invoke(evt.Text);
                    break;
                case "plan_mode":
                    OnPlanModeChanged?.Invoke(evt.Active);
                    break;
                case "prompt_suggestion":
                    OnPromptSuggestion?.Invoke(evt.Suggestion);
                    break;
                case "tool_activity":
                    OnToolActivity?.Invoke(evt);
                    break;
                case "task":
                    OnTaskEvent?.Invoke(evt);
                    break;
                case "tool_progress":
                    OnToolProgress?.Invoke(evt);
                    break;
            }
        }

        // ── SSE Parsing (public static for testability) ──

        /// <summary>
        /// Parses a JSON SSE data payload into a <see cref="SidecarEvent"/>.
        /// Returns null if the string is empty, invalid JSON, or missing a type field.
        /// </summary>
        /// <param name="json">The raw JSON string from an SSE data line.</param>
        /// <returns>A populated <see cref="SidecarEvent"/>, or null on failure.</returns>
        public static SidecarEvent ParseSSEData(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;

            JObject obj;
            try { obj = JObject.Parse(json); }
            catch { return null; }

            var type = obj["type"]?.ToString();
            if (string.IsNullOrEmpty(type)) return null;

            var evt = new SidecarEvent { Type = type };

            switch (type)
            {
                case "token":
                    evt.Text = obj["text"]?.ToString();
                    break;
                case "phase":
                    evt.Phase = obj["phase"]?.ToString();
                    evt.Tool = obj["tool"]?.ToString();
                    break;
                case "permission_request":
                    evt.Id = obj["id"]?.ToString();
                    evt.Tool = obj["tool"]?.ToString();
                    evt.Input = obj["input"] as JObject;
                    break;
                case "tool_executed":
                    evt.Tool = obj["tool"]?.ToString();
                    evt.Text = obj["result"]?.ToString();
                    evt.Success = obj["success"]?.Value<bool>() ?? false;
                    break;
                case "assistant_text":
                    evt.Text = obj["text"]?.ToString();
                    break;
                case "result":
                    evt.Text = obj["text"]?.ToString();
                    evt.SessionId = obj["session_id"]?.ToString();
                    var usage = obj["usage"];
                    evt.InputTokens = usage?["input"]?.Value<int>() ?? 0;
                    evt.OutputTokens = usage?["output"]?.Value<int>() ?? 0;
                    evt.CostUsd = obj["cost_usd"]?.Value<float>();
                    break;
                case "error":
                    evt.Text = obj["message"]?.ToString();
                    break;
                case "plan_mode":
                    evt.Active = obj["active"]?.Value<bool>() ?? false;
                    break;
                case "prompt_suggestion":
                    evt.Suggestion = obj["suggestion"]?.ToString();
                    break;
                case "tool_activity":
                    evt.ToolUseId = obj["toolUseId"]?.ToString();
                    evt.ToolName = obj["toolName"]?.ToString();
                    evt.InputJson = obj["input"]?.ToString() ?? "{}";
                    evt.ParentTaskId = obj["parentTaskId"]?.ToString();
                    break;
                case "task":
                    evt.TaskId = obj["taskId"]?.ToString();
                    evt.Status = obj["status"]?.ToString();
                    evt.Description = obj["description"]?.ToString();
                    evt.Error = obj["error"]?.ToString();
                    break;
                case "tool_progress":
                    evt.ToolUseId = obj["toolUseId"]?.ToString();
                    evt.ToolName = obj["toolName"]?.ToString();
                    evt.ElapsedSeconds = obj["elapsedSeconds"]?.Value<float>() ?? 0f;
                    evt.ParentTaskId = obj["parentTaskId"]?.ToString();
                    break;
            }

            return evt;
        }

        /// <summary>
        /// Splits accumulated SSE stream bytes into fully-received event data strings.
        /// Incomplete events remain in <paramref name="buffer"/> for the next call.
        /// SSE comment lines (starting with <c>:</c>) are skipped.
        /// Parses <c>id:</c> lines and updates <paramref name="lastEventId"/> when present.
        /// </summary>
        /// <param name="buffer">The accumulated raw SSE text; consumed in-place as events are extracted.</param>
        /// <param name="lastEventId">Updated to the most recently seen event ID, if any.</param>
        /// <returns>A list of JSON strings, one per complete SSE event.</returns>
        public static List<string> SplitSSEBuffer(ref string buffer, ref string lastEventId)
        {
            var results = new List<string>();
            var separator = "\n\n";

            while (true)
            {
                var idx = buffer.IndexOf(separator, StringComparison.Ordinal);
                if (idx < 0) break;

                var block = buffer.Substring(0, idx).Trim();
                buffer = buffer.Substring(idx + separator.Length);

                // Skip SSE comments (lines starting with :)
                if (block.StartsWith(":")) continue;

                // Parse multi-line SSE block for id: and data: fields
                string data = null;
                foreach (var line in block.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("id: "))
                        lastEventId = trimmed.Substring(4);
                    else if (trimmed.StartsWith("id:"))
                        lastEventId = trimmed.Substring(3);
                    else if (trimmed.StartsWith("data: "))
                        data = trimmed.Substring(6);
                    else if (trimmed.StartsWith("data:"))
                        data = trimmed.Substring(5);
                }

                if (data != null)
                    results.Add(data);
            }

            return results;
        }

        /// <summary>Backward-compatible overload that ignores event IDs.</summary>
        /// <param name="buffer">The accumulated raw SSE text; consumed in-place as events are extracted.</param>
        /// <returns>A list of JSON strings, one per complete SSE event.</returns>
        public static List<string> SplitSSEBuffer(ref string buffer)
        {
            string ignored = null;
            return SplitSSEBuffer(ref buffer, ref ignored);
        }

        /// <summary>
        /// Cancels the SSE stream and releases resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _sseCts?.Cancel();
            _sseCts?.Dispose();
        }
    }
}
