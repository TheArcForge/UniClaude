// Tests/Editor/ChatInputFieldTests.cs
using NUnit.Framework;
using UniClaude.Editor.UI.Input;

namespace UniClaude.Editor.Tests
{
    [TestFixture]
    public class ChatInputFieldTests
    {
        [Test]
        public void ComputeHeight_EmptyText_ReturnsMinHeight()
        {
            var height = ChatInputField.ComputeDesiredHeight(0, 20f, 36f, 160f);
            Assert.AreEqual(36f, height);
        }

        [Test]
        public void ComputeHeight_ThreeLines_GrowsToFitContent()
        {
            var height = ChatInputField.ComputeDesiredHeight(3, 20f, 36f, 160f);
            Assert.Greater(height, 36f);
            Assert.LessOrEqual(height, 160f);
        }

        [Test]
        public void ComputeHeight_TwentyLines_CapsAtMaxHeight()
        {
            var height = ChatInputField.ComputeDesiredHeight(20, 20f, 36f, 160f);
            Assert.AreEqual(160f, height);
        }

        [Test]
        public void ClampResize_WithinBounds_ReturnsValue()
        {
            Assert.AreEqual(100f, ChatInputField.ClampResizeHeight(100f, 36f, 160f));
        }

        [Test]
        public void ClampResize_BelowMin_ReturnsMin()
        {
            Assert.AreEqual(36f, ChatInputField.ClampResizeHeight(10f, 36f, 160f));
        }

        [Test]
        public void ClampResize_AboveMax_ReturnsMax()
        {
            Assert.AreEqual(160f, ChatInputField.ClampResizeHeight(300f, 36f, 160f));
        }
    }
}
