using System.Diagnostics;
using System.IO;

namespace UniClaude.Editor.Installer
{
    /// <summary>
    /// Thin synchronous wrapper around the `git` CLI.
    /// Used by the install-mode probe and bridge; the heavy mutations are in the Node installer.
    /// </summary>
    public static class GitCli
    {
        /// <summary>Result of a single git invocation.</summary>
        public struct Result
        {
            /// <summary>Exit code. 0 means success.</summary>
            public int ExitCode;
            /// <summary>Captured stdout.</summary>
            public string Stdout;
            /// <summary>Captured stderr.</summary>
            public string Stderr;
        }

        /// <summary>Run git with the given args in cwd. Captures both streams.</summary>
        /// <param name="cwd">Working directory for the invocation.</param>
        /// <param name="args">Arguments passed to git.</param>
        /// <returns>Exit code, stdout, stderr.</returns>
        public static Result Run(string cwd, params string[] args)
        {
            var psi = new ProcessStartInfo("git")
            {
                WorkingDirectory = cwd,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            foreach (var a in args) psi.ArgumentList.Add(a);

            using var proc = Process.Start(psi);
            if (proc == null) return new Result { ExitCode = -1 };

            var stdout = proc.StandardOutput.ReadToEnd();
            var stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();
            return new Result { ExitCode = proc.ExitCode, Stdout = stdout, Stderr = stderr };
        }

        /// <summary>True if git exits 0 for the given invocation.</summary>
        /// <param name="cwd">Working directory.</param>
        /// <param name="args">Arguments.</param>
        /// <returns>True on success.</returns>
        public static bool Ok(string cwd, params string[] args) => Run(cwd, args).ExitCode == 0;

        /// <summary>True if git is reachable (i.e., `git --version` succeeds).</summary>
        /// <returns>True if git is installed and on PATH.</returns>
        public static bool IsAvailable()
        {
            try { return Ok(Directory.GetCurrentDirectory(), "--version"); }
            catch { return false; }
        }
    }
}
