using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UniClaude.Editor.MCP
{
    /// <summary>
    /// Represents the result of an MCP tool invocation.
    /// </summary>
    public class MCPToolResult
    {
        /// <summary>Gets whether this result represents an error.</summary>
        public bool IsError { get; }

        /// <summary>Gets the result text content.</summary>
        public string Text { get; }

        MCPToolResult(bool isError, string text)
        {
            IsError = isError;
            Text = text;
        }

        /// <summary>
        /// Creates a successful result. Objects are serialized as JSON; strings are used directly.
        /// </summary>
        /// <param name="content">The content to return.</param>
        /// <returns>A successful MCPToolResult.</returns>
        public static MCPToolResult Success(object content)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));

            var text = content is string s ? s : JsonConvert.SerializeObject(content, Formatting.Indented);
            return new MCPToolResult(false, text);
        }

        /// <summary>
        /// Creates an error result with the given message.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <returns>An error MCPToolResult.</returns>
        public static MCPToolResult Error(string message)
        {
            if (string.IsNullOrEmpty(message))
                throw new ArgumentException("Error message must not be null or empty.", nameof(message));

            return new MCPToolResult(true, message);
        }

        /// <summary>
        /// Converts this result to the MCP protocol response format.
        /// </summary>
        /// <returns>A JObject with content array and optional isError flag.</returns>
        public JObject ToMCPResponse()
        {
            var result = new JObject
            {
                ["content"] = new JArray
                {
                    new JObject
                    {
                        ["type"] = "text",
                        ["text"] = Text
                    }
                }
            };

            if (IsError)
                result["isError"] = true;

            return result;
        }
    }
}
