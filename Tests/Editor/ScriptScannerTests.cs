using System.IO;
using System.Linq;
using NUnit.Framework;
using UniClaude.Editor;

namespace UniClaude.Editor.Tests
{
    [TestFixture]
    public class ScriptScannerTests
    {
        ScriptScanner _scanner;
        string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _scanner = new ScriptScanner();
            _tempDir = Path.Combine(Path.GetTempPath(), "ScriptScannerTest_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        string WriteScript(string filename, string content)
        {
            var path = Path.Combine(_tempDir, filename);
            File.WriteAllText(path, content);
            return path;
        }

        [Test]
        public void CanScan_CsFile_ReturnsTrue()
        {
            Assert.IsTrue(_scanner.CanScan("Assets/Scripts/Player.cs"));
        }

        [Test]
        public void CanScan_NonCsFile_ReturnsFalse()
        {
            Assert.IsFalse(_scanner.CanScan("Assets/Scenes/Main.unity"));
            Assert.IsFalse(_scanner.CanScan("Assets/Textures/icon.png"));
        }

        [Test]
        public void Scan_SimpleMonoBehaviour_ExtractsClassAndBase()
        {
            var path = WriteScript("Player.cs", @"
using UnityEngine;

public class Player : MonoBehaviour
{
    public float speed = 5f;
    void Update() { }
}
");
            var entry = _scanner.Scan(path);

            Assert.IsNotNull(entry);
            Assert.AreEqual(AssetKind.Script, entry.Kind);
            Assert.AreEqual("Player", entry.Name);
            Assert.IsTrue(entry.Symbols.Contains("Player"));
            Assert.IsTrue(entry.Summary.Contains("MonoBehaviour"));
        }

        [Test]
        public void Scan_Interface_ExtractsInterfaceName()
        {
            var path = WriteScript("IDamageable.cs", @"
public interface IDamageable
{
    void TakeDamage(float amount);
    float Health { get; }
}
");
            var entry = _scanner.Scan(path);

            Assert.IsNotNull(entry);
            Assert.AreEqual("IDamageable", entry.Name);
            Assert.IsTrue(entry.Symbols.Contains("IDamageable"));
            Assert.IsTrue(entry.Symbols.Contains("TakeDamage"));
        }

        [Test]
        public void Scan_SerializedFields_Detected()
        {
            var path = WriteScript("Health.cs", @"
using UnityEngine;

public class HealthSystem : MonoBehaviour
{
    [SerializeField] float maxHealth = 100f;
    [SerializeField] private int lives;
    public string playerName;
}
");
            var entry = _scanner.Scan(path);

            Assert.IsNotNull(entry);
            Assert.IsTrue(entry.Symbols.Contains("maxHealth"));
            Assert.IsTrue(entry.Symbols.Contains("lives"));
            Assert.IsTrue(entry.Symbols.Contains("playerName"));
            Assert.IsTrue(entry.Summary.Contains("maxHealth"));
        }

        [Test]
        public void Scan_WithBaseAndInterfaces_ExtractsDependencies()
        {
            var path = WriteScript("Warrior.cs", @"
public class Warrior : Character, IDamageable, ISerializable
{
    public void Attack() { }
}
");
            var entry = _scanner.Scan(path);

            Assert.IsNotNull(entry);
            Assert.IsTrue(entry.Summary.Contains("Character"));
            Assert.IsTrue(entry.Summary.Contains("IDamageable"));
        }

        [Test]
        public void Scan_PublicMethods_Extracted()
        {
            var path = WriteScript("Inventory.cs", @"
public class Inventory
{
    public void AddItem(Item item) { }
    public bool RemoveItem(string id) { return false; }
    private void SortInternal() { }
    public int Count { get; }
}
");
            var entry = _scanner.Scan(path);

            Assert.IsNotNull(entry);
            Assert.IsTrue(entry.Symbols.Contains("AddItem"));
            Assert.IsTrue(entry.Symbols.Contains("RemoveItem"));
            Assert.IsTrue(entry.Symbols.Contains("Count"));
            Assert.IsFalse(entry.Symbols.Contains("SortInternal"));
        }

        [Test]
        public void Scan_EmptyFile_ReturnsNull()
        {
            var path = WriteScript("Empty.cs", "");
            var entry = _scanner.Scan(path);

            Assert.IsNull(entry);
        }

        [Test]
        public void Scan_NoClassOrInterface_ReturnsNull()
        {
            var path = WriteScript("Usings.cs", @"
using System;
using UnityEngine;
// Just usings, no type declarations
");
            var entry = _scanner.Scan(path);

            Assert.IsNull(entry);
        }

        [Test]
        public void Scan_Enum_ExtractsName()
        {
            var path = WriteScript("DamageType.cs", @"
public enum DamageType
{
    Physical,
    Magic,
    Fire,
    Ice
}
");
            var entry = _scanner.Scan(path);

            Assert.IsNotNull(entry);
            Assert.AreEqual("DamageType", entry.Name);
            Assert.IsTrue(entry.Symbols.Contains("DamageType"));
        }

        [Test]
        public void Scan_Struct_ExtractsName()
        {
            var path = WriteScript("DamageEvent.cs", @"
public struct DamageEvent
{
    public float Amount;
    public DamageType Type;
}
");
            var entry = _scanner.Scan(path);

            Assert.IsNotNull(entry);
            Assert.AreEqual("DamageEvent", entry.Name);
            Assert.IsTrue(entry.Symbols.Contains("DamageEvent"));
            Assert.IsTrue(entry.Symbols.Contains("Amount"));
        }

        [Test]
        public void Scan_SetsLastModifiedTicks()
        {
            var path = WriteScript("Timestamped.cs", @"
public class Timestamped { }
");
            var entry = _scanner.Scan(path);

            Assert.IsNotNull(entry);
            Assert.Greater(entry.LastModifiedTicks, 0);
        }

        [Test]
        public void Scan_PartialClass_ExtractsName()
        {
            var path = WriteScript("PlayerMovement.cs", @"
public partial class PlayerController
{
    public void Move(Vector3 dir) { }
    public float speed = 5f;
}
");
            var entry = _scanner.Scan(path);

            Assert.IsNotNull(entry);
            Assert.AreEqual("PlayerController", entry.Name);
            Assert.IsTrue(entry.Symbols.Contains("Move"));
        }

        [Test]
        public void Scan_NestedType_ExtractsOuterName()
        {
            var path = WriteScript("InventoryNested.cs", @"
public class Inventory
{
    public void AddItem(Item item) { }

    public class SlotData
    {
        public int Index;
        public Item Item;
    }
}
");
            var entry = _scanner.Scan(path);

            Assert.IsNotNull(entry);
            Assert.AreEqual("Inventory", entry.Name);
            Assert.IsTrue(entry.Symbols.Contains("AddItem"));
        }

        [Test]
        public void Kind_IsScript()
        {
            Assert.AreEqual(AssetKind.Script, _scanner.Kind);
        }
    }
}
