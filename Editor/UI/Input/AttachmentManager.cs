// Editor/UI/Input/AttachmentManager.cs
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UniClaude.Editor.UI.Input
{
    /// <summary>
    /// Manages file attachments for the chat input: validation, staging, and lifecycle.
    /// </summary>
    public class AttachmentManager
    {
        static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            // Code/Text
            ".cs", ".shader", ".hlsl", ".json", ".xml", ".yaml", ".yml",
            ".txt", ".md", ".asmdef", ".uxml", ".uss",
            // Images
            ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tga", ".psd",
            // Unity assets
            ".unity", ".prefab", ".asset", ".mat", ".controller", ".anim"
        };

        static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".gif"
        };

        readonly string _stagingPath;
        readonly List<AttachmentInfo> _attachments = new();
        readonly HashSet<string> _attachedPaths = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Current list of attachments.</summary>
        public IReadOnlyList<AttachmentInfo> Attachments => _attachments;

        /// <summary>Fires when the attachment list changes (add, remove, clear).</summary>
        public event Action OnAttachmentsChanged;

        /// <summary>
        /// Creates a new AttachmentManager with the given staging directory for temp files.
        /// </summary>
        /// <param name="stagingPath">Directory path for pasted image temp files.</param>
        public AttachmentManager(string stagingPath)
        {
            _stagingPath = stagingPath;
        }

        /// <summary>
        /// Validates and adds a file by path. Silently ignores non-whitelisted,
        /// nonexistent, or already-attached files.
        /// </summary>
        public void AddFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            var ext = Path.GetExtension(path);
            if (!AllowedExtensions.Contains(ext))
                return;

            if (_attachedPaths.Contains(path))
                return;

            var info = new FileInfo(path);
            var attachment = new AttachmentInfo(
                originalPath: path,
                fileName: info.Name,
                extension: ext,
                fileSizeBytes: info.Length,
                isTemporary: false
            );

            if (ImageExtensions.Contains(ext))
                attachment.Thumbnail = LoadThumbnail(path);

            _attachments.Add(attachment);
            _attachedPaths.Add(path);
            OnAttachmentsChanged?.Invoke();
        }

        /// <summary>
        /// Saves a pasted image to the staging area and adds it as an attachment.
        /// </summary>
        public void AddImage(Texture2D image)
        {
            if (image == null) return;

            Directory.CreateDirectory(_stagingPath);
            var fileName = $"paste_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.png";
            var path = Path.Combine(_stagingPath, fileName);
            File.WriteAllBytes(path, image.EncodeToPNG());

            var info = new FileInfo(path);
            var attachment = new AttachmentInfo(
                originalPath: path,
                fileName: fileName,
                extension: ".png",
                fileSizeBytes: info.Length,
                isTemporary: true
            );
            attachment.Thumbnail = image;

            _attachments.Add(attachment);
            _attachedPaths.Add(path);
            OnAttachmentsChanged?.Invoke();
        }

        /// <summary>Removes an attachment. Deletes temp files from staging.</summary>
        public void Remove(AttachmentInfo attachment)
        {
            if (attachment == null) return;
            _attachments.Remove(attachment);
            _attachedPaths.Remove(attachment.OriginalPath);

            if (attachment.IsTemporary && File.Exists(attachment.OriginalPath))
                File.Delete(attachment.OriginalPath);

            OnAttachmentsChanged?.Invoke();
        }

        /// <summary>Removes all attachments and cleans up temp files.</summary>
        public void ClearAll()
        {
            foreach (var att in _attachments)
            {
                if (att.IsTemporary && File.Exists(att.OriginalPath))
                    File.Delete(att.OriginalPath);
            }
            _attachments.Clear();
            _attachedPaths.Clear();
            OnAttachmentsChanged?.Invoke();
        }

        /// <summary>Whether the extension is an image type.</summary>
        public static bool IsImageExtension(string ext)
        {
            return ImageExtensions.Contains(ext);
        }

        static Texture2D LoadThumbnail(string path)
        {
            try
            {
                var bytes = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2);
                if (tex.LoadImage(bytes))
                    return tex;
            }
            catch { /* thumbnail is optional — return null on failure */ }
            return null;
        }
    }
}
