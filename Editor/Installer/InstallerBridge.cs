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
    /// Bridges Unity UI to the Node installer script. Supports two operation
    /// modes: synchronous (ToNinja, where Unity stays alive) and staged
    /// (ToStandard / DeleteFromNinja, where Unity quits and hands off to the
    /// external finalize-transition helper).
    /// </summary>
    public static class InstallerBridge
    {
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
            /// <summary>Absolute path to the pending-transition.json marker (only set for staged subcommands).</summary>
            public string MarkerPath;
        }

        /// <summary>Run an installer subcommand synchronously and return the parsed status.</summary>
        public static Outcome Run(
            Subcommand cmd, string projectRoot, string gitUrl,
            string installerPath, string nodePath = null)
        {
            try
            {
                var node = ResolveNodeBinary(nodePath);
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
                string markerPath = null;
                if (!string.IsNullOrWhiteSpace(stdout))
                {
                    try
                    {
                        status = JsonConvert.DeserializeObject<TransitionStatus>(stdout.Trim());
                        // Also look for markerPath in the raw JSON; TransitionStatus doesn't have that field
                        // but it's present in stdout as a top-level key.
                        var raw = JsonConvert.DeserializeObject<Dictionary<string, object>>(stdout.Trim());
                        if (raw != null && raw.TryGetValue("markerPath", out var mp) && mp is string mpStr)
                            markerPath = mpStr;
                    }
                    catch (System.Exception ex) { parseError = ex.Message; }
                }

                return new Outcome
                {
                    ExitCode = proc.ExitCode,
                    Status = status,
                    Stderr = stderr,
                    ParseError = parseError,
                    MarkerPath = markerPath,
                };
            }
            catch (System.Exception ex)
            {
                return new Outcome { ExitCode = -1, Stderr = ex.Message };
            }
        }

        /// <summary>
        /// Run a staged subcommand (ToStandard or DeleteFromNinja), open the
        /// progress window, spawn the finalize-transition helper detached, and
        /// quit Unity. Returns without waiting.
        /// </summary>
        /// <returns>True if staging succeeded and Unity is about to exit; false if staging failed.</returns>
        public static bool StageAndExit(
            Subcommand cmd, string projectRoot, string installerPath, string nodePath = null)
        {
            if (cmd != Subcommand.ToStandard && cmd != Subcommand.DeleteFromNinja)
                throw new System.ArgumentException($"StageAndExit not supported for {cmd}");

            var node = ResolveNodeBinary(nodePath);
            if (string.IsNullOrEmpty(node))
            {
                EditorUtility.DisplayDialog("Node.js not found",
                    "Set the Node.js path in UniClaude Settings.", "OK");
                return false;
            }

            var outcome = Run(cmd, projectRoot, null, installerPath, node);
            if (outcome.ExitCode != 0 || outcome.Status?.Result != "ok" || string.IsNullOrEmpty(outcome.MarkerPath))
            {
                EditorUtility.DisplayDialog("Conversion failed",
                    outcome.Status?.Error ?? outcome.Stderr ?? outcome.ParseError ?? "Unknown error",
                    "OK");
                return false;
            }

            var kind = cmd == Subcommand.ToStandard ? TransitionKind.ToStandard : TransitionKind.DeleteFromNinja;
            TransitionProgressWindow.OpenForNewTransition(kind, outcome.MarkerPath);

            SpawnFinalizeHelper(node, installerPath, outcome.MarkerPath);

            EditorApplication.delayCall += () => EditorApplication.Exit(0);
            return true;
        }

        static void SpawnFinalizeHelper(string node, string installerPath, string markerPath)
        {
            var libraryInstaller = Path.Combine(
                Path.GetDirectoryName(markerPath), "installer-persistent.mjs");
            var entry = File.Exists(libraryInstaller) ? libraryInstaller : installerPath;

            var psi = new ProcessStartInfo(node)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(markerPath),
            };
            psi.ArgumentList.Add(entry);
            psi.ArgumentList.Add("finalize-transition");
            psi.ArgumentList.Add("--marker");
            psi.ArgumentList.Add(markerPath);
            Process.Start(psi);
        }

        static string ResolveNodeBinary(string explicitPath)
        {
            if (!string.IsNullOrEmpty(explicitPath)) return explicitPath;
            var settings = UniClaudeSettings.Load();
            return SidecarManager.FindNodeBinary(settings.NodePath);
        }

        /// <summary>Build the argv for a given subcommand.</summary>
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
            if (cmd == Subcommand.ToStandard || cmd == Subcommand.DeleteFromNinja)
            {
                list.Add("--unity-pid");
                list.Add(Process.GetCurrentProcess().Id.ToString());
                list.Add("--unity-app-path");
                list.Add(EditorApplication.applicationPath);
            }
            return list.ToArray();
        }

        /// <summary>Absolute path to the packaged installer.mjs.</summary>
        public static string FindInstallerPath(string projectRoot)
        {
            var pkg = UnityEditor.PackageManager.PackageInfo
                .FindForAssembly(typeof(InstallerBridge).Assembly);
            if (pkg != null && !string.IsNullOrEmpty(pkg.resolvedPath))
                return Path.Combine(pkg.resolvedPath, "Installer~", "installer.mjs");
            return Path.Combine(projectRoot, "Packages", "com.arcforge.uniclaude", "Installer~", "installer.mjs");
        }
    }
}
