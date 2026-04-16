// Editor/UI/Input/MessageSubmission.cs
using System.Collections.Generic;

namespace UniClaude.Editor.UI.Input
{
    /// <summary>
    /// Encapsulates a user's chat submission: typed text plus any file attachments.
    /// </summary>
    public class MessageSubmission
    {
        /// <summary>The user's typed message text (may be empty if attachments-only).</summary>
        public string Text { get; }

        /// <summary>Attached files. Empty list if none.</summary>
        public IReadOnlyList<AttachmentInfo> Attachments { get; }

        /// <summary>
        /// Creates a new <see cref="MessageSubmission"/> instance.
        /// </summary>
        /// <param name="text">The user's typed message text.</param>
        /// <param name="attachments">Attached files, or null for none.</param>
        public MessageSubmission(string text, IReadOnlyList<AttachmentInfo> attachments)
        {
            Text = text ?? "";
            Attachments = attachments ?? new List<AttachmentInfo>();
        }

        /// <summary>True if there is either text or at least one attachment.</summary>
        public bool HasContent => !string.IsNullOrEmpty(Text) || Attachments.Count > 0;
    }
}
