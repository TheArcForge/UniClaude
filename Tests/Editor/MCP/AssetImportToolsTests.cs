using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UniClaude.Editor.MCP;

namespace UniClaude.Editor.Tests.MCP
{
    public class AssetImportToolsTests
    {
        const string TestFolder = "Assets/UniClaudeTestTemp";
        string _texturePath;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TestFolder))
                AssetDatabase.CreateFolder("Assets", "UniClaudeTestTemp");

            _texturePath = $"{TestFolder}/TestTexture.png";
            var tex = new Texture2D(4, 4);
            var bytes = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            System.IO.File.WriteAllBytes(_texturePath, bytes);
            AssetDatabase.ImportAsset(_texturePath, ImportAssetOptions.ForceUpdate);
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TestFolder))
                AssetDatabase.DeleteAsset(TestFolder);
        }

        [Test]
        public void GetImportSettings_Texture_ReturnsImporterType()
        {
            var result = AssetImportTools.GetImportSettings(_texturePath);
            Assert.IsFalse(result.IsError, $"Expected success: {result.Text}");
            StringAssert.Contains("TextureImporter", result.Text);
        }

        [Test]
        public void GetImportSettings_NotFound_ReturnsError()
        {
            var result = AssetImportTools.GetImportSettings("Assets/NonExistent.png");
            Assert.IsTrue(result.IsError);
            StringAssert.Contains("no importer", result.Text.ToLower());
        }

        [Test]
        public void GetImportSettings_Folder_ReturnsImporter()
        {
            var result = AssetImportTools.GetImportSettings("Assets/UniClaudeTestTemp");
            Assert.IsFalse(result.IsError, $"Folders have importers in Unity: {result.Text}");
        }

        [Test]
        public void SetImportSettings_Texture_ChangesReadable()
        {
            var result = AssetImportTools.SetImportSettings(_texturePath, "{\"m_IsReadable\": true}");
            Assert.IsFalse(result.IsError, $"Expected success: {result.Text}");

            var importer = AssetImporter.GetAtPath(_texturePath) as TextureImporter;
            Assert.IsTrue(importer.isReadable);
        }

        [Test]
        public void SetImportSettings_InvalidProperty_ReturnsError()
        {
            var result = AssetImportTools.SetImportSettings(_texturePath, "{\"nonExistentProp\": 42}");
            Assert.IsTrue(result.IsError);
            StringAssert.Contains("not found", result.Text.ToLower());
        }

        [Test]
        public void SetImportSettings_NotFound_ReturnsError()
        {
            var result = AssetImportTools.SetImportSettings("Assets/NonExistent.png", "{\"m_MaxTextureSize\": 256}");
            Assert.IsTrue(result.IsError);
        }

        [Test]
        public void SetClipImportSettings_NotAModel_ReturnsError()
        {
            var result = AssetImportTools.SetClipImportSettings(
                _texturePath,
                "[{\"name\": \"clip\", \"loopTime\": true}]");
            Assert.IsTrue(result.IsError);
            StringAssert.Contains("ModelImporter", result.Text);
        }

        [Test]
        public void SetClipImportSettings_NotFound_ReturnsError()
        {
            var result = AssetImportTools.SetClipImportSettings(
                "Assets/NonExistent.fbx",
                "[{\"name\": \"clip\", \"loopTime\": true}]");
            Assert.IsTrue(result.IsError);
        }

        [Test]
        public void SetClipImportSettings_InvalidJson_ReturnsError()
        {
            var result = AssetImportTools.SetClipImportSettings(
                _texturePath,
                "not valid json");
            Assert.IsTrue(result.IsError);
            StringAssert.Contains("Invalid JSON", result.Text);
        }
    }
}
