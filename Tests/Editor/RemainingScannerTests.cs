using System.IO;
using System.Linq;
using NUnit.Framework;
using UniClaude.Editor;

namespace UniClaude.Editor.Tests
{
    [TestFixture]
    public class RemainingScannerTests
    {
        string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "RemainingScannerTest_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        string WriteFile(string filename, string content)
        {
            var dir = Path.GetDirectoryName(Path.Combine(_tempDir, filename));
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var path = Path.Combine(_tempDir, filename);
            File.WriteAllText(path, content);
            return path;
        }

        // --- ScriptableObjectScanner ---

        [Test]
        public void SO_CanScan_AssetFile_ReturnsTrue()
        {
            var scanner = new ScriptableObjectScanner();
            Assert.IsTrue(scanner.CanScan("Assets/Data/EnemyConfig.asset"));
        }

        [Test]
        public void SO_CanScan_ProjectSettingsFile_ReturnsFalse()
        {
            var scanner = new ScriptableObjectScanner();
            Assert.IsFalse(scanner.CanScan("ProjectSettings/TagManager.asset"));
        }

        [Test]
        public void SO_Scan_ExtractsFieldsAndGuid()
        {
            var scanner = new ScriptableObjectScanner();
            var path = WriteFile("EnemyConfig.asset", @"%YAML 1.1
--- !u!114 &11400000
MonoBehaviour:
  m_Script: {fileID: 11500000, guid: abc123, type: 3}
  m_Name: EnemyConfig
  health: 100
  speed: 5.5
  attackType: Melee
");
            var entry = scanner.Scan(path);

            Assert.IsNotNull(entry);
            Assert.AreEqual(AssetKind.ScriptableObject, entry.Kind);
            Assert.AreEqual("EnemyConfig", entry.Name);
            Assert.IsTrue(entry.Symbols.Contains("health"));
            Assert.IsTrue(entry.Symbols.Contains("speed"));
            Assert.IsTrue(entry.Dependencies.Contains("abc123"));
        }

        [Test]
        public void SO_Scan_EmptyFile_ReturnsNull()
        {
            var scanner = new ScriptableObjectScanner();
            var path = WriteFile("Empty.asset", "");
            Assert.IsNull(scanner.Scan(path));
        }

        [Test]
        public void SO_Scan_NonYamlAsset_ReturnsNull()
        {
            var scanner = new ScriptableObjectScanner();
            var path = WriteFile("Binary.asset", "NOT YAML CONTENT");
            Assert.IsNull(scanner.Scan(path));
        }

        // --- ProjectSettingsScanner ---

        [Test]
        public void PS_CanScan_ProjectSettingsFile_ReturnsTrue()
        {
            var scanner = new ProjectSettingsScanner();
            Assert.IsTrue(scanner.CanScan("ProjectSettings/TagManager.asset"));
        }

        [Test]
        public void PS_CanScan_RegularAssetFile_ReturnsFalse()
        {
            var scanner = new ProjectSettingsScanner();
            Assert.IsFalse(scanner.CanScan("Assets/Data/Config.asset"));
        }

        [Test]
        public void PS_Scan_ExtractsFields()
        {
            var scanner = new ProjectSettingsScanner();
            var path = WriteFile("ProjectSettings/TagManager.asset", @"%YAML 1.1
--- !u!78 &1
TagManager:
  m_CompanyName: MyStudio
  m_ProductName: MyGame
  scriptingBackend: 1
");
            var entry = scanner.Scan(path);

            Assert.IsNotNull(entry);
            Assert.AreEqual(AssetKind.ProjectSettings, entry.Kind);
            Assert.AreEqual("TagManager", entry.Name);
            Assert.IsTrue(entry.Summary.Contains("m_CompanyName") || entry.Summary.Contains("m_ProductName"));
        }

        [Test]
        public void PS_Scan_EmptyFile_ReturnsNull()
        {
            var scanner = new ProjectSettingsScanner();
            var path = WriteFile("ProjectSettings/Empty.asset", "");
            Assert.IsNull(scanner.Scan(path));
        }

        // --- ShaderScanner ---

        [Test]
        public void Shader_CanScan_ShaderFile_ReturnsTrue()
        {
            var scanner = new ShaderScanner();
            Assert.IsTrue(scanner.CanScan("Assets/Shaders/Toon.shader"));
        }

        [Test]
        public void Shader_CanScan_NonShaderFile_ReturnsFalse()
        {
            var scanner = new ShaderScanner();
            Assert.IsFalse(scanner.CanScan("Assets/Scripts/Player.cs"));
        }

        [Test]
        public void Shader_Scan_ExtractsNameAndProperties()
        {
            var scanner = new ShaderScanner();
            var path = WriteFile("Toon.shader", @"Shader ""Custom/Toon""
{
    Properties
    {
        _MainTex (""Albedo"", 2D) = ""white"" {}
        _Color (""Tint Color"", Color) = (1,1,1,1)
    }
    SubShader { }
}");
            var entry = scanner.Scan(path);

            Assert.IsNotNull(entry);
            Assert.AreEqual(AssetKind.Shader, entry.Kind);
            Assert.AreEqual("Toon", entry.Name);
            Assert.IsTrue(entry.Symbols.Contains("Custom/Toon"));
            Assert.IsTrue(entry.Symbols.Contains("_MainTex"));
            Assert.IsTrue(entry.Symbols.Contains("_Color"));
        }

        [Test]
        public void Shader_Scan_NoShaderDeclaration_ReturnsNull()
        {
            var scanner = new ShaderScanner();
            var path = WriteFile("Bad.shader", "// just a comment, no Shader declaration");
            Assert.IsNull(scanner.Scan(path));
        }

        [Test]
        public void Shader_Scan_EmptyFile_ReturnsNull()
        {
            var scanner = new ShaderScanner();
            var path = WriteFile("Empty.shader", "");
            Assert.IsNull(scanner.Scan(path));
        }
    }
}
