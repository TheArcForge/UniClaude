using System;

namespace UniClaude.Editor
{
    /// <summary>
    /// Represents a single message in a UniClaude conversation.
    /// </summary>
    [Serializable]
    public class ChatMessage
    {
        /// <summary>Who sent the message.</summary>
        public MessageRole Role;

        /// <summary>The message text content.</summary>
        public string Content;

        /// <summary>UTC timestamp when the message was created.</summary>
        public string Timestamp;

        /// <summary>Tool name for <see cref="MessageRole.ToolCall"/> entries.</summary>
        public string ToolName;

        /// <summary>Whether the tool call failed. Only used by <see cref="MessageRole.ToolCall"/>.</summary>
        public bool IsError;

        /// <summary>Input token count for <see cref="MessageRole.TokenUsage"/> entries.</summary>
        public int InputTokens;

        /// <summary>Output token count for <see cref="MessageRole.TokenUsage"/> entries.</summary>
        public int OutputTokens;

        /// <summary>
        /// Tool and subagent activity recorded during this assistant turn.
        /// Only populated for <see cref="MessageRole.Assistant"/> messages.
        /// </summary>
        public ActivityLog Activity;

        /// <summary>
        /// Creates a new chat message with the current UTC timestamp.
        /// </summary>
        /// <param name="role">Who sent the message.</param>
        /// <param name="content">The message text.</param>
        public ChatMessage(MessageRole role, string content)
        {
            Role = role;
            Content = content;
            Timestamp = DateTime.UtcNow.ToString("o");
        }

        /// <summary>Parameterless constructor for serialization.</summary>
        public ChatMessage() { }
    }

    /// <summary>
    /// The sender of a chat message.
    /// </summary>
    public enum MessageRole
    {
        /// <summary>Message from the user.</summary>
        User,

        /// <summary>Message from Claude.</summary>
        Assistant,

        /// <summary>System status message (errors, info).</summary>
        System,

        /// <summary>Tool execution entry (name + result + success/failure).</summary>
        ToolCall,

        /// <summary>Project context indicator (tier 1 system prompt).</summary>
        ProjectContext,

        /// <summary>Informational status line (MCP log, domain lock, etc.).</summary>
        Info,

        /// <summary>Token usage label after an assistant response.</summary>
        TokenUsage
    }
}
