using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace UniClaude.Editor.MCP
{
    /// <summary>
    /// Centralized path validation utility that prevents path traversal attacks by ensuring
    /// all resolved paths remain within the Unity project root.
    /// </summary>
    public static class PathSandbox
    {
        /// <summary>
        /// Path comparison mode: case-insensitive on Windows, case-sensitive elsewhere.
        /// </summary>
        static readonly StringComparison PathComparison =
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

        /// <summary>
        /// Gets the absolute, canonicalized path to the Unity project root directory.
        /// </summary>
        public static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        /// <summary>
        /// Resolves a relative path to a canonical absolute path within the project root.
        /// Suitable for read operations.
        /// </summary>
        /// <param name="relativePath">The path relative to the project root. Must not be null, empty, or absolute.</param>
        /// <returns>The canonical absolute path within the project root.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="relativePath"/> is null, empty, or is an absolute path.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the resolved path escapes the project root (e.g. via <c>../</c> traversal).
        /// </exception>
        public static string Resolve(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                throw new ArgumentException("Path must not be null or empty.", nameof(relativePath));

            if (relativePath.StartsWith("/") || relativePath.StartsWith("\\") ||
                (relativePath.Length >= 2 && relativePath[1] == ':'))
                throw new ArgumentException($"Path '{relativePath}' must be relative, not absolute.", nameof(relativePath));

            // Normalize backslashes so traversal via '\' is caught on all platforms.
            var normalized = relativePath.Replace('\\', '/');

            var root = ProjectRoot;
            var combined = Path.Combine(root, normalized);
            var canonical = Path.GetFullPath(combined);

            var rootWithSeparator = root + Path.DirectorySeparatorChar;
            if (!canonical.StartsWith(rootWithSeparator, PathComparison) &&
                !string.Equals(canonical, root, PathComparison))
                throw new InvalidOperationException($"Path '{relativePath}' resolves outside the project root. Access denied.");

            return canonical;
        }

        /// <summary>
        /// Resolves a relative path to a canonical absolute path within the project root,
        /// additionally checking that the path is not blocked for write or delete operations.
        /// </summary>
        /// <param name="relativePath">The path relative to the project root. Must not be null, empty, or absolute.</param>
        /// <returns>The canonical absolute path within the project root.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="relativePath"/> is null, empty, or is an absolute path.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the resolved path escapes the project root, or targets a path segment
        /// that is blocked for write operations.
        /// </exception>
        public static string ResolveWritable(string relativePath)
        {
            var canonical = Resolve(relativePath);

            if (IsBlockedForWrite(relativePath))
            {
                var normalized = relativePath.Replace('\\', '/');
                var segment = normalized.Split('/')[0];
                throw new InvalidOperationException($"Writing to '{segment}' is not allowed.");
            }

            return canonical;
        }

        /// <summary>
        /// Strips the project root prefix from an absolute path, returning a project-relative path.
        /// If the path does not start with the project root, it is returned as-is.
        /// </summary>
        /// <param name="absolutePath">The absolute file path to make relative.</param>
        /// <returns>
        /// The path relative to the project root, or <paramref name="absolutePath"/> unchanged
        /// if it does not start with the project root.
        /// </returns>
        public static string MakeRelative(string absolutePath)
        {
            var root = ProjectRoot;

            var withSep = root + Path.DirectorySeparatorChar;
            if (absolutePath.StartsWith(withSep, PathComparison))
                return absolutePath.Substring(withSep.Length);

            var withAltSep = root + Path.AltDirectorySeparatorChar;
            if (absolutePath.StartsWith(withAltSep, PathComparison))
                return absolutePath.Substring(withAltSep.Length);

            return absolutePath;
        }

        /// <summary>
        /// Returns true when the given relative path targets a location that must not be written to.
        /// </summary>
        /// <param name="relativePath">The path relative to the project root.</param>
        /// <returns><c>true</c> if the path is blocked; otherwise <c>false</c>.</returns>
        static bool IsBlockedForWrite(string relativePath)
        {
            var normalized = relativePath.Replace('\\', '/');
            return normalized.Equals(".git", PathComparison) ||
                   normalized.StartsWith(".git/", PathComparison);
        }
    }
}
