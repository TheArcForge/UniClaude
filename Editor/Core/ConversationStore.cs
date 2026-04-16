using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace UniClaude.Editor
{
    /// <summary>
    /// Lightweight summary of a conversation for index/listing purposes.
    /// </summary>
    [Serializable]
    public class ConversationSummary
    {
        /// <summary>Unique identifier matching the full conversation.</summary>
        public string Id;

        /// <summary>Display title of the conversation.</summary>
        public string Title;

        /// <summary>UTC timestamp when the conversation was created.</summary>
        public string CreatedAt;

        /// <summary>UTC timestamp of the last update.</summary>
        public string UpdatedAt;

        /// <summary>Total number of messages in the conversation.</summary>
        public int MessageCount;

        /// <summary>
        /// Creates a <see cref="ConversationSummary"/> from a full <see cref="Conversation"/>.
        /// </summary>
        /// <param name="c">The conversation to summarize.</param>
        /// <returns>A new summary with fields copied from the conversation.</returns>
        public static ConversationSummary From(Conversation c)
        {
            return new ConversationSummary
            {
                Id = c.Id,
                Title = c.Title,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
                MessageCount = c.Messages.Count
            };
        }
    }

    /// <summary>
    /// Static utility for persisting and retrieving <see cref="Conversation"/> objects as JSON files.
    /// Maintains an index file for fast listing without deserializing every conversation.
    /// </summary>
    public static class ConversationStore
    {
        static readonly string DefaultBaseDir =
            Path.Combine("Library", "UniClaude", "conversations");

        static string _baseDir;

        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            Converters = new List<JsonConverter> { new StringEnumConverter() }
        };

        /// <summary>
        /// Directory where conversation JSON files and the index are stored.
        /// Defaults to <c>Library/UniClaude/conversations</c>. Can be overridden for testing.
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

        /// <summary>
        /// Saves a conversation to disk as <c>{id}.json</c> and updates the index.
        /// Uses an atomic write (tmp file + move) to prevent corruption.
        /// </summary>
        /// <param name="conversation">The conversation to persist.</param>
        public static void Save(Conversation conversation)
        {
            Directory.CreateDirectory(BaseDir);

            var filePath = Path.Combine(BaseDir, conversation.Id + ".json");
            var tmpPath = filePath + ".tmp";
            var json = JsonConvert.SerializeObject(conversation, Settings);

            File.WriteAllText(tmpPath, json);
            MoveWithOverwrite(tmpPath, filePath);

            UpdateIndex(conversation);
        }

        /// <summary>
        /// Loads a conversation by its identifier.
        /// </summary>
        /// <param name="id">The conversation identifier (filename without extension).</param>
        /// <returns>The deserialized <see cref="Conversation"/>, or <c>null</c> if the file
        /// is missing or corrupt.</returns>
        public static Conversation Load(string id)
        {
            var filePath = Path.Combine(BaseDir, id + ".json");
            if (!File.Exists(filePath))
                return null;

            try
            {
                var json = File.ReadAllText(filePath);
                return JsonConvert.DeserializeObject<Conversation>(json, Settings);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Returns the conversation index sorted by <c>UpdatedAt</c> descending (most recent first).
        /// If the index file is missing or corrupt, it is silently rebuilt from the conversation files.
        /// </summary>
        /// <returns>A list of <see cref="ConversationSummary"/> objects.</returns>
        public static List<ConversationSummary> LoadIndex()
        {
            var indexPath = Path.Combine(BaseDir, "index.json");

            if (File.Exists(indexPath))
            {
                try
                {
                    var json = File.ReadAllText(indexPath);
                    var summaries = JsonConvert.DeserializeObject<List<ConversationSummary>>(json, Settings);
                    if (summaries != null)
                        return SortDescending(summaries);
                }
                catch
                {
                    // Corrupt index, fall through to rebuild.
                }
            }

            RebuildIndex();
            return LoadIndexDirect();
        }

        /// <summary>
        /// Renames a conversation by updating its title in both the JSON file and the index.
        /// </summary>
        /// <param name="id">The conversation identifier to rename.</param>
        /// <param name="newTitle">The new title for the conversation.</param>
        /// <returns><c>true</c> if the conversation was found and renamed; <c>false</c> otherwise.</returns>
        public static bool Rename(string id, string newTitle)
        {
            var conversation = Load(id);
            if (conversation == null)
                return false;

            conversation.Title = newTitle;
            Save(conversation);
            return true;
        }

        /// <summary>
        /// Deletes a conversation file and removes its entry from the index.
        /// </summary>
        /// <param name="id">The conversation identifier to delete.</param>
        public static void Delete(string id)
        {
            var filePath = Path.Combine(BaseDir, id + ".json");
            if (File.Exists(filePath))
                File.Delete(filePath);

            RemoveFromIndex(id);
        }

        /// <summary>
        /// Deletes all conversation files and writes an empty index.
        /// </summary>
        public static void DeleteAll()
        {
            if (Directory.Exists(BaseDir))
            {
                foreach (var file in Directory.GetFiles(BaseDir, "*.json"))
                {
                    File.Delete(file);
                }

                // Also clean up any leftover .tmp files
                foreach (var file in Directory.GetFiles(BaseDir, "*.tmp"))
                {
                    File.Delete(file);
                }
            }

            Directory.CreateDirectory(BaseDir);
            WriteIndex(new List<ConversationSummary>());
        }

        /// <summary>
        /// Rebuilds the index by scanning all conversation JSON files on disk.
        /// Skips <c>index.json</c> and any files that fail to deserialize.
        /// </summary>
        public static void RebuildIndex()
        {
            Directory.CreateDirectory(BaseDir);

            var summaries = new List<ConversationSummary>();

            foreach (var file in Directory.GetFiles(BaseDir, "*.json"))
            {
                if (Path.GetFileName(file) == "index.json")
                    continue;

                try
                {
                    var json = File.ReadAllText(file);
                    var conv = JsonConvert.DeserializeObject<Conversation>(json, Settings);
                    if (conv != null)
                        summaries.Add(ConversationSummary.From(conv));
                }
                catch
                {
                    // Skip corrupt files.
                }
            }

            WriteIndex(SortDescending(summaries));
        }

        /// <summary>
        /// Returns statistics about the cached conversation files (excluding index.json).
        /// </summary>
        /// <returns>A tuple of (file count, total bytes).</returns>
        public static (int count, long bytes) GetCacheStats()
        {
            if (!Directory.Exists(BaseDir))
                return (0, 0);

            int count = 0;
            long bytes = 0;

            foreach (var file in Directory.GetFiles(BaseDir, "*.json"))
            {
                if (Path.GetFileName(file) == "index.json")
                    continue;

                count++;
                bytes += new FileInfo(file).Length;
            }

            return (count, bytes);
        }

        // --- Private helpers ---

        static string IndexPath => Path.Combine(BaseDir, "index.json");

        static void UpdateIndex(Conversation conversation)
        {
            var summaries = LoadIndexSafe();
            summaries.RemoveAll(s => s.Id == conversation.Id);
            summaries.Add(ConversationSummary.From(conversation));
            WriteIndex(SortDescending(summaries));
        }

        static void RemoveFromIndex(string id)
        {
            var summaries = LoadIndexSafe();
            summaries.RemoveAll(s => s.Id == id);
            WriteIndex(summaries);
        }

        static List<ConversationSummary> LoadIndexSafe()
        {
            var indexPath = IndexPath;
            if (!File.Exists(indexPath))
                return new List<ConversationSummary>();

            try
            {
                var json = File.ReadAllText(indexPath);
                return JsonConvert.DeserializeObject<List<ConversationSummary>>(json, Settings)
                       ?? new List<ConversationSummary>();
            }
            catch
            {
                return new List<ConversationSummary>();
            }
        }

        static List<ConversationSummary> LoadIndexDirect()
        {
            var indexPath = IndexPath;
            if (!File.Exists(indexPath))
                return new List<ConversationSummary>();

            try
            {
                var json = File.ReadAllText(indexPath);
                return JsonConvert.DeserializeObject<List<ConversationSummary>>(json, Settings)
                       ?? new List<ConversationSummary>();
            }
            catch
            {
                return new List<ConversationSummary>();
            }
        }

        static void WriteIndex(List<ConversationSummary> summaries)
        {
            Directory.CreateDirectory(BaseDir);
            var indexPath = IndexPath;
            var tmpPath = indexPath + ".tmp";
            var json = JsonConvert.SerializeObject(summaries, Settings);
            File.WriteAllText(tmpPath, json);
            MoveWithOverwrite(tmpPath, indexPath);
        }

        static List<ConversationSummary> SortDescending(List<ConversationSummary> summaries)
        {
            summaries.Sort((a, b) =>
                string.Compare(b.UpdatedAt, a.UpdatedAt, StringComparison.Ordinal));
            return summaries;
        }

        static void MoveWithOverwrite(string source, string destination)
        {
            try
            {
                if (File.Exists(destination))
                    File.Delete(destination);
                File.Move(source, destination);
            }
            catch (IOException)
            {
                // Fallback: if the destination was locked between delete and move,
                // copy + delete source instead.
                File.Copy(source, destination, overwrite: true);
                try { File.Delete(source); }
                catch { /* Best effort cleanup of temp file */ }
            }
        }
    }
}
