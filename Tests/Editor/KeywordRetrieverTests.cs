using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UniClaude.Editor;

namespace UniClaude.Editor.Tests
{
    [TestFixture]
    public class KeywordRetrieverTests
    {
        KeywordRetriever _retriever;
        ProjectIndex _index;
        RetrievalSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _retriever = new KeywordRetriever();
            _settings = new RetrievalSettings { MaxFiles = 10, MaxTokens = 4096 };
            _index = new ProjectIndex
            {
                Entries = new List<IndexEntry>
                {
                    new IndexEntry
                    {
                        AssetPath = "Assets/Scripts/HealthSystem.cs",
                        Kind = AssetKind.Script,
                        Name = "HealthSystem",
                        Symbols = new[] { "HealthSystem", "TakeDamage", "Heal", "maxHealth" },
                        Dependencies = new[] { "Assets/Scripts/IDamageable.cs" },
                        Summary = "HealthSystem : MonoBehaviour (IDamageable)\n  Fields: maxHealth\n  Methods: TakeDamage, Heal"
                    },
                    new IndexEntry
                    {
                        AssetPath = "Assets/Scripts/IDamageable.cs",
                        Kind = AssetKind.Script,
                        Name = "IDamageable",
                        Symbols = new[] { "IDamageable", "TakeDamage" },
                        Dependencies = new string[0],
                        Summary = "interface IDamageable\n  Methods: TakeDamage"
                    },
                    new IndexEntry
                    {
                        AssetPath = "Assets/Scripts/PlayerController.cs",
                        Kind = AssetKind.Script,
                        Name = "PlayerController",
                        Symbols = new[] { "PlayerController", "Move", "Jump", "speed" },
                        Dependencies = new string[0],
                        Summary = "PlayerController : MonoBehaviour\n  Methods: Move, Jump"
                    },
                    new IndexEntry
                    {
                        AssetPath = "Assets/Scripts/DamageEvent.cs",
                        Kind = AssetKind.Script,
                        Name = "DamageEvent",
                        Symbols = new[] { "DamageEvent", "Amount", "Type" },
                        Dependencies = new string[0],
                        Summary = "DamageEvent : ScriptableObject\n  Fields: Amount, Type"
                    }
                }
            };
        }

        [Test]
        public void Retrieve_ExactNameMatch_ReturnsEntry()
        {
            var result = _retriever.Retrieve("What does HealthSystem do?", _index, _settings);

            Assert.IsTrue(result.Entries.Any(e => e.Name == "HealthSystem"));
        }

        [Test]
        public void Retrieve_MethodNameMatch_ReturnsEntry()
        {
            var result = _retriever.Retrieve("How does TakeDamage work?", _index, _settings);

            Assert.IsTrue(result.Entries.Any(e => e.Name == "HealthSystem"));
        }

        [Test]
        public void Retrieve_WalksDependencies_IncludesIDamageable()
        {
            var result = _retriever.Retrieve("Tell me about HealthSystem", _index, _settings);

            Assert.IsTrue(result.Entries.Any(e => e.Name == "HealthSystem"));
            Assert.IsTrue(result.Entries.Any(e => e.Name == "IDamageable"),
                "Should include IDamageable as a dependency of HealthSystem");
        }

        [Test]
        public void Retrieve_NoMatch_ReturnsEmpty()
        {
            var result = _retriever.Retrieve("What is the weather like?", _index, _settings);

            Assert.AreEqual(0, result.Entries.Count);
        }

        [Test]
        public void Retrieve_RespectsMaxFiles()
        {
            _settings.MaxFiles = 1;
            var result = _retriever.Retrieve("TakeDamage HealthSystem", _index, _settings);

            Assert.LessOrEqual(result.Entries.Count, 1);
        }

        [Test]
        public void Retrieve_RespectsMaxTokens()
        {
            _settings.MaxTokens = 10; // Very small — should limit results
            var result = _retriever.Retrieve("HealthSystem", _index, _settings);

            Assert.LessOrEqual(result.EstimatedTokens, 10 + 50); // Allow some tolerance for single entry
        }

        [Test]
        public void Retrieve_CaseInsensitive()
        {
            var result = _retriever.Retrieve("healthsystem", _index, _settings);

            Assert.IsTrue(result.Entries.Any(e => e.Name == "HealthSystem"));
        }

        [Test]
        public void Retrieve_EmptyQuery_ReturnsEmpty()
        {
            var result = _retriever.Retrieve("", _index, _settings);

            Assert.AreEqual(0, result.Entries.Count);
        }

        [Test]
        public void Retrieve_NullQuery_ReturnsEmpty()
        {
            var result = _retriever.Retrieve(null, _index, _settings);

            Assert.AreEqual(0, result.Entries.Count);
        }

        [Test]
        public void Retrieve_SetsEstimatedTokens()
        {
            var result = _retriever.Retrieve("HealthSystem", _index, _settings);

            Assert.Greater(result.EstimatedTokens, 0);
        }
    }
}
