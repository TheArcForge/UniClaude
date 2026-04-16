using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UniClaude.Editor.MCP;

namespace UniClaude.Editor.Tests.MCP
{
    /// <summary>
    /// Tests for <see cref="ReferenceTools"/> MCP tools.
    /// </summary>
    public class ReferenceToolsTests
    {
        const string TestSpritePath = "Assets/RefToolsTestSprite.png";

        GameObject _managerGO;
        GameObject _targetGO;
        string _originalScenePath;

        [SetUp]
        public void SetUp()
        {
            _originalScenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _managerGO = new GameObject("RefTestManager");
            _targetGO = new GameObject("RefTestTarget");
            _targetGO.AddComponent<Camera>();
            CreateTestSprite();
        }

        [TearDown]
        public void TearDown()
        {
            if (_managerGO != null) Object.DestroyImmediate(_managerGO);
            if (_targetGO != null) Object.DestroyImmediate(_targetGO);
            DeleteTestSprite();
            // Discard dirty test scene without save prompt, then restore original
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!string.IsNullOrEmpty(_originalScenePath))
                EditorSceneManager.OpenScene(_originalScenePath);
        }

        static void CreateTestSprite()
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(TestSpritePath) != null)
                return;

            var tex = new Texture2D(4, 4);
            var bytes = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            System.IO.File.WriteAllBytes(TestSpritePath, bytes);
            AssetDatabase.ImportAsset(TestSpritePath, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(TestSpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
        }

        static void DeleteTestSprite()
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(TestSpritePath) != null)
                AssetDatabase.DeleteAsset(TestSpritePath);
        }

        [Test]
        public void ReferenceSet_SceneGameObject_SetsReference()
        {
            _managerGO.AddComponent<ConfigurableJoint>();
            _targetGO.AddComponent<Rigidbody>();

            var result = ReferenceTools.SetReference(
                "RefTestManager", "ConfigurableJoint", "m_ConnectedBody",
                targetPath: "RefTestTarget", targetComponentType: "Rigidbody");

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            var joint = _managerGO.GetComponent<ConfigurableJoint>();
            Assert.AreEqual(_targetGO.GetComponent<Rigidbody>(), joint.connectedBody);
        }

        [Test]
        public void ReferenceSet_SceneGameObject_WithoutComponentType_SetsGameObject()
        {
            _managerGO.AddComponent<ConfigurableJoint>();

            var result = ReferenceTools.SetReference(
                "RefTestManager", "ConfigurableJoint", "m_ConnectedBody",
                targetPath: "RefTestTarget");

            // This should fail because ConnectedBody expects Rigidbody, not GameObject
            Assert.IsTrue(result.IsError);
            StringAssert.Contains("Type mismatch", result.Text);
        }

        [Test]
        public void ReferenceSet_AssetPath_SetsReference()
        {
            _managerGO.AddComponent<MeshFilter>();

            var result = ReferenceTools.SetReference(
                "RefTestManager", "MeshFilter", "m_Mesh",
                targetAssetPath: "Library/unity default resources::Cube");

            if (result.IsError)
                StringAssert.Contains("not found", result.Text);
        }

        [Test]
        public void ReferenceSet_NeitherTarget_ReturnsError()
        {
            _managerGO.AddComponent<ConfigurableJoint>();

            var result = ReferenceTools.SetReference(
                "RefTestManager", "ConfigurableJoint", "m_ConnectedBody");

            Assert.IsTrue(result.IsError);
            StringAssert.Contains("targetPath", result.Text);
        }

        [Test]
        public void ReferenceSet_BothTargets_ReturnsError()
        {
            _managerGO.AddComponent<ConfigurableJoint>();

            var result = ReferenceTools.SetReference(
                "RefTestManager", "ConfigurableJoint", "m_ConnectedBody",
                targetPath: "RefTestTarget", targetAssetPath: "Assets/Foo.asset");

            Assert.IsTrue(result.IsError);
        }

        [Test]
        public void ReferenceGet_NullReference_ReturnsNull()
        {
            _managerGO.AddComponent<ConfigurableJoint>();

            var result = ReferenceTools.GetReference(
                "RefTestManager", "ConfigurableJoint", "m_ConnectedBody");

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            StringAssert.Contains("null", result.Text);
        }

        [Test]
        public void ReferenceGet_SetReference_ReturnsInfo()
        {
            _managerGO.AddComponent<ConfigurableJoint>();
            _targetGO.AddComponent<Rigidbody>();
            var joint = _managerGO.GetComponent<ConfigurableJoint>();
            joint.connectedBody = _targetGO.GetComponent<Rigidbody>();

            var result = ReferenceTools.GetReference(
                "RefTestManager", "ConfigurableJoint", "m_ConnectedBody");

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            StringAssert.Contains("RefTestTarget", result.Text);
        }

        [Test]
        public void ReferenceFindUnset_FindsNullReferences()
        {
            _managerGO.AddComponent<ConfigurableJoint>();

            var result = ReferenceTools.FindUnsetReferences("RefTestManager", "ConfigurableJoint");

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            StringAssert.Contains("m_ConnectedBody", result.Text);
        }

        [Test]
        public void ReferenceFindUnset_GONotFound_ReturnsError()
        {
            var result = ReferenceTools.FindUnsetReferences("NonExistentGO999");

            Assert.IsTrue(result.IsError);
            StringAssert.Contains("NonExistentGO999", result.Text);
        }

        [Test]
        public void ReferenceSet_SpriteAutoResolve_SetsSpriteNotTexture()
        {
            // This test validates the auto-resolve logic works when a Sprite sub-asset exists.
            _managerGO.AddComponent<SpriteRenderer>();

            // Use the test sprite created in SetUp (imported as Sprite type)
            var result = ReferenceTools.SetReference(
                "RefTestManager", "SpriteRenderer", "m_Sprite",
                targetAssetPath: TestSpritePath);

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");

            var sr = _managerGO.GetComponent<SpriteRenderer>();
            Assert.IsNotNull(sr.sprite, "Sprite should have been assigned");
            Assert.IsInstanceOf<Sprite>(sr.sprite);
        }
    }
}
