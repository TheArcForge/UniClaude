using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace UniClaude.Editor
{
    /// <summary>
    /// A single tool invocation recorded during an agent turn.
    /// </summary>
    [Serializable]
    public class ToolActivity
    {
        /// <summary>SDK tool_use block ID.</summary>
        public string ToolUseId;

        /// <summary>Tool name (e.g. "Read", "Grep", "Agent").</summary>
        public string ToolName;

        /// <summary>Raw JSON string of tool input parameters.</summary>
        public string InputJson;

        /// <summary>Task ID of the parent subagent, or null for root-level tools.</summary>
        public string ParentTaskId;

        /// <summary>UTC ISO-8601 timestamp.</summary>
        public string Timestamp;
    }

    /// <summary>
    /// A subagent task lifecycle entry.
    /// </summary>
    [Serializable]
    public class TaskActivity
    {
        /// <summary>SDK task identifier.</summary>
        public string TaskId;

        /// <summary>Human-readable description of what the subagent is doing.</summary>
        public string Description;

        /// <summary>Lifecycle status: started, progress, completed, failed, stopped.</summary>
        public string Status;

        /// <summary>Error message if status is "failed".</summary>
        public string Error;

        /// <summary>Tools executed by this subagent, in chronological order.</summary>
        public List<ToolActivity> Children = new();

        /// <summary>UTC ISO-8601 timestamp.</summary>
        public string Timestamp;
    }

    /// <summary>
    /// Tracks all tool and subagent activity during a single agent turn.
    /// Built incrementally from SSE events, then persisted alongside the assistant message.
    /// </summary>
    [Serializable]
    public class ActivityLog
    {
        /// <summary>Root-level tool invocations (not nested under a subagent).</summary>
        public List<ToolActivity> RootTools = new();

        /// <summary>Subagent tasks with their nested tool children.</summary>
        public List<TaskActivity> Tasks = new();

        /// <summary>Runtime-only lookup. Populated during event ingestion and on deserialization.</summary>
        [JsonIgnore]
        public Dictionary<string, TaskActivity> TaskMap = new();

        /// <summary>
        /// Handles a tool_activity SSE event. Nests under the parent task if one exists.
        /// </summary>
        public void HandleToolActivity(string toolUseId, string toolName, string inputJson, string parentTaskId)
        {
            var tool = new ToolActivity
            {
                ToolUseId = toolUseId,
                ToolName = toolName,
                InputJson = inputJson,
                ParentTaskId = parentTaskId,
                Timestamp = DateTime.UtcNow.ToString("o"),
            };

            if (!string.IsNullOrEmpty(parentTaskId) && TaskMap.TryGetValue(parentTaskId, out var parentTask))
            {
                parentTask.Children.Add(tool);
            }
            else
            {
                RootTools.Add(tool);
            }
        }

        /// <summary>
        /// Handles a task SSE event. Creates or updates the task in the log.
        /// </summary>
        public void HandleTaskEvent(string taskId, string status, string description, string error)
        {
            if (status == "started")
            {
                var task = new TaskActivity
                {
                    TaskId = taskId,
                    Description = description,
                    Status = status,
                    Timestamp = DateTime.UtcNow.ToString("o"),
                };
                Tasks.Add(task);
                TaskMap[taskId] = task;
            }
            else if (TaskMap.TryGetValue(taskId, out var existing))
            {
                existing.Status = status;
                if (!string.IsNullOrEmpty(description))
                    existing.Description = description;
                if (!string.IsNullOrEmpty(error))
                    existing.Error = error;
            }
        }

        /// <summary>
        /// Total number of tool invocations (root + nested under tasks).
        /// </summary>
        [JsonIgnore]
        public int TotalToolCount
        {
            get
            {
                int count = RootTools.Count;
                foreach (var task in Tasks)
                    count += task.Children.Count;
                return count;
            }
        }

        /// <summary>
        /// The most recently added tool activity (root or nested), or null if empty.
        /// </summary>
        [JsonIgnore]
        public ToolActivity LatestTool
        {
            get
            {
                ToolActivity latest = null;

                if (RootTools.Count > 0)
                    latest = RootTools[^1];

                foreach (var task in Tasks)
                {
                    if (task.Children.Count > 0)
                    {
                        var child = task.Children[^1];
                        if (latest == null ||
                            string.Compare(child.Timestamp, latest.Timestamp, StringComparison.Ordinal) > 0)
                        {
                            latest = child;
                        }
                    }
                }

                return latest;
            }
        }

        /// <summary>
        /// Rebuilds the runtime <see cref="TaskMap"/> from the persisted <see cref="Tasks"/> list.
        /// Call after deserialization.
        /// </summary>
        public void RebuildTaskMap()
        {
            TaskMap.Clear();
            foreach (var task in Tasks)
                TaskMap[task.TaskId] = task;
        }

        /// <summary>
        /// Returns true if the log has any tool or task entries.
        /// </summary>
        [JsonIgnore]
        public bool HasEntries => RootTools.Count > 0 || Tasks.Count > 0;

        /// <summary>
        /// Returns an ordered list of all root-level entries (tools and tasks interleaved)
        /// sorted by timestamp for display purposes.
        /// </summary>
        [JsonIgnore]
        public List<object> OrderedEntries
        {
            get
            {
                var entries = new List<(string timestamp, object entry)>();

                foreach (var tool in RootTools)
                    entries.Add((tool.Timestamp, tool));
                foreach (var task in Tasks)
                    entries.Add((task.Timestamp, task));

                entries.Sort((a, b) => string.Compare(a.timestamp, b.timestamp, StringComparison.Ordinal));

                var result = new List<object>(entries.Count);
                foreach (var (_, entry) in entries)
                    result.Add(entry);
                return result;
            }
        }

        /// <summary>
        /// Generates a human-readable label for a tool activity based on its name and input.
        /// </summary>
        public static string FormatToolLabel(string toolName, string inputJson)
        {
            Newtonsoft.Json.Linq.JObject input = null;
            try { input = Newtonsoft.Json.Linq.JObject.Parse(inputJson ?? "{}"); } catch { }

            string Get(string key) => input?[key]?.ToString();
            string TruncPath(string path)
            {
                if (string.IsNullOrEmpty(path)) return path;
                var idx = path.LastIndexOf('/');
                return idx >= 0 ? path.Substring(idx + 1) : path;
            }

            return toolName switch
            {
                "Read" => $"Reading {TruncPath(Get("file_path"))}",
                "Grep" or "Search" => FormatSearchLabel(Get("pattern"), Get("path")),
                "Edit" or "Write" => $"Editing {TruncPath(Get("file_path"))}",
                "Bash" => "Running command...",
                "Agent" => $"Subagent: {Get("description") ?? "working..."}",
                "Glob" => $"Finding files matching {Get("pattern")}",
                _ when toolName?.StartsWith("mcp__uniclaude-unity__") == true
                    => $"Unity: {toolName.Substring("mcp__uniclaude-unity__".Length)}",
                _ => $"Using {toolName}...",
            };
        }

        static string FormatSearchLabel(string pattern, string path)
        {
            var label = "Searching";
            if (!string.IsNullOrEmpty(pattern))
                label += $" for \"{pattern}\"";
            if (!string.IsNullOrEmpty(path))
            {
                var idx = path.LastIndexOf('/');
                var short_ = idx >= 0 ? path.Substring(idx + 1) : path;
                label += $" in {short_}";
            }
            return label;
        }
    }
}
