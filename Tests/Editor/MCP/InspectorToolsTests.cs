using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UniClaude.Editor.MCP;

namespace UniClaude.Editor.Tests.MCP
{
    /// <summary>
    /// Tests for <see cref="InspectorTools"/> MCP tools covering GameObject selection
    /// and full property inspection via SerializedObject.
    /// </summary>
    public class InspectorToolsTests
    {
        /// <summary>Tracks scene GameObjects created during tests for cleanup.</summary>
        List<GameObject> _tempObjects;
        string _originalScenePath;

        [SetUp]
        public void SetUp()
        {
            _tempObjects = new List<GameObject>();
            _originalScenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _tempObjects)
            {
                if (go != null)
                    Object.DestroyImmediate(go);
            }
            _tempObjects.Clear();

            // Discard dirty test scene without save prompt, then restore original
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!string.IsNullOrEmpty(_originalScenePath))
                EditorSceneManager.OpenScene(_originalScenePath);
        }

        /// <summary>
        /// Creates a temporary GameObject that will be cleaned up in TearDown.
        /// </summary>
        GameObject CreateTemp(string name)
        {
            var go = new GameObject(name);
            _tempObjects.Add(go);
            return go;
        }

        // ── SelectGameObject ──

        [Test]
        public void SelectGameObject_SetsSelection()
        {
            var go = CreateTemp("InspectorTest_Select");

            var result = InspectorTools.SelectGameObject("InspectorTest_Select");

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            Assert.That(result.Text, Does.Contain("InspectorTest_Select"));
            Assert.That(result.Text, Does.Contain("\"selected\":"));
        }

        [Test]
        public void SelectGameObject_NotFound_Error()
        {
            var marker = CreateTemp("InspectorTest_Marker");

            var result = InspectorTools.SelectGameObject("NonExistentGO_InspectorTest_99999");

            Assert.IsTrue(result.IsError);
            Assert.That(result.Text, Does.Contain("GameObject not found"));
            Assert.That(result.Text, Does.Contain("Root objects in scene:"));
            Assert.That(result.Text, Does.Contain("InspectorTest_Marker"));
        }

        // ── InspectGameObject ──

        [Test]
        public void InspectGameObject_ReturnsTransformAndComponents()
        {
            var go = CreateTemp("InspectorTest_Inspect");
            go.AddComponent<BoxCollider>();

            var result = InspectorTools.InspectGameObject("InspectorTest_Inspect");

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            Assert.That(result.Text, Does.Contain("InspectorTest_Inspect"));
            Assert.That(result.Text, Does.Contain("Transform"));
            Assert.That(result.Text, Does.Contain("BoxCollider"));
            Assert.That(result.Text, Does.Contain("\"position\":"));
            Assert.That(result.Text, Does.Contain("\"properties\":"));
        }

        [Test]
        public void InspectGameObject_DumpsSerializedProperties()
        {
            var go = CreateTemp("InspectorTest_Props");
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(2, 3, 4);

            var result = InspectorTools.InspectGameObject("InspectorTest_Props");

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            // SerializedObject should dump "m_Size" or "size" property
            Assert.That(result.Text, Does.Contain("Size").Or.Contain("size").Or.Contain("m_Size"));
        }

        [Test]
        public void InspectGameObject_NotFound_Error()
        {
            var marker = CreateTemp("InspectorTest_InspectMarker");

            var result = InspectorTools.InspectGameObject("NonExistentGO_InspectTest_99999");

            Assert.IsTrue(result.IsError);
            Assert.That(result.Text, Does.Contain("GameObject not found"));
            Assert.That(result.Text, Does.Contain("InspectorTest_InspectMarker"));
        }

        [Test]
        public void InspectGameObject_ShowsChildNames()
        {
            var parent = CreateTemp("InspectorTest_Parent");
            var child = new GameObject("InspectorTest_Child");
            child.transform.SetParent(parent.transform);
            _tempObjects.Add(child);

            var result = InspectorTools.InspectGameObject("InspectorTest_Parent");

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            Assert.That(result.Text, Does.Contain("InspectorTest_Child"));
            Assert.That(result.Text, Does.Contain("\"childCount\": 1"));
        }
    }
}
