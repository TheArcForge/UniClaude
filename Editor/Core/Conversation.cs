using System;
using System.Collections.Generic;

namespace UniClaude.Editor
{
    /// <summary>
    /// Represents a single UniClaude conversation with its message history and metadata.
    /// </summary>
    [Serializable]
    public class Conversation
    {
        /// <summary>Unique identifier for this conversation.</summary>
        public string Id;

        /// <summary>Display title (auto-generated from first message or user-set).</summary>
        public string Title;

        /// <summary>UTC timestamp when the conversation was created.</summary>
        public string CreatedAt;

        /// <summary>UTC timestamp of the last message.</summary>
        public string UpdatedAt;

        /// <summary>CLI session ID for resuming this conversation via <c>--resume</c>.</summary>
        public string SessionId;

        /// <summary>All messages in this conversation, in chronological order.</summary>
        public List<ChatMessage> Messages;

        /// <summary>
        /// Creates a new empty conversation with a generated ID and current timestamp.
        /// </summary>
        public Conversation()
        {
            Id = Guid.NewGuid().ToString("N");
            Title = "New Chat";
            CreatedAt = DateTime.UtcNow.ToString("o");
            UpdatedAt = CreatedAt;
            Messages = new List<ChatMessage>();
        }

        /// <summary>
        /// Adds a message to the conversation and updates the timestamp.
        /// Auto-sets the title from the first user message if still default.
        /// </summary>
        /// <param name="message">The message to add.</param>
        public void AddMessage(ChatMessage message)
        {
            Messages.Add(message);
            UpdatedAt = DateTime.UtcNow.ToString("o");

            if (Title == "New Chat" && message.Role == MessageRole.User)
            {
                Title = message.Content.Length > 60
                    ? message.Content.Substring(0, 57) + "..."
                    : message.Content;
            }
        }
    }
}
