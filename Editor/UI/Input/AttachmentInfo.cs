// Editor/UI/Input/AttachmentInfo.cs
using UnityEngine;

namespace UniClaude.Editor.UI.Input
{
    /// <summary>
    /// Data model representing a single file attachment in the chat input.
    /// </summary>
    public class AttachmentInfo
    {
        /// <summary>Full path to the file (original location, or staging path for pasted images).</summary>
        public string OriginalPath { get; }

        /// <summary>Display name (e.g. "PlayerController.cs").</summary>
        public string FileName { get; }

        /// <summary>File extension including the dot (e.g. ".cs").</summary>
        public string Extension { get; }

        /// <summary>File size in bytes.</summary>
        public long FileSizeBytes { get; }

        /// <summary>True if this is a temp file in the staging area (pasted image). Deleted on remove.</summary>
        public bool IsTemporary { get; }

        /// <summary>Small preview texture for image attachments. Null for non-images.</summary>
        public Texture2D Thumbnail { get; set; }

        /// <summary>True when file size is 500KB or more.</summary>
        public bool IsLargeFile => FileSizeBytes >= 512_000;

        /// <summary>Human-readable size string (e.g. "2.3 MB").</summary>
        public string SizeLabel
        {
            get
            {
                if (FileSizeBytes < 1024) return $"{FileSizeBytes} B";
                if (FileSizeBytes < 1_048_576) return $"{FileSizeBytes / 1024.0:F1} KB";
                return $"{FileSizeBytes / 1_048_576.0:F1} MB";
            }
        }

        /// <summary>
        /// Creates a new <see cref="AttachmentInfo"/> instance.
        /// </summary>
        /// <param name="originalPath">Full path to the file.</param>
        /// <param name="fileName">Display name of the file.</param>
        /// <param name="extension">File extension including the dot.</param>
        /// <param name="fileSizeBytes">File size in bytes.</param>
        /// <param name="isTemporary">True if this is a temporary staged file.</param>
        public AttachmentInfo(string originalPath, string fileName, string extension,
            long fileSizeBytes, bool isTemporary)
        {
            OriginalPath = originalPath;
            FileName = fileName;
            Extension = extension;
            FileSizeBytes = fileSizeBytes;
            IsTemporary = isTemporary;
        }
    }
}
