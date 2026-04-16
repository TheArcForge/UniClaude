// Editor/UI/Input/AttachmentChip.cs
using System;
using UnityEngine;
using UnityEngine.UIElements;
using static UniClaude.Editor.UI.ThemeContext;

namespace UniClaude.Editor.UI.Input
{
    /// <summary>
    /// A pill-shaped visual element showing an attached file: icon/thumbnail, filename,
    /// optional size warning, and an X button to remove.
    /// </summary>
    public class AttachmentChip : VisualElement
    {
        const int ThumbnailSize = 20;
        const int MaxFileNameLength = 20;

        /// <summary>Fires when the user clicks the X button.</summary>
        public event Action<AttachmentInfo> OnRemoveClicked;

        /// <summary>
        /// Creates an attachment chip displaying the given file info with theme-appropriate styling.
        /// </summary>
        /// <param name="info">Attachment metadata (filename, extension, thumbnail, size).</param>
        /// <param name="theme">Active theme context for colors and font sizes.</param>
        public AttachmentChip(AttachmentInfo info, ThemeContext theme)
        {
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.paddingLeft = 6;
            style.paddingRight = 4;
            style.paddingTop = 3;
            style.paddingBottom = 3;
            style.marginRight = 4;
            style.marginBottom = 4;
            style.borderTopLeftRadius = 12;
            style.borderTopRightRadius = 12;
            style.borderBottomLeftRadius = 12;
            style.borderBottomRightRadius = 12;
            style.backgroundColor = theme.IsDark
                ? new Color(0.25f, 0.25f, 0.28f)
                : new Color(0.85f, 0.85f, 0.88f);

            tooltip = info.OriginalPath;

            // Icon or thumbnail
            if (info.Thumbnail != null)
            {
                var img = new Image { image = info.Thumbnail };
                img.style.width = ThumbnailSize;
                img.style.height = ThumbnailSize;
                img.style.marginRight = 4;
                img.style.borderTopLeftRadius = 3;
                img.style.borderTopRightRadius = 3;
                img.style.borderBottomLeftRadius = 3;
                img.style.borderBottomRightRadius = 3;
                Add(img);
            }
            else
            {
                var iconLabel = new Label(GetFileIcon(info.Extension));
                iconLabel.style.fontSize = 12;
                iconLabel.style.marginRight = 4;
                iconLabel.style.color = theme.DimText;
                Add(iconLabel);
            }

            // Filename
            var displayName = info.FileName.Length > MaxFileNameLength
                ? info.FileName.Substring(0, MaxFileNameLength - 1) + "\u2026"
                : info.FileName;
            var nameLabel = new Label(displayName);
            nameLabel.style.fontSize = theme.FontSize(FontTier.Hint);
            nameLabel.style.color = theme.TextColor;
            Add(nameLabel);

            // Size warning
            if (info.IsLargeFile)
            {
                var sizeLabel = new Label(info.SizeLabel);
                sizeLabel.style.fontSize = theme.FontSize(FontTier.Hint);
                sizeLabel.style.color = new Color(0.9f, 0.65f, 0.2f); // amber warning
                sizeLabel.style.marginLeft = 4;
                Add(sizeLabel);
            }

            // X button
            var removeBtn = new Label("\u00d7"); // multiplication sign as X
            removeBtn.style.fontSize = 14;
            removeBtn.style.color = theme.DimText;
            removeBtn.style.marginLeft = 4;
            removeBtn.style.unityTextAlign = TextAnchor.MiddleCenter;
            removeBtn.AddToClassList("unity-cursor-link");
            removeBtn.RegisterCallback<ClickEvent>(_ => OnRemoveClicked?.Invoke(info));
            Add(removeBtn);
        }

        static string GetFileIcon(string ext)
        {
            ext = ext?.ToLowerInvariant() ?? "";
            if (ext == ".cs" || ext == ".shader" || ext == ".hlsl") return "\u2630"; // code
            if (ext == ".json" || ext == ".xml" || ext == ".yaml" || ext == ".yml") return "\u2699"; // config
            if (ext == ".unity" || ext == ".prefab" || ext == ".asset") return "\u25a0"; // Unity
            if (ext == ".mat" || ext == ".controller" || ext == ".anim") return "\u25c6"; // asset
            return "\u2610"; // generic file
        }
    }
}
