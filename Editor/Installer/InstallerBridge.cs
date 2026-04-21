using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;
using UniClaude.Editor;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace UniClaude.Editor.Installer
{
    /// <summary>
    /// Bridges Unity UI to the Node installer script.
    /// Spawns synchronous subprocesses, parses the JSON status they produce,
    /// and persists a transition key across domain reloads via EditorPrefs.
    /// </summary>
    public static class InstallerBridge
    {
        /// <summary>EditorPrefs key used to checkpoint transition state across reloads.</summary>
        public const string TransitionKey = "UniClaude.Transition";

        /// <summary>Supported installer subcommands.</summary>
        public enum Subcommand { ToNinja, ToStandard, DeleteFromNinja }

        /// <summary>Outcome of running the installer.</summary>
        public struct Outcome
        {
            /// <summary>Process exit code (0 on success).</summary>
            public int ExitCode;
            /// <summary>Status JSON the installer wrote (null if it failed before writing or printed malformed JSON).</summary>
            public TransitionStatus Status;
            /// <summary>Captured stderr on failure.</summary>
            public string Stderr;
            /// <summary>JSON parse error message when stdout was non-empty but unparseable; null otherwise.</summary>
            public string ParseError;
        }

        /// <summary>Run an installer subcommand synchronously and return the parsed status.</summary>
        /// <param name="cmd">Subcommand to run.</param>
        /// <param name="projectRoot">Unity project root.</param>
        /// <param name="gitUrl">Required for ToNinja; ignored otherwise.</param>
        /// <param name="installerPath">Absolute path to installer.mjs.</param>
        /// <param name="nodePath">Node.js binary path (null = plain "node" on PATH).</param>
        /// <returns>Outcome.</returns>
        public static Outcome Run(
            Subcommand cmd, string projectRoot, string gitUrl,
            string installerPath, string nodePath = null)
        {
            try
            {
                var node = nodePath;
                if (string.IsNullOrEmpty(node))
                {
                    var settings = UniClaudeSettings.Load();
                    node = SidecarManager.FindNodeBinary(settings.NodePath);
                }
                if (string.IsNullOrEmpty(node))
                    return new Outcome { ExitCode = -1, Stderr = "Node.js binary not found. Set the path in UniClaude Settings." };

                var args = new List<string> { installerPath };
                args.AddRange(BuildArgs(cmd, projectRoot, gitUrl));

                var psi = new ProcessStartInfo(node)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = projectRoot,
                };
                foreach (var a in args) psi.ArgumentList.Add(a);

                using var proc = Process.Start(psi);
                if (proc == null) return new Outcome { ExitCode = -1, Stderr = "failed to spawn" };

                var stdout = proc.StandardOutput.ReadToEnd();
                var stderr = proc.StandardError.ReadToEnd();
                proc.WaitForExit();

                TransitionStatus status = null;
                string parseError = null;
                if (!string.IsNullOrWhiteSpace(stdout))
                {
                    try { status = JsonConvert.DeserializeObject<TransitionStatus>(stdout.Trim()); }
                    catch (System.Exception ex) { parseError = ex.Message; }
                }

                return new Outcome { ExitCode = proc.ExitCode, Status = status, Stderr = stderr, ParseError = parseError };
            }
            catch (System.Exception ex)
            {
                return new Outcome { ExitCode = -1, Stderr = ex.Message };
            }
        }

        /// <summary>Build the argv for a given subcommand.</summary>
        /// <param name="cmd">Subcommand.</param>
        /// <param name="projectRoot">Project root.</param>
        /// <param name="gitUrl">Git URL for ToNinja; null otherwise.</param>
        /// <returns>Argument list (without the node binary or installer path).</returns>
        public static string[] BuildArgs(Subcommand cmd, string projectRoot, string gitUrl)
        {
            var sub = cmd switch
            {
                Subcommand.ToNinja => "to-ninja",
                Subcommand.ToStandard => "to-standard",
                Subcommand.DeleteFromNinja => "delete-from-ninja",
                _ => throw new System.ArgumentException($"unknown: {cmd}"),
            };

            var list = new List<string> { sub, "--project-root", projectRoot };
            if (cmd == Subcommand.ToNinja)
            {
                if (string.IsNullOrEmpty(gitUrl))
                    throw new System.ArgumentException("gitUrl required for ToNinja");
                list.Add("--git-url");
                list.Add(gitUrl);
            }
            return list.ToArray();
        }

        /// <summary>Absolute path to the packaged installer.mjs.</summary>
        /// <param name="projectRoot">Project root.</param>
        /// <returns>Path to installer.mjs inside the resolved UniClaude package (Library/PackageCache in Standard mode, Packages/com.arcforge.uniclaude in Ninja mode).</returns>
        public static string FindInstallerPath(string projectRoot)
        {
            var pkg = UnityEditor.PackageManager.PackageInfo
                .FindForAssembly(typeof(InstallerBridge).Assembly);
            if (pkg != null && !string.IsNullOrEmpty(pkg.resolvedPath))
                return Path.Combine(pkg.resolvedPath, "Installer~", "installer.mjs");
            return Path.Combine(projectRoot, "Packages", "com.arcforge.uniclaude", "Installer~", "installer.mjs");
        }

        /// <summary>Persist a transition checkpoint with a timestamp.</summary>
        /// <param name="value">Value to store (use empty string to clear).</param>
        public static void WriteCheckpoint(string value)
        {
            EditorPrefs.SetString(TransitionKey, value ?? "");
            EditorPrefs.SetString(TransitionKey + ".At",
                value != null ? System.DateTime.UtcNow.ToString("O") : "");
        }

        /// <summary>Read the current transition checkpoint (empty string if none).</summary>
        /// <returns>Current checkpoint value.</returns>
        public static string ReadCheckpoint() => EditorPrefs.GetString(TransitionKey, "");

        /// <summary>Read the timestamp when the current checkpoint was set.</summary>
        /// <returns>UTC timestamp, or DateTime.MinValue if none/unparseable.</returns>
        public static System.DateTime ReadCheckpointTimestamp()
        {
            var ts = EditorPrefs.GetString(TransitionKey + ".At", "");
            if (string.IsNullOrEmpty(ts)) return System.DateTime.MinValue;
            return System.DateTime.TryParse(ts, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
                ? dt : System.DateTime.MinValue;
        }
    }
}
