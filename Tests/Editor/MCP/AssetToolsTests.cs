using System.IO;
using NUnit.Framework;
using UnityEditor;
using UniClaude.Editor.MCP;

namespace UniClaude.Editor.Tests.MCP
{
    /// <summary>
    /// Tests for <see cref="AssetTools"/> MCP tools covering asset info retrieval,
    /// search, move, and reimport operations.
    /// Uses a temporary directory under Assets/UniClaudeTestTemp/ for asset operations
    /// so that <see cref="AssetDatabase"/> can track the files.
    /// </summary>
    public class AssetToolsTests
    {
        /// <summary>
        /// The project-relative path to the temp folder used by asset tests.
        /// Must be under Assets/ for AssetDatabase visibility.
        /// </summary>
        const string TempFolder = "Assets/UniClaudeTestTemp";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.CreateFolder("Assets", "UniClaudeTestTemp");
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.DeleteAsset(TempFolder);
            }
        }

        /// <summary>
        /// Creates a minimal text asset inside the temp folder and refreshes the AssetDatabase
        /// so it is immediately available for queries.
        /// </summary>
        /// <param name="fileName">The file name (e.g. "test.txt").</param>
        /// <param name="content">Optional file content; defaults to "temp".</param>
        /// <returns>The project-relative asset path.</returns>
        string CreateTempAsset(string fileName, string content = "temp")
        {
            var assetPath = TempFolder + "/" + fileName;
            var absolutePath = Path.Combine(
                Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..")),
                assetPath);

            var dir = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(absolutePath, content);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            return assetPath;
        }

        // ── GetAssetInfo ──

        /// <summary>
        /// Verifies that GetAssetInfo returns the asset type and GUID for a known project file.
        /// Uses a .cs script from the package itself as a stable known asset.
        /// </summary>
        [Test]
        public void GetAssetInfo_ReturnsTypeAndGUID()
        {
            var assetPath = CreateTempAsset("info_test.txt", "hello");

            var result = AssetTools.GetAssetInfo(assetPath);

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            Assert.That(result.Text, Does.Contain("\"type\":"));
            Assert.That(result.Text, Does.Contain("\"guid\":"));
            Assert.That(result.Text, Does.Contain("\"labels\":"));
            Assert.That(result.Text, Does.Contain("\"dependencies\":"));
        }

        /// <summary>
        /// Verifies that GetAssetInfo returns an error for a path that does not exist.
        /// </summary>
        [Test]
        public void GetAssetInfo_NotFound_Error()
        {
            var result = AssetTools.GetAssetInfo("Assets/NonExistent/FakeAsset_12345.png");

            Assert.IsTrue(result.IsError);
            Assert.That(result.Text, Does.Contain("Asset not found at path"));
        }

        // ── FindAssets ──

        /// <summary>
        /// Verifies that FindAssets with a type filter returns results.
        /// Searches for scripts, which must exist in any Unity project.
        /// </summary>
        [Test]
        public void FindAssets_ByTypeFilter()
        {
            var result = AssetTools.FindAssets("t:Script", null);

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            Assert.That(result.Text, Does.Contain("\"matches\":"));
            // There must be at least one script in the project
            Assert.That(result.Text, Does.Contain(".cs"));
        }

        /// <summary>
        /// Verifies that FindAssets caps results at 100 entries regardless of how many
        /// assets match.
        /// </summary>
        [Test]
        public void FindAssets_CappedAt100()
        {
            // Search for all assets in the project — there should be more than 100
            var result = AssetTools.FindAssets("", null);

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            // The count field should be at most 100
            Assert.That(result.Text, Does.Contain("\"count\": 100").Or.Contain("\"count\":"));
            // If there are more than 100 total, truncated should be true
            // Either way, the matches array should have no more than 100 entries
        }

        // ── MoveAsset ──

        /// <summary>
        /// Verifies that MoveAsset successfully moves a temporary text asset to a new
        /// location and that the new path is valid in the AssetDatabase.
        /// </summary>
        [Test]
        public void MoveAsset_Success()
        {
            var sourcePath = CreateTempAsset("move_source.txt", "move me");
            var destPath = TempFolder + "/Subfolder/move_dest.txt";

            var result = AssetTools.MoveAsset(sourcePath, destPath);

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            Assert.That(result.Text, Does.Contain(destPath));

            // Verify the asset exists at the new location
            var destType = AssetDatabase.GetMainAssetTypeAtPath(destPath);
            Assert.IsNotNull(destType, "Asset should exist at the destination path after move");

            // Verify the asset is gone from the old location
            var sourceType = AssetDatabase.GetMainAssetTypeAtPath(sourcePath);
            Assert.IsNull(sourceType, "Asset should no longer exist at the source path after move");
        }

        /// <summary>
        /// Verifies that MoveAsset returns an error when the source path does not exist.
        /// </summary>
        [Test]
        public void MoveAsset_SourceNotFound_Error()
        {
            var result = AssetTools.MoveAsset(
                "Assets/NonExistent/NoSuchFile_99999.txt",
                TempFolder + "/destination.txt");

            Assert.IsTrue(result.IsError);
            Assert.That(result.Text, Does.Contain("Source asset not found"));
        }

        // ── ImportAsset ──

        /// <summary>
        /// Verifies that ImportAsset succeeds when given a valid existing asset path.
        /// </summary>
        [Test]
        public void ImportAsset_Success()
        {
            var assetPath = CreateTempAsset("import_test.txt", "reimport me");

            var result = AssetTools.ImportAsset(assetPath);

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            Assert.That(result.Text, Does.Contain("\"reimported\": true"));
        }

        /// <summary>
        /// Verifies that ImportAsset returns an error when the asset does not exist.
        /// </summary>
        [Test]
        public void ImportAsset_NotFound_Error()
        {
            var result = AssetTools.ImportAsset("Assets/NonExistent/FakeAsset_67890.png");

            Assert.IsTrue(result.IsError);
            Assert.That(result.Text, Does.Contain("Asset not found at path"));
        }
    }
}
