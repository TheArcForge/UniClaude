namespace UniClaude.Editor.MCP
{
    /// <summary>
    /// MCP tool for searching the project index. Provides on-demand access to
    /// project file summaries, symbols, and dependencies via keyword retrieval.
    /// </summary>
    public static class ProjectSearchTools
    {
        /// <summary>
        /// Searches the project index for assets matching the given query.
        /// Returns file paths, symbols (classes, methods, properties), dependencies, and summaries.
        /// </summary>
        /// <param name="query">Search query — class names, method names, concepts, or file names.</param>
        /// <param name="maxResults">Maximum number of results to return.</param>
        /// <returns>Formatted search results or an error if the index is unavailable.</returns>
        [MCPTool("project_search",
            "Search the project index for scripts, scenes, prefabs, shaders, and other assets by keyword. " +
            "Returns file paths, symbols (classes, methods, properties), dependencies, and summaries. " +
            "Use when you need to understand existing code, find relevant files, or discover " +
            "what exists before creating or modifying assets.")]
        public static MCPToolResult ProjectSearch(
            [MCPToolParam("Search query — class names, method names, concepts, or file names", required: true)]
            string query,
            [MCPToolParam("Max results to return (default 10)")]
            int maxResults = 10)
        {
            if (string.IsNullOrWhiteSpace(query))
                return MCPToolResult.Error("Query must not be empty.");

            var awareness = ProjectAwareness.Instance;
            if (awareness == null)
                return MCPToolResult.Error(
                    "Project awareness is not available. Enable it in UniClaude settings and reopen the chat window.");

            var index = awareness.GetIndex();
            var settings = new RetrievalSettings
            {
                MaxFiles = maxResults > 0 ? maxResults : 10,
                MaxTokens = 8192
            };
            var retriever = new KeywordRetriever();
            var result = retriever.Retrieve(query, index, settings);

            return MCPToolResult.Success(ContextFormatter.FormatResults(result));
        }
    }
}
