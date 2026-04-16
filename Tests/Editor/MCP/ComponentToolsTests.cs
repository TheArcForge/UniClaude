using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UniClaude.Editor.MCP;

namespace UniClaude.Editor.Tests.MCP
{
    /// <summary>
    /// Tests for <see cref="ComponentTools"/> MCP tools.
    /// </summary>
    public class ComponentToolsTests
    {
        GameObject _tempGO;
        string _originalScenePath;

        [SetUp]
        public void SetUp()
        {
            _originalScenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _tempGO = new GameObject("ComponentToolsTestGO");
        }

        [TearDown]
        public void TearDown()
        {
            if (_tempGO != null)
                Object.DestroyImmediate(_tempGO);

            // Clean up any other test objects that may have been created
            var leftover = GameObject.Find("ComponentToolsTestGO");
            if (leftover != null)
                Object.DestroyImmediate(leftover);

            var extra = GameObject.Find("ComponentToolsTestGO2");
            if (extra != null)
                Object.DestroyImmediate(extra);

            // Discard dirty test scene without save prompt, then restore original
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!string.IsNullOrEmpty(_originalScenePath))
                EditorSceneManager.OpenScene(_originalScenePath);
        }

        [Test]
        public void AddComponent_AddsToGameObject()
        {
            var result = ComponentTools.AddComponent("ComponentToolsTestGO", "BoxCollider");

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            Assert.IsNotNull(_tempGO.GetComponent<BoxCollider>());
        }

        [Test]
        public void AddComponent_TypeNotFound_ReturnsError()
        {
            var result = ComponentTools.AddComponent("ComponentToolsTestGO", "NonExistentComponent123");

            Assert.IsTrue(result.IsError);
            StringAssert.Contains("NonExistentComponent123", result.Text);
            StringAssert.Contains("not found", result.Text);
        }

        [Test]
        public void AddComponent_GONotFound_ErrorWithSuggestions()
        {
            var result = ComponentTools.AddComponent("NoSuchGameObject999", "BoxCollider");

            Assert.IsTrue(result.IsError);
            StringAssert.Contains("NoSuchGameObject999", result.Text);
            StringAssert.Contains("Root objects", result.Text);
        }

        [Test]
        public void RemoveComponent_RemovesFromGameObject()
        {
            _tempGO.AddComponent<BoxCollider>();
            Assert.IsNotNull(_tempGO.GetComponent<BoxCollider>(), "Precondition: BoxCollider should exist");

            var result = ComponentTools.RemoveComponent("ComponentToolsTestGO", "BoxCollider");

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            Assert.IsNull(_tempGO.GetComponent<BoxCollider>());
        }

        [Test]
        public void RemoveComponent_NotFound_ListsExisting()
        {
            // GO only has Transform by default
            var result = ComponentTools.RemoveComponent("ComponentToolsTestGO", "BoxCollider");

            Assert.IsTrue(result.IsError);
            StringAssert.Contains("BoxCollider", result.Text);
            StringAssert.Contains("Transform", result.Text);
        }

        [Test]
        public void GetComponents_ListsAll()
        {
            _tempGO.AddComponent<BoxCollider>();
            _tempGO.AddComponent<Rigidbody>();

            var result = ComponentTools.GetComponents("ComponentToolsTestGO");

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            StringAssert.Contains("Transform", result.Text);
            StringAssert.Contains("BoxCollider", result.Text);
            StringAssert.Contains("Rigidbody", result.Text);
        }

        [Test]
        public void FindComponents_FindsMatching()
        {
            _tempGO.AddComponent<Camera>();

            var result = ComponentTools.FindComponents("Camera");

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            StringAssert.Contains("ComponentToolsTestGO", result.Text);
        }

        [Test]
        public void GetComponentProperty_ReadsValue()
        {
            _tempGO.transform.localPosition = new Vector3(1f, 2f, 3f);

            var result = ComponentTools.GetComponentProperty(
                "ComponentToolsTestGO", "Transform", "m_LocalPosition");

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            StringAssert.Contains("1", result.Text);
            StringAssert.Contains("2", result.Text);
            StringAssert.Contains("3", result.Text);
        }

        [Test]
        public void GetComponentProperty_NotFound_ListsValid()
        {
            var result = ComponentTools.GetComponentProperty(
                "ComponentToolsTestGO", "Transform", "nonExistentProperty");

            Assert.IsTrue(result.IsError);
            StringAssert.Contains("nonExistentProperty", result.Text);
            StringAssert.Contains("Valid properties", result.Text);
            // Transform should have m_LocalPosition as a valid property
            StringAssert.Contains("m_LocalPosition", result.Text);
        }

        [Test]
        public void SetComponentProperty_SetsValue()
        {
            _tempGO.transform.localPosition = Vector3.zero;

            var result = ComponentTools.SetComponentProperty(
                "ComponentToolsTestGO", "Transform", "m_LocalPosition", "{\"x\":5,\"y\":10,\"z\":15}");

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            Assert.AreEqual(5f, _tempGO.transform.localPosition.x, 0.001f);
            Assert.AreEqual(10f, _tempGO.transform.localPosition.y, 0.001f);
            Assert.AreEqual(15f, _tempGO.transform.localPosition.z, 0.001f);
        }

        [Test]
        public void SetComponentProperty_NotFound_ListsValid()
        {
            var result = ComponentTools.SetComponentProperty(
                "ComponentToolsTestGO", "Transform", "nonExistentProperty", "42");

            Assert.IsTrue(result.IsError);
            StringAssert.Contains("nonExistentProperty", result.Text);
            StringAssert.Contains("Valid properties", result.Text);
            // Should include property types in the listing
            StringAssert.Contains("m_LocalPosition", result.Text);
        }

        [Test]
        public void SetProperties_SetsMultiplePropertiesOnOneComponent()
        {
            _tempGO.AddComponent<Rigidbody>();

            var input = JsonConvert.SerializeObject(new[]
            {
                new
                {
                    gameObject = "ComponentToolsTestGO",
                    component = "Rigidbody",
                    properties = new Dictionary<string, string>
                    {
                        { "m_UseGravity", "false" },
                        { "m_Mass", "10" }
                    }
                }
            });

            var result = ComponentTools.SetProperties(input);

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            var rb = _tempGO.GetComponent<Rigidbody>();
            Assert.IsFalse(rb.useGravity);
            Assert.AreEqual(10f, rb.mass, 0.01f);
        }

        [Test]
        public void SetProperties_AcrossMultipleGameObjects()
        {
            _tempGO.AddComponent<BoxCollider>();
            var go2 = new GameObject("ComponentToolsTestGO2");
            go2.AddComponent<BoxCollider>();

            var input = JsonConvert.SerializeObject(new[]
            {
                new
                {
                    gameObject = "ComponentToolsTestGO",
                    component = "BoxCollider",
                    properties = new Dictionary<string, string> { { "m_IsTrigger", "true" } }
                },
                new
                {
                    gameObject = "ComponentToolsTestGO2",
                    component = "BoxCollider",
                    properties = new Dictionary<string, string> { { "m_IsTrigger", "true" } }
                }
            });

            var result = ComponentTools.SetProperties(input);

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            Assert.IsTrue(_tempGO.GetComponent<BoxCollider>().isTrigger);
            Assert.IsTrue(go2.GetComponent<BoxCollider>().isTrigger);
            Object.DestroyImmediate(go2);
        }

        [Test]
        public void SetProperties_ContinuesOnError_ReportsFailures()
        {
            _tempGO.AddComponent<BoxCollider>();

            var input = JsonConvert.SerializeObject(new[]
            {
                new
                {
                    gameObject = "ComponentToolsTestGO",
                    component = "BoxCollider",
                    properties = new Dictionary<string, string> { { "m_IsTrigger", "true" } }
                },
                new
                {
                    gameObject = "NonExistentGO999",
                    component = "BoxCollider",
                    properties = new Dictionary<string, string> { { "m_IsTrigger", "true" } }
                }
            });

            var result = ComponentTools.SetProperties(input);

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            Assert.IsTrue(_tempGO.GetComponent<BoxCollider>().isTrigger);
            Assert.That(result.Text, Does.Contain("NonExistentGO999"));
        }

        [Test]
        public void SetProperties_EmptyInput_ReturnsSuccess()
        {
            var input = JsonConvert.SerializeObject(new object[] { });

            var result = ComponentTools.SetProperties(input);

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            Assert.That(result.Text, Does.Contain("0"));
        }

        [Test]
        public void SetProperties_InvalidComponent_ReportsError()
        {
            var input = JsonConvert.SerializeObject(new[]
            {
                new
                {
                    gameObject = "ComponentToolsTestGO",
                    component = "FakeComponent999",
                    properties = new Dictionary<string, string> { { "m_Foo", "bar" } }
                }
            });

            var result = ComponentTools.SetProperties(input);

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            Assert.That(result.Text, Does.Contain("FakeComponent999"));
        }

        [Test]
        public void SetProperty_DisplayName_ResolvesToSerializedName()
        {
            _tempGO.AddComponent<Camera>();
            // "Orthographic Size" is the display name for "orthographic size" serialized prop
            var result = ComponentTools.SetComponentProperty(
                "ComponentToolsTestGO", "Camera", "Orthographic Size", "10");
            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
        }

        [Test]
        public void SetProperty_NormalizedName_ResolvesToSerializedName()
        {
            _tempGO.AddComponent<Camera>();
            // "orthographic_size" should normalize to match "Orthographic Size"
            var result = ComponentTools.SetComponentProperty(
                "ComponentToolsTestGO", "Camera", "orthographic_size", "10");
            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
        }

        [Test]
        public void SetProperty_CaseInsensitive_ResolvesToSerializedName()
        {
            _tempGO.AddComponent<Camera>();
            var result = ComponentTools.SetComponentProperty(
                "ComponentToolsTestGO", "Camera", "ORTHOGRAPHIC SIZE", "10");
            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
        }

        [Test]
        public void SetProperty_ExactSerializedName_StillWorks()
        {
            _tempGO.AddComponent<Camera>();
            var result = ComponentTools.SetComponentProperty(
                "ComponentToolsTestGO", "Camera", "orthographic size", "10");
            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
        }

        [Test]
        public void ListProperties_Camera_ReturnsProperties()
        {
            _tempGO.AddComponent<Camera>();
            var result = ComponentTools.ListComponentProperties(
                "ComponentToolsTestGO", "Camera");

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            StringAssert.Contains("orthographic size", result.Text);
            StringAssert.Contains("Orthographic size", result.Text);
        }

        [Test]
        public void ListProperties_GONotFound_ReturnsError()
        {
            var result = ComponentTools.ListComponentProperties(
                "NoSuchGO999", "Camera");
            Assert.IsTrue(result.IsError);
            StringAssert.Contains("not found", result.Text);
        }

        [Test]
        public void ListProperties_ComponentNotFound_ReturnsError()
        {
            var result = ComponentTools.ListComponentProperties(
                "ComponentToolsTestGO", "Camera");
            Assert.IsTrue(result.IsError);
            StringAssert.Contains("not found", result.Text);
        }

        [Test]
        public void SetProperties_NestedJsonValues_Works()
        {
            _tempGO.AddComponent<Camera>();
            var json = JsonConvert.SerializeObject(new[]
            {
                new
                {
                    gameObject = "ComponentToolsTestGO",
                    component = "Camera",
                    properties = new Dictionary<string, object>
                    {
                        { "m_BackGroundColor", new { r = 0.5, g = 0.5, b = 0.5, a = 1.0 } }
                    }
                }
            });

            var result = ComponentTools.SetProperties(json);
            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
        }
    }
}
