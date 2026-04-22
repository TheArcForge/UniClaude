using System;

namespace UniClaude.Editor.VersionTracker
{
    /// <summary>Minimal semver parser and comparator. No prerelease / metadata support.</summary>
    public static class SemverCompare
    {
        /// <summary>Parsed semver triple.</summary>
        public struct Version
        {
            /// <summary>Major version number.</summary>
            public int Major;
            /// <summary>Minor version number.</summary>
            public int Minor;
            /// <summary>Patch version number.</summary>
            public int Patch;
        }

        /// <summary>
        /// Parses "v1.2.3" or "1.2.3" into a <see cref="Version"/>.
        /// Rejects prereleases, build metadata, and malformed input.
        /// </summary>
        /// <param name="input">Raw version string.</param>
        /// <param name="version">Parsed result on success.</param>
        /// <returns>True on successful parse.</returns>
        public static bool TryParse(string input, out Version version)
        {
            version = default;
            if (string.IsNullOrWhiteSpace(input)) return false;

            var s = input.Trim();
            if (s.Length > 0 && (s[0] == 'v' || s[0] == 'V')) s = s.Substring(1);

            var parts = s.Split('.');
            if (parts.Length != 3) return false;

            if (!int.TryParse(parts[0], out var ma) || ma < 0) return false;
            if (!int.TryParse(parts[1], out var mi) || mi < 0) return false;
            if (!int.TryParse(parts[2], out var pa) || pa < 0) return false;

            version = new Version { Major = ma, Minor = mi, Patch = pa };
            return true;
        }

        /// <summary>
        /// True when <paramref name="candidate"/> parses as a strictly-newer semver than <paramref name="current"/>.
        /// False for equal, older, or any unparseable input.
        /// </summary>
        /// <param name="candidate">Tag under consideration (e.g. from GitHub).</param>
        /// <param name="current">Currently-installed version (from package.json).</param>
        /// <returns>True if candidate is strictly newer.</returns>
        public static bool IsNewer(string candidate, string current)
        {
            if (!TryParse(candidate, out var a)) return false;
            if (!TryParse(current, out var b)) return false;

            if (a.Major != b.Major) return a.Major > b.Major;
            if (a.Minor != b.Minor) return a.Minor > b.Minor;
            return a.Patch > b.Patch;
        }
    }
}
