using System.Collections.Generic;

namespace UniClaude.Editor
{
    /// <summary>
    /// Settings controlling how many results a retriever should return.
    /// </summary>
    public class RetrievalSettings
    {
        /// <summary>Maximum number of files to include in results. Default: 10.</summary>
        public int MaxFiles = 10;

        /// <summary>Maximum estimated tokens for all results combined. Default: 4096.</summary>
        public int MaxTokens = 4096;

        /// <summary>Additional stop words to filter from queries. Merged with built-in defaults.</summary>
        public string[] StopWords;
    }

    /// <summary>
    /// Result of an index retrieval operation.
    /// </summary>
    public class RetrievalResult
    {
        /// <summary>Matched index entries, ranked by relevance (highest first).</summary>
        public List<IndexEntry> Entries = new List<IndexEntry>();

        /// <summary>Estimated token count of all entries combined.</summary>
        public int EstimatedTokens;
    }

    /// <summary>
    /// Interface for index retrieval strategies. Implementations match a user query
    /// against the project index and return ranked results.
    /// Pluggable: keyword matching today, embeddings in the future.
    /// </summary>
    public interface IIndexRetriever
    {
        /// <summary>
        /// Retrieves index entries matching the given query, ranked by relevance
        /// and capped by the settings limits.
        /// </summary>
        /// <param name="query">The user's message text.</param>
        /// <param name="index">The project index to search.</param>
        /// <param name="settings">Max files and tokens limits.</param>
        /// <returns>Ranked results within the configured limits.</returns>
        RetrievalResult Retrieve(string query, ProjectIndex index, RetrievalSettings settings);
    }
}
