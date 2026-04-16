using System.Collections.Generic;
using NUnit.Framework;
using UniClaude.Editor;

namespace UniClaude.Editor.Tests
{
    [TestFixture]
    public class ContextFormatterTests
    {
        ProjectIndex _index;

        [SetUp]
        public void SetUp()
        {
            _index = new ProjectIndex
            {
                ProjectName = "TestGame",
                UnityVersion = "6000.3",
                ProjectTreeSummary = "Assets/Scripts/\n  Player.cs\n  Enemy.cs",
                Entries = new List<IndexEntry>
                {
                    new IndexEntry
                    {
                        AssetPath = "Assets/Scripts/Player.cs",
                        Kind = AssetKind.Script,
                        Name = "Player",
                        Summary = "Player : MonoBehaviour\n  Methods: Move, Jump"
                    },
                    new IndexEntry
                    {
                        AssetPath = "Assets/Scripts/Enemy.cs",
                        Kind = AssetKind.Script,
                        Name = "Enemy",
                        Summary = "Enemy : MonoBehaviour\n  Methods: Attack"
                    }
                }
            };
        }

        [Test]
        public void Format_NoMatches_ReturnsTier1Only()
        {
            var result = new RetrievalResult();

            var context = ContextFormatter.Format(_index, result);

            Assert.IsNotNull(context);
            Assert.IsTrue(context.FormattedPrompt.Contains("Project Context"));
            Assert.IsTrue(context.FormattedPrompt.Contains("TestGame"));
            Assert.IsFalse(context.FormattedPrompt.Contains("Relevant Files"));
        }

        [Test]
        public void Format_WithMatches_IncludesTier2()
        {
            var result = new RetrievalResult
            {
                Entries = new List<IndexEntry> { _index.Entries[0] },
                EstimatedTokens = 20
            };

            var context = ContextFormatter.Format(_index, result);

            Assert.IsTrue(context.FormattedPrompt.Contains("Relevant Files"));
            Assert.IsTrue(context.FormattedPrompt.Contains("Player"));
        }

        [Test]
        public void Format_ContextBlock_HasCorrectSummary()
        {
            var result = new RetrievalResult
            {
                Entries = new List<IndexEntry> { _index.Entries[0], _index.Entries[1] },
                EstimatedTokens = 40
            };

            var context = ContextFormatter.Format(_index, result);

            Assert.IsNotNull(context.Block);
            Assert.AreEqual(2, context.Block.FileNames.Count);
            Assert.IsTrue(context.Block.FileNames.Contains("Player.cs"));
            Assert.IsTrue(context.Block.FileNames.Contains("Enemy.cs"));
            Assert.Greater(context.Block.TokenCount, 0);
        }

        [Test]
        public void Format_ContextBlock_FullTextContainsPrompt()
        {
            var result = new RetrievalResult
            {
                Entries = new List<IndexEntry> { _index.Entries[0] },
                EstimatedTokens = 20
            };

            var context = ContextFormatter.Format(_index, result);

            Assert.AreEqual(context.FormattedPrompt, context.Block.FullText);
        }

        [Test]
        public void Format_NullIndex_ReturnsNull()
        {
            var context = ContextFormatter.Format(null, new RetrievalResult());
            Assert.IsNull(context);
        }

        [Test]
        public void Format_IncludesProjectStats()
        {
            var context = ContextFormatter.Format(_index, new RetrievalResult());

            Assert.IsTrue(context.FormattedPrompt.Contains("Scripts: 2"));
        }

        [Test]
        public void FormatTier1_ReturnsProjectSummaryOnly()
        {
            var result = ContextFormatter.FormatTier1(_index);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("Project Context"));
            Assert.IsTrue(result.Contains("TestGame"));
            Assert.IsTrue(result.Contains("Unity 6000.3"));
            Assert.IsTrue(result.Contains("Scripts: 2"));
            Assert.IsTrue(result.Contains("Player.cs"));
            Assert.IsFalse(result.Contains("Relevant Files"));
        }

        [Test]
        public void FormatTier1_NullIndex_ReturnsNull()
        {
            var result = ContextFormatter.FormatTier1(null);
            Assert.IsNull(result);
        }

        [Test]
        public void FormatResults_WithEntries_ReturnsRelevantFiles()
        {
            var result = new RetrievalResult
            {
                Entries = new List<IndexEntry> { _index.Entries[0] },
                EstimatedTokens = 20
            };

            var formatted = ContextFormatter.FormatResults(result);

            Assert.IsNotNull(formatted);
            Assert.IsTrue(formatted.Contains("Relevant Files"));
            Assert.IsTrue(formatted.Contains("Player.cs"));
            Assert.IsTrue(formatted.Contains("Player : MonoBehaviour"));
            Assert.IsFalse(formatted.Contains("Project Context"));
        }

        [Test]
        public void FormatResults_EmptyResult_ReturnsNoMatchesMessage()
        {
            var result = new RetrievalResult();

            var formatted = ContextFormatter.FormatResults(result);

            Assert.IsNotNull(formatted);
            Assert.IsTrue(formatted.Contains("No matching"));
        }

        [Test]
        public void FormatResults_NullResult_ReturnsNoMatchesMessage()
        {
            var formatted = ContextFormatter.FormatResults(null);

            Assert.IsNotNull(formatted);
            Assert.IsTrue(formatted.Contains("No matching"));
        }

        [Test]
        public void FormatTier1_WithPackages_IncludesPackagesLine()
        {
            var index = new ProjectIndex
            {
                ProjectName = "TestGame",
                UnityVersion = "6000.3",
                Entries = new List<IndexEntry>(),
                IndexedPackages = new List<PackageInfo>
                {
                    new PackageInfo { Name = "com.arcforge.ui", Version = "0.1.0" },
                    new PackageInfo { Name = "com.test.pkg", Version = "2.0.0" }
                }
            };

            var result = ContextFormatter.FormatTier1(index);

            Assert.IsTrue(result.Contains("Packages: com.arcforge.ui (0.1.0), com.test.pkg (2.0.0)"));
        }

        [Test]
        public void FormatTier1_NoPackages_OmitsPackagesLine()
        {
            var index = new ProjectIndex
            {
                ProjectName = "TestGame",
                UnityVersion = "6000.3",
                Entries = new List<IndexEntry>(),
                IndexedPackages = new List<PackageInfo>()
            };

            var result = ContextFormatter.FormatTier1(index);

            Assert.IsFalse(result.Contains("Packages:"));
        }

        [Test]
        public void FormatTier1_NullPackages_OmitsPackagesLine()
        {
            var index = new ProjectIndex
            {
                ProjectName = "TestGame",
                UnityVersion = "6000.3",
                Entries = new List<IndexEntry>(),
                IndexedPackages = null
            };

            var result = ContextFormatter.FormatTier1(index);

            Assert.IsFalse(result.Contains("Packages:"));
        }

        [Test]
        public void Format_WithPackages_IncludesPackagesLine()
        {
            var index = new ProjectIndex
            {
                ProjectName = "TestGame",
                UnityVersion = "6000.3",
                Entries = new List<IndexEntry>(),
                IndexedPackages = new List<PackageInfo>
                {
                    new PackageInfo { Name = "com.arcforge.ui", Version = "0.1.0" }
                }
            };
            var result = ContextFormatter.Format(index, new RetrievalResult());

            Assert.IsTrue(result.FormattedPrompt.Contains("Packages: com.arcforge.ui (0.1.0)"));
        }
    }
}
