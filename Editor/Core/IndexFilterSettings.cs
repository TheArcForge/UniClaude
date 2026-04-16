using System;

namespace UniClaude.Editor
{
    /// <summary>
    /// Resolves whether packages and project folders should be included in indexing
    /// based on user settings. Local packages default to included, registry packages
    /// default to excluded. Users override via <see cref="UniClaudeSettings.PackageIndexOverrides"/>.
    /// </summary>
    public static class IndexFilterSettings
    {
        static readonly string[] SkippedPackageFolders = { "Tests", "Tests~", "Samples", "Samples~" };

        /// <summary>
        /// Returns true if the given package should be indexed based on user settings.
        /// Local packages are included by default; registry packages are excluded by default.
        /// Explicit overrides take precedence.
        /// </summary>
        /// <param name="package">The package to check.</param>
        /// <param name="settings">Current user settings.</param>
        /// <returns>True if the package should be scanned.</returns>
        public static bool IsPackageIncluded(PackageInfo package, UniClaudeSettings settings)
        {
            if (settings.PackageIndexOverrides.TryGetValue(package.Name, out var explicitValue))
                return explicitValue;

            return package.IsLocal;
        }

        /// <summary>
        /// Returns true if the given relative asset path falls under an excluded folder.
        /// </summary>
        /// <param name="relativePath">Relative path from project root (e.g. "Assets/ThirdParty/Foo.cs").</param>
        /// <param name="settings">Current user settings.</param>
        /// <returns>True if the path should be skipped during scanning.</returns>
        public static bool IsPathExcluded(string relativePath, UniClaudeSettings settings)
        {
            foreach (var folder in settings.ExcludedFolders)
            {
                var normalized = folder.TrimEnd('/', '\\');
                if (relativePath.StartsWith(normalized + "/", StringComparison.Ordinal) ||
                    relativePath.StartsWith(normalized + "\\", StringComparison.Ordinal) ||
                    relativePath == normalized)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Returns true if the given folder name is a UPM convention folder that should
        /// be skipped when scanning packages (Tests, Samples, and their tilde variants).
        /// </summary>
        /// <param name="folderName">The folder name (not full path).</param>
        /// <returns>True if the folder should be skipped.</returns>
        public static bool IsPackageFolderSkipped(string folderName)
        {
            foreach (var skipped in SkippedPackageFolders)
            {
                if (string.Equals(folderName, skipped, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }
    }
}
