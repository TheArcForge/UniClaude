using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace UniClaude.Editor
{
    /// <summary>
    /// Manages the project index in memory and persists it to disk as JSON.
    /// Stored in <c>Library/UniClaude/index/</c>. Handles staleness detection
    /// and cache lifecycle.
    /// </summary>
    public static class ProjectIndexStore
    {
        static readonly string DefaultBaseDir = Path.Combine("Library", "UniClaude", "index");

        static string _baseDir;

        static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            Converters = new List<JsonConverter> { new StringEnumConverter() }
        };

        /// <summary>
        /// Directory where the index JSON file is stored.
        /// Defaults to <c>Library/UniClaude/index</c>. Can be overridden for testing.
        /// </summary>
        public static string BaseDir
        {
            get => _baseDir ?? DefaultBaseDir;
            set => _baseDir = value;
        }

        /// <summary>
        /// Restores <see cref="BaseDir"/> to its default value.
        /// </summary>
        public static void ResetBaseDir()
        {
            _baseDir = null;
        }

        static string IndexPath => Path.Combine(BaseDir, "index.json");

        /// <summary>
        /// Saves the project index to disk using atomic write (tmp file + move).
        /// </summary>
        /// <param name="index">The index to persist.</param>
        public static void Save(ProjectIndex index)
        {
            Directory.CreateDirectory(BaseDir);
            var tmpPath = IndexPath + ".tmp";
            File.WriteAllText(tmpPath, JsonConvert.SerializeObject(index, JsonSettings));
            MoveWithOverwrite(tmpPath, IndexPath);
        }

        /// <summary>
        /// Loads the project index from disk.
        /// </summary>
        /// <returns>The deserialized index, or <c>null</c> if missing or corrupt.</returns>
        public static ProjectIndex Load()
        {
            if (!File.Exists(IndexPath))
                return null;

            try
            {
                var json = File.ReadAllText(IndexPath);
                return JsonConvert.DeserializeObject<ProjectIndex>(json, JsonSettings);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Deletes the cached index file.
        /// </summary>
        public static void Delete()
        {
            if (File.Exists(IndexPath))
                File.Delete(IndexPath);
        }

        /// <summary>
        /// Returns cache statistics for the index file.
        /// </summary>
        /// <returns>A tuple of (exists, bytes) where <c>exists</c> is true if the file is present
        /// and <c>bytes</c> is the file size in bytes.</returns>
        public static (bool exists, long bytes) GetCacheStats()
        {
            if (!File.Exists(IndexPath))
                return (false, 0);

            return (true, new FileInfo(IndexPath).Length);
        }

        static void MoveWithOverwrite(string source, string destination)
        {
            if (File.Exists(destination))
                File.Delete(destination);
            File.Move(source, destination);
        }
    }
}
