// Editor/UI/Input/AttachmentChipStrip.cs
using UniClaude.Editor.UI;
using UnityEngine.UIElements;

namespace UniClaude.Editor.UI.Input
{
    /// <summary>
    /// A horizontal flex-wrap container that displays <see cref="AttachmentChip"/> elements.
    /// Hidden when no attachments exist.
    /// </summary>
    public class AttachmentChipStrip : VisualElement
    {
        readonly AttachmentManager _manager;
        readonly ThemeContext _theme;

        /// <summary>
        /// Creates a chip strip that auto-rebuilds when the manager's attachment list changes.
        /// Subscribes to <see cref="AttachmentManager.OnAttachmentsChanged"/> and unsubscribes on detach.
        /// </summary>
        /// <param name="manager">The attachment manager whose list drives the strip contents.</param>
        /// <param name="theme">Active theme context for chip styling.</param>
        public AttachmentChipStrip(AttachmentManager manager, ThemeContext theme)
        {
            _manager = manager;
            _theme = theme;

            style.flexDirection = FlexDirection.Row;
            style.flexWrap = Wrap.Wrap;
            style.paddingLeft = 8;
            style.paddingRight = 8;
            style.paddingTop = 4;
            style.paddingBottom = 0;
            style.display = DisplayStyle.None;

            _manager.OnAttachmentsChanged += Rebuild;

            RegisterCallback<DetachFromPanelEvent>(_ => _manager.OnAttachmentsChanged -= Rebuild);
        }

        void Rebuild()
        {
            Clear();

            if (_manager.Attachments.Count == 0)
            {
                style.display = DisplayStyle.None;
                return;
            }

            style.display = DisplayStyle.Flex;

            foreach (var info in _manager.Attachments)
            {
                var chip = new AttachmentChip(info, _theme);
                chip.OnRemoveClicked += att => _manager.Remove(att);
                Add(chip);
            }
        }
    }
}
