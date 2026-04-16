using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UniClaude.Editor.MCP;

namespace UniClaude.Editor.Tests.MCP
{
    /// <summary>
    /// Tests for <see cref="TagLayerTools"/> MCP tools.
    /// </summary>
    public class TagLayerToolsTests
    {
        [TearDown]
        public void TearDown()
        {
            RemoveTestTag("MCPTestTag");
            RemoveTestTag("MCPTestTag2");
            ClearTestLayer("MCPTestLayer");
        }

        [Test]
        public void TagCreate_CreatesNewTag()
        {
            var result = TagLayerTools.CreateTag("MCPTestTag");
            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            Assert.That(TagExists("MCPTestTag"), Is.True, "Tag should exist after creation");
        }

        [Test]
        public void TagCreate_Duplicate_ReturnsError()
        {
            TagLayerTools.CreateTag("MCPTestTag");
            var result = TagLayerTools.CreateTag("MCPTestTag");
            Assert.IsTrue(result.IsError);
            StringAssert.Contains("already exists", result.Text);
        }

        [Test]
        public void TagCreate_EmptyName_ReturnsError()
        {
            var result = TagLayerTools.CreateTag("");
            Assert.IsTrue(result.IsError);
        }

        [Test]
        public void TagDelete_RemovesCustomTag()
        {
            TagLayerTools.CreateTag("MCPTestTag");
            Assert.IsTrue(TagExists("MCPTestTag"), "Precondition: tag should exist");
            var result = TagLayerTools.DeleteTag("MCPTestTag");
            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            Assert.IsFalse(TagExists("MCPTestTag"));
        }

        [Test]
        public void TagDelete_BuiltIn_ReturnsError()
        {
            var result = TagLayerTools.DeleteTag("Untagged");
            Assert.IsTrue(result.IsError);
            StringAssert.Contains("built-in", result.Text);
        }

        [Test]
        public void TagDelete_NotFound_ReturnsError()
        {
            var result = TagLayerTools.DeleteTag("NonExistentTag999");
            Assert.IsTrue(result.IsError);
            StringAssert.Contains("not found", result.Text);
        }

        [Test]
        public void TagList_IncludesBuiltInTags()
        {
            var result = TagLayerTools.ListTags();
            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            StringAssert.Contains("Untagged", result.Text);
        }

        [Test]
        public void TagList_IncludesCustomTags()
        {
            TagLayerTools.CreateTag("MCPTestTag");
            var result = TagLayerTools.ListTags();
            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            StringAssert.Contains("MCPTestTag", result.Text);
        }

        [Test]
        public void LayerCreate_AssignsToFirstAvailableSlot()
        {
            var result = TagLayerTools.CreateLayer("MCPTestLayer");
            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            StringAssert.Contains("MCPTestLayer", result.Text);
            var layerIndex = LayerMask.NameToLayer("MCPTestLayer");
            Assert.That(layerIndex, Is.GreaterThanOrEqualTo(8));
        }

        [Test]
        public void LayerCreate_SpecificIndex_UsesIt()
        {
            int emptySlot = FindEmptyLayerSlot();
            if (emptySlot < 0)
            {
                Assert.Ignore("No empty layer slots available for test");
                return;
            }
            var result = TagLayerTools.CreateLayer("MCPTestLayer", emptySlot.ToString());
            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            Assert.AreEqual(emptySlot, LayerMask.NameToLayer("MCPTestLayer"));
        }

        [Test]
        public void LayerCreate_ReservedIndex_ReturnsError()
        {
            var result = TagLayerTools.CreateLayer("MCPTestLayer", "0");
            Assert.IsTrue(result.IsError);
            StringAssert.Contains("reserved", result.Text);
        }

        [Test]
        public void LayerList_IncludesDefaultLayer()
        {
            var result = TagLayerTools.ListLayers();
            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            StringAssert.Contains("Default", result.Text);
        }

        static bool TagExists(string tagName)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset");
            var so = new SerializedObject(asset);
            var tags = so.FindProperty("tags");
            for (int i = 0; i < tags.arraySize; i++)
            {
                if (tags.GetArrayElementAtIndex(i).stringValue == tagName)
                    return true;
            }
            return false;
        }

        static void RemoveTestTag(string tagName)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset");
            var so = new SerializedObject(asset);
            var tags = so.FindProperty("tags");
            for (int i = tags.arraySize - 1; i >= 0; i--)
            {
                if (tags.GetArrayElementAtIndex(i).stringValue == tagName)
                {
                    tags.DeleteArrayElementAtIndex(i);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    return;
                }
            }
        }

        static void ClearTestLayer(string layerName)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset");
            var so = new SerializedObject(asset);
            var layers = so.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                if (layers.GetArrayElementAtIndex(i).stringValue == layerName)
                {
                    layers.GetArrayElementAtIndex(i).stringValue = "";
                    so.ApplyModifiedPropertiesWithoutUndo();
                    return;
                }
            }
        }

        [Test]
        public void TagCreate_IsUndoable()
        {
            TagLayerTools.CreateTag("MCPTestTag");
            Assert.IsTrue(TagExists("MCPTestTag"), "Precondition: tag should exist");

            Undo.PerformUndo();

            Assert.IsFalse(TagExists("MCPTestTag"), "Tag should be removed after undo");
        }

        [Test]
        public void TagDelete_IsUndoable()
        {
            TagLayerTools.CreateTag("MCPTestTag");
            Assert.IsTrue(TagExists("MCPTestTag"), "Precondition: tag should exist");

            TagLayerTools.DeleteTag("MCPTestTag");
            Assert.IsFalse(TagExists("MCPTestTag"), "Precondition: tag should be deleted");

            Undo.PerformUndo();

            Assert.IsTrue(TagExists("MCPTestTag"), "Tag should be restored after undo");
        }

        [Test]
        public void LayerCreate_IsUndoable()
        {
            TagLayerTools.CreateLayer("MCPTestLayer");
            Assert.That(LayerMask.NameToLayer("MCPTestLayer"), Is.GreaterThanOrEqualTo(8));

            Undo.PerformUndo();

            Assert.AreEqual(-1, LayerMask.NameToLayer("MCPTestLayer"),
                "Layer should be removed after undo");
        }

        static int FindEmptyLayerSlot()
        {
            var asset = AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset");
            var so = new SerializedObject(asset);
            var layers = so.FindProperty("layers");
            for (int i = 8; i < layers.arraySize && i < 32; i++)
            {
                if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                    return i;
            }
            return -1;
        }
    }
}
