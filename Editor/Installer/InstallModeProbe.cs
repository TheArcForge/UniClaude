using System.IO;
using UnityEditor.PackageManager;
using PkgPackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace UniClaude.Editor.Installer
{
    /// <summary>
    /// Detects the current install mode and pre-flight conditions for conversion.
    /// Pure IO + git subprocess — no Unity UI.
    /// </summary>
    public static class InstallModeProbe
    {
        const string PackageName = "com.arcforge.uniclaude";
        const string SentinelComment = "# UniClaude ninja-mode (managed by UniClaude — do not edit)";

        /// <summary>Aggregate probe result surfaced to the Settings UI.</summary>
        public struct Status
        {
            /// <summary>Current runtime install state.</summary>
            public InstallMode Mode;
            /// <summary>Project root contains a .git directory.</summary>
            public bool IsGitRepo;
            /// <summary>git binary is reachable.</summary>
            public bool GitOnPath;
            /// <summary>Packages/manifest.json has uncommitted changes.</summary>
            public bool ManifestDirty;
            /// <summary>Packages/packages-lock.json has uncommitted changes
            /// (only meaningful in Standard mode; Ninja hides it via filter).</summary>
            public bool LockDirty;
            /// <summary>User-facing reason for disabling conversion buttons, or null.</summary>
            public string BlockingReason;
        }

        /// <summary>Detect the current install state from the running assembly's package info.</summary>
        /// <param name="projectRoot">Unity project root (contains Packages/, Library/, .git/).</param>
        /// <returns>Probe status.</returns>
        public static Status Probe(string projectRoot)
        {
            var status = new Status
            {
                IsGitRepo = IsGitRepo(projectRoot),
                GitOnPath = GitCli.IsAvailable(),
            };

            status.Mode = DetectMode(projectRoot);

            if (!status.IsGitRepo)
            {
                status.BlockingReason = "Ninja mode requires a git repo.";
                return status;
            }
            if (!status.GitOnPath)
            {
                status.BlockingReason = "Install `git` and make it available on PATH.";
                return status;
            }

            status.ManifestDirty = !GitCli.Ok(projectRoot, "diff", "--quiet", "Packages/manifest.json");
            if (status.Mode == InstallMode.Standard)
            {
                status.LockDirty = !GitCli.Ok(projectRoot, "diff", "--quiet", "Packages/packages-lock.json");
            }

            if (status.ManifestDirty || status.LockDirty)
            {
                status.BlockingReason = "Commit or stash changes in Packages/manifest.json and packages-lock.json first.";
            }

            return status;
        }

        /// <summary>True if .git/ exists under projectRoot.</summary>
        /// <param name="projectRoot">Project root path.</param>
        /// <returns>True if a git repo.</returns>
        public static bool IsGitRepo(string projectRoot)
        {
            return Directory.Exists(Path.Combine(projectRoot, ".git"));
        }

        /// <summary>True if .git/info/exclude contains the UniClaude sentinel block.</summary>
        /// <param name="projectRoot">Project root path.</param>
        /// <returns>True if ninja sentinel is present.</returns>
        public static bool HasSentinel(string projectRoot)
        {
            var p = Path.Combine(projectRoot, ".git", "info", "exclude");
            if (!File.Exists(p)) return false;
            return File.ReadAllText(p).Contains(SentinelComment);
        }

        static InstallMode DetectMode(string projectRoot)
        {
            var asm = typeof(InstallModeProbe).Assembly;
            var info = PkgPackageInfo.FindForAssembly(asm);
            if (info == null || info.name != PackageName) return InstallMode.Other;

            if (info.source == PackageSource.Git) return InstallMode.Standard;
            if (info.source == PackageSource.Embedded && HasSentinel(projectRoot)) return InstallMode.Ninja;
            return InstallMode.Other;
        }
    }
}
