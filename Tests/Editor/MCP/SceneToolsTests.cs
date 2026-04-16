using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UniClaude.Editor.MCP;

namespace UniClaude.Editor.Tests.MCP
{
    /// <summary>
    /// Tests for <see cref="SceneTools"/> MCP tools covering hierarchy listing,
    /// creation, deletion, reparenting, and renaming of GameObjects.
    /// </summary>
    public class SceneToolsTests
    {
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
        /// <param name="name">Name for the temporary GameObject.</param>
        /// <returns>The created GameObject.</returns>
        GameObject CreateTemp(string name)
        {
            var go = new GameObject(name);
            _tempObjects.Add(go);
            return go;
        }

        [Test]
        public void GetSceneHierarchy_ReturnsTree()
        {
            CreateTemp("SceneToolsTest_HierarchyRoot");

            var result = SceneTools.GetSceneHierarchy();

            Assert.IsFalse(result.IsError);
            Assert.IsNotNull(result.Text);
            Assert.That(result.Text, Does.Contain("SceneToolsTest_HierarchyRoot"));
        }

        [Test]
        public void CreateGameObject_CreatesAtRoot()
        {
            var result = SceneTools.CreateGameObject("SceneToolsTest_NewRoot", null);

            Assert.IsFalse(result.IsError);
            Assert.That(result.Text, Does.Contain("SceneToolsTest_NewRoot"));

            var found = GameObject.Find("SceneToolsTest_NewRoot");
            Assert.IsNotNull(found);
            _tempObjects.Add(found);
        }

        [Test]
        public void CreateGameObject_WithParent()
        {
            var parent = CreateTemp("SceneToolsTest_Parent");

            var result = SceneTools.CreateGameObject("SceneToolsTest_Child", "SceneToolsTest_Parent");

            Assert.IsFalse(result.IsError);

            var child = GameObject.Find("SceneToolsTest_Parent/SceneToolsTest_Child");
            Assert.IsNotNull(child);
            Assert.AreEqual(parent.transform, child.transform.parent);
            _tempObjects.Add(child);
        }

        [Test]
        public void CreateGameObject_InvalidParent_ErrorWithSuggestions()
        {
            var marker = CreateTemp("SceneToolsTest_Marker");

            var result = SceneTools.CreateGameObject("SomeChild", "NonExistentParent_12345");

            Assert.IsTrue(result.IsError);
            Assert.That(result.Text, Does.Contain("Parent not found: NonExistentParent_12345"));
            Assert.That(result.Text, Does.Contain("Root objects in scene:"));
            Assert.That(result.Text, Does.Contain("SceneToolsTest_Marker"));
        }

        [Test]
        public void DeleteGameObject_RemovesFromScene()
        {
            var go = CreateTemp("SceneToolsTest_ToDelete");

            var result = SceneTools.DeleteGameObject("SceneToolsTest_ToDelete");

            Assert.IsFalse(result.IsError);
            Assert.That(result.Text, Does.Contain("SceneToolsTest_ToDelete"));

            var found = GameObject.Find("SceneToolsTest_ToDelete");
            Assert.IsNull(found);

            // Already destroyed, remove from cleanup list to avoid double-destroy
            _tempObjects.Remove(go);
        }

        [Test]
        public void DeleteGameObject_NotFound_ErrorWithSuggestions()
        {
            var marker = CreateTemp("SceneToolsTest_DeleteMarker");

            var result = SceneTools.DeleteGameObject("NonExistentObject_67890");

            Assert.IsTrue(result.IsError);
            Assert.That(result.Text, Does.Contain("GameObject not found: NonExistentObject_67890"));
            Assert.That(result.Text, Does.Contain("Root objects in scene:"));
            Assert.That(result.Text, Does.Contain("SceneToolsTest_DeleteMarker"));
        }

        [Test]
        public void ReparentGameObject_MovesUnderNewParent()
        {
            var child = CreateTemp("SceneToolsTest_ReparentChild");
            var newParent = CreateTemp("SceneToolsTest_ReparentTarget");

            Assert.IsNull(child.transform.parent);

            var result = SceneTools.ReparentGameObject("SceneToolsTest_ReparentChild", "SceneToolsTest_ReparentTarget");

            Assert.IsFalse(result.IsError);
            Assert.AreEqual(newParent.transform, child.transform.parent);
            Assert.That(result.Text, Does.Contain("SceneToolsTest_ReparentTarget/SceneToolsTest_ReparentChild"));
        }

        [Test]
        public void ReparentGameObject_NotFound_ErrorWithSuggestions()
        {
            var marker = CreateTemp("SceneToolsTest_ReparentMarker");

            var result = SceneTools.ReparentGameObject("NonExistentReparent_99999", "SceneToolsTest_ReparentMarker");

            Assert.IsTrue(result.IsError);
            Assert.That(result.Text, Does.Contain("GameObject not found: NonExistentReparent_99999"));
            Assert.That(result.Text, Does.Contain("Root objects in scene:"));
        }

        [Test]
        public void ReparentGameObject_InvalidNewParent_ErrorWithSuggestions()
        {
            var child = CreateTemp("SceneToolsTest_ReparentChild2");

            var result = SceneTools.ReparentGameObject("SceneToolsTest_ReparentChild2", "NonExistentParent_88888");

            Assert.IsTrue(result.IsError);
            Assert.That(result.Text, Does.Contain("GameObject not found: NonExistentParent_88888"));
            Assert.That(result.Text, Does.Contain("Root objects in scene:"));
        }

        [Test]
        public void RenameGameObject_ChangesName()
        {
            var go = CreateTemp("SceneToolsTest_OldName");

            var result = SceneTools.RenameGameObject("SceneToolsTest_OldName", "SceneToolsTest_NewName");

            Assert.IsFalse(result.IsError);
            Assert.AreEqual("SceneToolsTest_NewName", go.name);
            Assert.That(result.Text, Does.Contain("SceneToolsTest_NewName"));
        }

        [Test]
        public void RenameGameObject_NotFound_ErrorWithSuggestions()
        {
            var marker = CreateTemp("SceneToolsTest_RenameMarker");

            var result = SceneTools.RenameGameObject("NonExistentRename_77777", "SomeNewName");

            Assert.IsTrue(result.IsError);
            Assert.That(result.Text, Does.Contain("GameObject not found: NonExistentRename_77777"));
            Assert.That(result.Text, Does.Contain("Root objects in scene:"));
            Assert.That(result.Text, Does.Contain("SceneToolsTest_RenameMarker"));
        }

        [Test]
        public void SceneSetup_CreatesSingleGameObject()
        {
            var input = JsonConvert.SerializeObject(new[]
            {
                new { name = "TestPlayer" }
            });

            var result = SceneTools.SceneSetup(input);

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            var go = GameObject.Find("TestPlayer");
            Assert.IsNotNull(go, "TestPlayer should exist in scene");
            _tempObjects.Add(go);
        }

        [Test]
        public void SceneSetup_SetsTransform()
        {
            var input = JsonConvert.SerializeObject(new[]
            {
                new
                {
                    name = "Positioned",
                    position = new[] { 1f, 2f, 3f },
                    rotation = new[] { 0f, 90f, 0f },
                    scale = new[] { 2f, 2f, 2f }
                }
            });

            var result = SceneTools.SceneSetup(input);

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            var go = GameObject.Find("Positioned");
            Assert.IsNotNull(go);
            _tempObjects.Add(go);
            Assert.AreEqual(1f, go.transform.localPosition.x, 0.01f);
            Assert.AreEqual(2f, go.transform.localPosition.y, 0.01f);
            Assert.AreEqual(3f, go.transform.localPosition.z, 0.01f);
            Assert.AreEqual(90f, go.transform.localEulerAngles.y, 0.01f);
            Assert.AreEqual(2f, go.transform.localScale.x, 0.01f);
        }

        [Test]
        public void SceneSetup_AddsComponents()
        {
            var input = JsonConvert.SerializeObject(new[]
            {
                new
                {
                    name = "WithComponents",
                    components = new[]
                    {
                        new { type = "BoxCollider" },
                        new { type = "Rigidbody" }
                    }
                }
            });

            var result = SceneTools.SceneSetup(input);

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            var go = GameObject.Find("WithComponents");
            Assert.IsNotNull(go);
            _tempObjects.Add(go);
            Assert.IsNotNull(go.GetComponent<BoxCollider>());
            Assert.IsNotNull(go.GetComponent<Rigidbody>());
        }

        [Test]
        public void SceneSetup_SetsComponentProperties()
        {
            var input = JsonConvert.SerializeObject(new[]
            {
                new
                {
                    name = "WithProps",
                    components = new object[]
                    {
                        new
                        {
                            type = "Rigidbody",
                            properties = new Dictionary<string, string>
                            {
                                { "m_UseGravity", "false" },
                                { "m_Mass", "5" }
                            }
                        }
                    }
                }
            });

            var result = SceneTools.SceneSetup(input);

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            var go = GameObject.Find("WithProps");
            Assert.IsNotNull(go);
            _tempObjects.Add(go);
            var rb = go.GetComponent<Rigidbody>();
            Assert.IsNotNull(rb);
            Assert.IsFalse(rb.useGravity);
            Assert.AreEqual(5f, rb.mass, 0.01f);
        }

        [Test]
        public void SceneSetup_CreatesNestedChildren()
        {
            var input = JsonConvert.SerializeObject(new[]
            {
                new
                {
                    name = "Parent",
                    children = new[]
                    {
                        new
                        {
                            name = "Child",
                            position = new[] { 0f, -0.5f, 0f }
                        }
                    }
                }
            });

            var result = SceneTools.SceneSetup(input);

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            var parent = GameObject.Find("Parent");
            Assert.IsNotNull(parent);
            _tempObjects.Add(parent);
            Assert.AreEqual(1, parent.transform.childCount);
            var child = parent.transform.GetChild(0).gameObject;
            Assert.AreEqual("Child", child.name);
            Assert.AreEqual(-0.5f, child.transform.localPosition.y, 0.01f);
        }

        [Test]
        public void SceneSetup_ParentsUnderExistingObject()
        {
            var existing = CreateTemp("ExistingParent");

            var input = JsonConvert.SerializeObject(new[]
            {
                new { name = "NewChild", parent = "ExistingParent" }
            });

            var result = SceneTools.SceneSetup(input);

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            Assert.AreEqual(1, existing.transform.childCount);
            var child = existing.transform.GetChild(0).gameObject;
            Assert.AreEqual("NewChild", child.name);
            _tempObjects.Add(child);
        }

        [Test]
        public void SceneSetup_ContinuesOnError_ReportsFailures()
        {
            var input = JsonConvert.SerializeObject(new[]
            {
                new
                {
                    name = "Good",
                    components = new[]
                    {
                        new { type = "BoxCollider" }
                    }
                },
                new
                {
                    name = "Bad",
                    components = new[]
                    {
                        new { type = "NonExistentComponent999" }
                    }
                }
            });

            var result = SceneTools.SceneSetup(input);

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            var good = GameObject.Find("Good");
            Assert.IsNotNull(good);
            _tempObjects.Add(good);
            Assert.IsNotNull(good.GetComponent<BoxCollider>());
            var bad = GameObject.Find("Bad");
            Assert.IsNotNull(bad, "Bad GO should still be created even if component failed");
            _tempObjects.Add(bad);
            Assert.That(result.Text, Does.Contain("NonExistentComponent999"));
        }

        [Test]
        public void SceneSetup_EmptyInput_ReturnsSuccess()
        {
            var input = JsonConvert.SerializeObject(new object[] { });

            var result = SceneTools.SceneSetup(input);

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            Assert.That(result.Text, Does.Contain("0"));
        }

        [Test]
        public void SceneSetup_SetsTagAndLayer()
        {
            var input = JsonConvert.SerializeObject(new[]
            {
                new
                {
                    name = "Tagged",
                    tag = "Player",
                    layer = "Default"
                }
            });

            var result = SceneTools.SceneSetup(input);

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            var go = GameObject.Find("Tagged");
            Assert.IsNotNull(go);
            _tempObjects.Add(go);
            Assert.AreEqual("Player", go.tag);
            Assert.AreEqual(LayerMask.NameToLayer("Default"), go.layer);
        }
    }
}
