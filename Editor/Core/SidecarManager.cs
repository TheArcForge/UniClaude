using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace UniClaude.Editor
{
    /// <summary>
    /// Manages the Node.js sidecar process lifecycle.
    /// Spawns, monitors, and cleans up the sidecar that runs the Agent SDK.
    /// </summary>
    public class SidecarManager : IDisposable
    {
        const string SessionKeyPid = "UniClaude_SidecarPid";
        const string SessionKeyPort = "UniClaude_SidecarPort";
        const int MaxRetries = 3;
        const int HealthTimeoutMs = 10000;
        const int HealthIntervalMs = 30000;

        static readonly HttpClient _http = new();

        Process _process;
        int _port;
        int _retryCount;
        double _lastHealthCheck;
        bool _disposed;

        /// <summary>The port the sidecar process is listening on.</summary>
        public int Port => _port;

        /// <summary>Whether the sidecar process is currently running.</summary>
        public bool IsRunning => _process != null && !_process.HasExited;

        /// <summary>
        /// Ensures the sidecar is running. Reconnects after domain reload
        /// or spawns a new process as needed.
        /// </summary>
        /// <param name="mcpPort">The port to use for MCP communication.</param>
        /// <param name="settings">The current UniClaude user settings.</param>
        public async Task EnsureRunning(int mcpPort, UniClaudeSettings settings)
        {
            // Try reconnecting to existing process (domain reload)
            var savedPid = SessionState.GetInt(SessionKeyPid, 0);
            var savedPort = SessionState.GetInt(SessionKeyPort, 0);

            if (savedPid > 0 && savedPort > 0)
            {
                if (await CheckHealth(savedPort))
                {
                    _port = savedPort;
                    try { _process = Process.GetProcessById(savedPid); }
                    catch { /* Process gone — will respawn below */ }

                    if (IsRunning)
                    {
                        Debug.Log($"[UniClaude] Reconnected to sidecar on port {_port}");
                        return;
                    }
                }

                // Stale — kill and respawn
                TryKillProcess(savedPid);
            }

            await Spawn(mcpPort, settings);
        }

        async Task Spawn(int mcpPort, UniClaudeSettings settings)
        {
            var nodePath = FindNodeBinary(settings.NodePath);
            if (nodePath == null)
            {
                throw new InvalidOperationException(
                    "Node.js not found. Claude Code requires Node.js — check your installation."
                );
            }

            var entryPoint = GetSidecarEntryPoint();
            if (!File.Exists(entryPoint))
            {
                throw new FileNotFoundException(
                    $"Sidecar entry point not found: {entryPoint}"
                );
            }

            var requestedPort = settings.SidecarPort;
            var args = BuildArgs(requestedPort, mcpPort);

            var psi = new ProcessStartInfo
            {
                FileName = nodePath,
                Arguments = $"\"{entryPoint}\" {args}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            // Build a rich PATH: node bin dir + user's shell PATH + Unity's PATH
            var nodeBinDir = Path.GetDirectoryName(nodePath)!;
            var shellPath = GetShellPath();
            var unityPath = Environment.GetEnvironmentVariable("PATH") ?? "";
            psi.Environment["PATH"] = nodeBinDir + Path.PathSeparator + shellPath
                + Path.PathSeparator + unityPath;

            // Ensure HOME is set (Unity may strip it on macOS)
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(home))
                psi.Environment["HOME"] = home;

            _process = Process.Start(psi);
            if (_process == null)
                throw new InvalidOperationException("Failed to start sidecar process");

            // Forward sidecar stderr to Unity console, routed by severity
            var verbose = settings.VerboseLogging;
            _process.ErrorDataReceived += (_, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;
                var line = e.Data;
                if (line.Contains("error", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("exited with code"))
                    Debug.LogError($"[UniClaude Sidecar] {line}");
                else if (verbose && line.Contains("warn", StringComparison.OrdinalIgnoreCase))
                    Debug.LogWarning($"[UniClaude Sidecar] {line}");
                else if (verbose)
                    Debug.Log($"[UniClaude Sidecar] {line}");
            };
            _process.BeginErrorReadLine();

            // Read first line of stdout — should be JSON with port
            var startLine = await Task.Run(() => _process.StandardOutput.ReadLine());
            if (string.IsNullOrEmpty(startLine))
                throw new InvalidOperationException("Sidecar did not output startup info");

            // Parse port from startup JSON: {"status":"started","port":12345,...}
            var json = Newtonsoft.Json.Linq.JObject.Parse(startLine);
            _port = json["port"]?.Value<int>() ?? 0;
            if (_port == 0)
                throw new InvalidOperationException("Sidecar did not report a valid port");

            // Wait for health check
            if (!await WaitForHealth(_port))
            {
                _process.Kill();
                throw new InvalidOperationException("Sidecar failed health check after startup");
            }

            // Persist for domain reload
            SessionState.SetInt(SessionKeyPid, _process.Id);
            SessionState.SetInt(SessionKeyPort, _port);

            Debug.Log($"[UniClaude] Sidecar started on port {_port} (PID {_process.Id})");
        }

        async Task<bool> WaitForHealth(int port)
        {
            var elapsed = 0;
            var delay = 100;
            while (elapsed < HealthTimeoutMs)
            {
                if (await CheckHealth(port))
                    return true;

                await Task.Delay(delay);
                elapsed += delay;
                delay = Math.Min(delay * 2, 2000);
            }
            return false;
        }

        static async Task<bool> CheckHealth(int port)
        {
            try
            {
                var response = await _http.GetAsync($"http://127.0.0.1:{port}/health");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Public health check for use during setup verification.
        /// Returns true if the sidecar at the given port responds to /health.
        /// </summary>
        public static Task<bool> CheckHealthPublic(int port) => CheckHealth(port);

        /// <summary>
        /// Periodic health ping — call from EditorApplication.update.
        /// Safe to call from a synchronous context; the async work is wrapped
        /// internally so exceptions never go unobserved.
        /// </summary>
        public void HealthPing()
        {
            if (_port == 0) return;

            var now = EditorApplication.timeSinceStartup;
            if (now - _lastHealthCheck < HealthIntervalMs / 1000.0) return;
            _lastHealthCheck = now;

            _ = HealthPingAsync();
        }

        async Task HealthPingAsync()
        {
            try
            {
                if (!await CheckHealth(_port))
                    Debug.LogWarning("[UniClaude] Sidecar health check failed");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UniClaude] Health check error: {ex.Message}");
            }
        }

        static bool IsWindows => Application.platform == RuntimePlatform.WindowsEditor;

        /// <summary>
        /// The executable name for node on the current platform.
        /// </summary>
        static string NodeExeName => IsWindows ? "node.exe" : "node";

        /// <summary>
        /// Searches for the Node.js binary using an override path or the system PATH.
        /// </summary>
        /// <param name="overridePath">A direct path to the node binary or a directory to search. Pass empty string to use system PATH only.</param>
        /// <returns>The full path to the node binary, or null if not found.</returns>
        public static string FindNodeBinary(string overridePath)
        {
            // Check user override first
            if (!string.IsNullOrEmpty(overridePath))
            {
                if (File.Exists(overridePath))
                    return overridePath;

                var candidate = Path.Combine(overridePath, NodeExeName);
                if (File.Exists(candidate))
                    return candidate;
            }

            // Search PATH
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (var dir in pathEnv.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                var candidate = Path.Combine(dir, NodeExeName);
                if (File.Exists(candidate))
                    return candidate;
            }

            // Platform-specific common locations
            if (IsWindows)
            {
                var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string[] windowsPaths =
                {
                    Path.Combine(programFiles, "nodejs", "node.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "nodejs", "node.exe"),
                    Path.Combine(home, "AppData", "Roaming", "nvm", "current", "node.exe"),
                    Path.Combine(home, "AppData", "Local", "fnm_multishells", "node.exe"),
                    Path.Combine(home, "scoop", "apps", "nodejs", "current", "node.exe"),
                };
                foreach (var p in windowsPaths)
                {
                    if (File.Exists(p))
                        return p;
                }
            }
            else
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string[] unixPaths =
                {
                    "/usr/local/bin/node",
                    "/opt/homebrew/bin/node",
                    Path.Combine(home, ".nvm/current/bin/node"),
                };
                foreach (var p in unixPaths)
                {
                    if (File.Exists(p))
                        return p;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the absolute path to the sidecar's entry point script.
        /// </summary>
        /// <returns>The path to <c>Sidecar~/dist/index.js</c> within the UniClaude package.</returns>
        public static string GetSidecarEntryPoint()
        {
            var packageRoot = GetSidecarRoot();
            return Path.Combine(packageRoot, "dist", "index.js");
        }

        /// <summary>
        /// Returns the absolute path to the Sidecar~ directory.
        /// </summary>
        public static string GetSidecarRoot()
        {
            return Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "Packages", "com.arcforge.uniclaude", "Sidecar~")
            );
        }

        /// <summary>
        /// Whether the sidecar dependencies are installed.
        /// The compiled JavaScript (dist/) ships with the package; only node_modules is needed at setup time.
        /// </summary>
        public static bool IsSetupComplete
        {
            get
            {
                var root = GetSidecarRoot();
                return Directory.Exists(Path.Combine(root, "node_modules"));
            }
        }

        /// <summary>
        /// Runs npm install to set up the sidecar dependencies.
        /// Reports progress via the <paramref name="onProgress"/> callback.
        /// </summary>
        /// <param name="nodePath">Path to the Node.js binary.</param>
        /// <param name="onProgress">Callback for progress messages.</param>
        /// <returns>Null on success, or an error message on failure.</returns>
        public static async Task<string> RunSetup(string nodePath, Action<string> onProgress)
        {
            var root = GetSidecarRoot();
            var nodeBinDir = Path.GetDirectoryName(nodePath)!;
            var npmName = IsWindows ? "npm.cmd" : "npm";
            var npmPath = Path.Combine(nodeBinDir, npmName);

            onProgress?.Invoke("Installing dependencies (npm install)...");
            var installResult = await RunProcess(npmPath, "install", root, nodeBinDir);
            if (installResult.ExitCode != 0)
                return $"npm install failed:\n{installResult.StdErr}";

            onProgress?.Invoke("Setup complete!");
            return null;
        }

        static async Task<(int ExitCode, string StdOut, string StdErr)> RunProcess(
            string fileName, string arguments, string workingDirectory, string extraPathDir = null)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            // Ensure node is on PATH for child processes (npm/npx shebang uses env node)
            if (extraPathDir != null)
            {
                var currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
                psi.Environment["PATH"] = extraPathDir + Path.PathSeparator + currentPath;
            }

            var process = Process.Start(psi);
            if (process == null)
                return (-1, "", "Failed to start process");

            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await Task.Run(() => process.WaitForExit());
            return (process.ExitCode, stdout, stderr);
        }

        /// <summary>
        /// Builds the command-line argument string for the sidecar process.
        /// </summary>
        /// <param name="port">The HTTP port for the sidecar to listen on. 0 = auto-assign.</param>
        /// <param name="mcpPort">The MCP port for communication with the Unity Editor.</param>
        /// <returns>A formatted argument string suitable for passing to the node process.</returns>
        public static string BuildArgs(int port, int mcpPort)
        {
            return $"--port {port} --mcp-port {mcpPort}";
        }

        /// <summary>
        /// Resolves the user's full shell PATH. On macOS/Linux, runs a login shell
        /// to pick up PATH entries from shell profile files. On Windows, the system
        /// PATH is already complete, so returns it directly.
        /// </summary>
        static string GetShellPath()
        {
            if (IsWindows)
                return Environment.GetEnvironmentVariable("PATH") ?? "";

            try
            {
                var shell = Environment.GetEnvironmentVariable("SHELL") ?? "/bin/zsh";
                var psi = new ProcessStartInfo
                {
                    FileName = shell,
                    Arguments = "-l -c \"echo $PATH\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                using var p = Process.Start(psi);
                if (p == null) return "";
                var output = p.StandardOutput.ReadToEnd().Trim();
                p.WaitForExit(5000);
                return p.ExitCode == 0 ? output : "";
            }
            catch { return ""; }
        }

        static void TryKillProcess(int pid)
        {
            try
            {
                var p = Process.GetProcessById(pid);
                p.Kill();
            }
            catch { /* Already exited */ }
        }

        /// <summary>
        /// Stops the sidecar process and clears session state.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (IsRunning)
            {
                try { _process.Kill(); }
                catch { /* Already exited */ }
            }

            SessionState.EraseInt(SessionKeyPid);
            SessionState.EraseInt(SessionKeyPort);
        }
    }
}
