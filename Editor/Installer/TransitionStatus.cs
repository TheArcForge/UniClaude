using System.Collections.Generic;
using Newtonsoft.Json;

namespace UniClaude.Editor.Installer
{
    /// <summary>
    /// JSON DTO matching the status file written by the Node installer
    /// (Library/UniClaude/transition-status.json).
    /// </summary>
    public class TransitionStatus
    {
        /// <summary>Subcommand name e.g. "to-ninja".</summary>
        [JsonProperty("command")] public string Command { get; set; }

        /// <summary>"ok" | "error" | "in-progress".</summary>
        [JsonProperty("result")] public string Result { get; set; }

        /// <summary>"ninja" | "standard" | "deleted" | "unknown".</summary>
        [JsonProperty("mode")] public string Mode { get; set; }

        /// <summary>Last completed step name, if applicable.</summary>
        [JsonProperty("step")] public string Step { get; set; }

        /// <summary>User-facing error message when Result == "error".</summary>
        [JsonProperty("error")] public string Error { get; set; }

        /// <summary>ISO-8601 write time.</summary>
        [JsonProperty("timestamp")] public string Timestamp { get; set; }

        /// <summary>Per-step breakdown.</summary>
        [JsonProperty("steps")] public List<StepResult> Steps { get; set; }

        /// <summary>Individual step outcome in a transition.</summary>
        public class StepResult
        {
            /// <summary>Step name.</summary>
            [JsonProperty("name")] public string Name { get; set; }
            /// <summary>"ok" | "error".</summary>
            [JsonProperty("status")] public string Status { get; set; }
            /// <summary>Optional detail (e.g., error text).</summary>
            [JsonProperty("detail")] public string Detail { get; set; }
        }
    }
}
