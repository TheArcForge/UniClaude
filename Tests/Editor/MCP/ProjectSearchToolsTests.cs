using System.IO;
using NUnit.Framework;
using UniClaude.Editor;
using UniClaude.Editor.MCP;

namespace UniClaude.Editor.Tests.MCP
{
    /// <summary>
    /// Tests for <see cref="ProjectSearchTools"/> MCP tool.
    /// Uses a temporary directory with test scripts to exercise index search.
    /// </summary>
    [TestFixture]
    public class ProjectSearchToolsTests
    {
        string _tempDir;
        string _indexDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "ProjectSearchTest_" + Path.GetRandomFileName());
            _indexDir = Path.Combine(_tempDir, "index");
            Directory.CreateDirectory(_tempDir);
            Directory.CreateDirectory(_indexDir);
            ProjectIndexStore.BaseDir = _indexDir;

            // Create test scripts
            var scriptsDir = Path.Combine(_tempDir, "Assets", "Scripts");
            Directory.CreateDirectory(scriptsDir);
            File.WriteAllText(Path.Combine(scriptsDir, "PlayerHealth.cs"),
                "public class PlayerHealth : MonoBehaviour { public float health; public void TakeDamage(float amount) { } }");
            File.WriteAllText(Path.Combine(scriptsDir, "EnemyAI.cs"),
                "public class EnemyAI : MonoBehaviour { public void Attack() { } }");

            var awareness = new ProjectAwareness();
            awareness.Initialize(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            ProjectAwareness.Instance?.Dispose();
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
            ProjectIndexStore.ResetBaseDir();
        }

        [Test]
        public void ProjectSearch_MatchingQuery_ReturnsResults()
        {
            var result = ProjectSearchTools.ProjectSearch("PlayerHealth", 10);

            Assert.IsFalse(result.IsError);
            Assert.IsTrue(result.Text.Contains("PlayerHealth"));
            Assert.IsTrue(result.Text.Contains("Relevant Files"));
        }

        [Test]
        public void ProjectSearch_NoMatch_ReturnsNoMatchesMessage()
        {
            var result = ProjectSearchTools.ProjectSearch("ZzzNonExistentClass999", 10);

            Assert.IsFalse(result.IsError);
            Assert.IsTrue(result.Text.Contains("No matching"));
        }

        [Test]
        public void ProjectSearch_NullQuery_ReturnsError()
        {
            var result = ProjectSearchTools.ProjectSearch(null, 10);

            Assert.IsTrue(result.IsError);
        }

        [Test]
        public void ProjectSearch_EmptyQuery_ReturnsError()
        {
            var result = ProjectSearchTools.ProjectSearch("", 10);

            Assert.IsTrue(result.IsError);
        }

        [Test]
        public void ProjectSearch_RespectsMaxResults()
        {
            var result = ProjectSearchTools.ProjectSearch("MonoBehaviour", 1);

            Assert.IsFalse(result.IsError);
            // Should contain at most 1 file header (# FileName)
            var headerCount = 0;
            foreach (var line in result.Text.Split('\n'))
            {
                if (line.StartsWith("# ")) headerCount++;
            }
            Assert.LessOrEqual(headerCount, 1);
        }

        [Test]
        public void ProjectSearch_NoInstance_ReturnsError()
        {
            ProjectAwareness.Instance.Dispose();

            var result = ProjectSearchTools.ProjectSearch("Player", 10);

            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Text.Contains("not available"));
        }
    }
}
