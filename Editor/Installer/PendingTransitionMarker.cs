using System.IO;
using Newtonsoft.Json;

namespace UniClaude.Editor.Installer
{
    /// <summary>
    /// DTO for <c>Library/UniClaude/pending-transition.json</c>. Written by Unity
    /// during staging, consumed by the Node <c>finalize-transition</c> helper and
    /// by the post-restart resume path.
    /// </summary>
    public class PendingTransitionMarker
    {
        /// <summary>"to-standard" or "delete-from-ninja".</summary>
        [JsonProperty("kind")] public string Kind { get; set; }
        /// <summary>Unity editor process PID captured at stage time.</summary>
        [JsonProperty("unityPid")] public int UnityPid { get; set; }
        /// <summary>Absolute path to the Unity editor binary (e.g. EditorApplication.applicationPath).</summary>
        [JsonProperty("unityAppPath")] public string UnityAppPath { get; set; }
        /// <summary>Project root (contains Packages/, Library/, .git/).</summary>
        [JsonProperty("projectPath")] public string ProjectPath { get; set; }
        /// <summary>Full path to the embedded UniClaude package folder.</summary>
        [JsonProperty("packagePath")] public string PackagePath { get; set; }
        /// <summary>Full path to transition-status.json (shared with the helper).</summary>
        [JsonProperty("statusPath")] public string StatusPath { get; set; }
        /// <summary>ISO-8601 UTC timestamp of stage time.</summary>
        [JsonProperty("createdAt")] public string CreatedAt { get; set; }

        /// <summary>Read the marker file; returns null if missing.</summary>
        public static PendingTransitionMarker Read(string path)
        {
            if (!File.Exists(path)) return null;
            return JsonConvert.DeserializeObject<PendingTransitionMarker>(File.ReadAllText(path));
        }

        /// <summary>Write the marker file, creating parent directories as needed.</summary>
        public static void Write(string path, PendingTransitionMarker marker)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonConvert.SerializeObject(marker, Formatting.Indented) + "\n");
        }
    }
}
