using System;
using System.Collections.Generic;
using System.Linq;

namespace UniClaude.Editor
{
    /// <summary>
    /// Retrieves index entries by keyword matching against entry names, symbols, and summaries.
    /// Walks one level of dependencies for matched entries. Ranks by match score.
    /// </summary>
    public class KeywordRetriever : IIndexRetriever
    {
        /// <inheritdoc />
        public RetrievalResult Retrieve(string query, ProjectIndex index, RetrievalSettings settings)
        {
            var result = new RetrievalResult();

            if (string.IsNullOrWhiteSpace(query) || index?.Entries == null || index.Entries.Count == 0)
                return result;

            var queryTokens = Tokenize(query, settings.StopWords);
            if (queryTokens.Count == 0)
                return result;

            // Score each entry
            var scored = new List<(IndexEntry entry, int score)>();
            foreach (var entry in index.Entries)
            {
                var score = ScoreEntry(entry, queryTokens);
                if (score > 0)
                    scored.Add((entry, score));
            }

            // Sort by score descending
            scored.Sort((a, b) => b.score.CompareTo(a.score));

            // Walk dependencies for matched entries (one level deep)
            var entryPaths = new HashSet<string>(scored.Select(s => s.entry.AssetPath));
            var depEntries = new List<(IndexEntry entry, int score)>();

            foreach (var (entry, _) in scored)
            {
                if (entry.Dependencies == null) continue;

                foreach (var depPath in entry.Dependencies)
                {
                    if (entryPaths.Contains(depPath)) continue;

                    var depEntry = index.Entries.FirstOrDefault(e =>
                        string.Equals(e.AssetPath, depPath, StringComparison.OrdinalIgnoreCase));

                    if (depEntry != null)
                    {
                        entryPaths.Add(depPath);
                        depEntries.Add((depEntry, 0)); // score 0 = dependency, not direct match
                    }
                }
            }

            scored.AddRange(depEntries);

            // Cap by MaxFiles and MaxTokens
            int tokenCount = 0;
            foreach (var (entry, _) in scored)
            {
                if (result.Entries.Count >= settings.MaxFiles)
                    break;

                var entryTokens = EstimateTokens(entry);
                if (tokenCount + entryTokens > settings.MaxTokens && result.Entries.Count > 0)
                    break;

                result.Entries.Add(entry);
                tokenCount += entryTokens;
            }

            result.EstimatedTokens = tokenCount;
            return result;
        }

        /// <summary>
        /// Scores an entry against query tokens. Higher = better match.
        /// </summary>
        static int ScoreEntry(IndexEntry entry, List<string> queryTokens)
        {
            int score = 0;

            foreach (var token in queryTokens)
            {
                // Exact name match (highest value)
                if (string.Equals(entry.Name, token, StringComparison.OrdinalIgnoreCase))
                    score += 10;

                // Symbol match
                if (entry.Symbols != null)
                {
                    foreach (var symbol in entry.Symbols)
                    {
                        if (string.Equals(symbol, token, StringComparison.OrdinalIgnoreCase))
                            score += 5;
                    }
                }

                // Summary contains token
                if (entry.Summary != null &&
                    entry.Summary.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    score += 1;
            }

            return score;
        }

        /// <summary>
        /// Default stop words for keyword retrieval. Exposed so settings can display them.
        /// </summary>
        public static readonly string[] DefaultStopWords =
        {
            "the", "a", "an", "is", "are", "was", "were", "be", "been",
            "do", "does", "did", "have", "has", "had", "will", "would",
            "can", "could", "should", "may", "might", "shall",
            "in", "on", "at", "to", "for", "of", "with", "by", "from",
            "and", "or", "not", "but", "if", "then", "so", "as",
            "it", "its", "this", "that", "what", "how", "why", "when",
            "my", "me", "i", "you", "we", "they", "he", "she",
            "about", "tell", "show", "explain", "describe",
            "project", "unity", "game", "using", "want", "need", "make",
            "hey", "hi", "hello", "please", "thanks", "thank"
        };

        /// <summary>
        /// Splits a query string into lowercase tokens for matching.
        /// Filters out stop words and very short tokens.
        /// </summary>
        /// <param name="query">The user's query text.</param>
        /// <param name="stopWordsList">Stop words to filter. Uses <see cref="DefaultStopWords"/> if null.</param>
        static List<string> Tokenize(string query, string[] stopWordsList)
        {
            var stopWords = new HashSet<string>(
                stopWordsList ?? DefaultStopWords,
                StringComparer.OrdinalIgnoreCase);

            return query
                .Split(new[] { ' ', '\t', '\n', '\r', '.', ',', '?', '!', ':', ';', '(', ')', '[', ']', '{', '}', '"', '\'' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length >= 2 && !stopWords.Contains(t))
                .Select(t => t.ToLowerInvariant())
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Estimates token count for an entry using the summary length / 4 heuristic.
        /// </summary>
        static int EstimateTokens(IndexEntry entry)
        {
            return (entry.Summary?.Length ?? 0) / 4 + 1;
        }
    }
}
