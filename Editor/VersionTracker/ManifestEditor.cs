using System;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace UniClaude.Editor.VersionTracker
{
    /// <summary>
    /// Pure string / JSON helpers for reading and rewriting entries in Packages/manifest.json.
    /// Avoids full-file reserialization to preserve user formatting.
    /// </summary>
    public static class ManifestEditor
    {
        /// <summary>Shape of the manifest entry for the package.</summary>
        public enum EntryKind
        {
            /// <summary>Package not present in the manifest.</summary>
            Missing,
            /// <summary>Git URL with a `#vX.Y.Z` suffix.</summary>
            TagPinned,
            /// <summary>Git URL without a semver tag suffix, or a registry version string.</summary>
            Floating,
        }

        /// <summary>Inspection result.</summary>
        public struct Inspection
        {
            /// <summary>Classification of the manifest entry.</summary>
            public EntryKind Kind;
            /// <summary>Tag portion after `#` when Kind is TagPinned.</summary>
            public string CurrentTag;
            /// <summary>Raw entry value.</summary>
            public string RawValue;
        }

        static readonly Regex TagSuffix = new Regex(@"#(v?\d+\.\d+\.\d+)$", RegexOptions.Compiled);

        /// <summary>Parse the manifest JSON and classify the package entry.</summary>
        /// <param name="manifestJson">Full manifest.json text.</param>
        /// <param name="packageName">Package name to look up.</param>
        /// <returns>Inspection result.</returns>
        public static Inspection Inspect(string manifestJson, string packageName)
        {
            var root = JObject.Parse(manifestJson);
            var deps = root["dependencies"] as JObject;
            var entry = deps?[packageName]?.ToString();
            if (entry == null) return new Inspection { Kind = EntryKind.Missing };

            var m = TagSuffix.Match(entry);
            if (m.Success)
                return new Inspection
                {
                    Kind = EntryKind.TagPinned,
                    CurrentTag = m.Groups[1].Value,
                    RawValue = entry,
                };

            return new Inspection { Kind = EntryKind.Floating, RawValue = entry };
        }

        /// <summary>
        /// Rewrite the tag suffix of the package entry to <paramref name="newTag"/>.
        /// Throws <see cref="InvalidOperationException"/> when the entry is not tag-pinned.
        /// Uses a targeted string replacement to preserve formatting.
        /// </summary>
        /// <param name="manifestJson">Original manifest.json text.</param>
        /// <param name="packageName">Package name whose entry should be rewritten.</param>
        /// <param name="newTag">New tag to substitute (e.g. "v0.3.0").</param>
        /// <returns>Manifest text with the tag replaced.</returns>
        public static string RewriteTag(string manifestJson, string packageName, string newTag)
        {
            var insp = Inspect(manifestJson, packageName);
            if (insp.Kind != EntryKind.TagPinned)
                throw new InvalidOperationException("Entry is not tag-pinned: " + insp.Kind);

            var oldValue = insp.RawValue;
            var newValue = TagSuffix.Replace(oldValue, "#" + newTag);

            var needle = "\"" + oldValue + "\"";
            var replacement = "\"" + newValue + "\"";
            var idx = manifestJson.IndexOf(needle, StringComparison.Ordinal);
            if (idx < 0)
                throw new InvalidOperationException("Value lookup failed; manifest may be malformed.");
            return manifestJson.Substring(0, idx) + replacement + manifestJson.Substring(idx + needle.Length);
        }
    }
}
