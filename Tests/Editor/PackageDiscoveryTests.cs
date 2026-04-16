using System.IO;
using System.Linq;
using NUnit.Framework;

namespace UniClaude.Editor.Tests
{
    [TestFixture]
    public class PackageDiscoveryTests
    {
        string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "PackageDiscoveryTest_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        [Test]
        public void Resolve_WithLockFile_ReturnsAllPackages()
        {
            Directory.CreateDirectory(Path.Combine(_tempDir, "Packages"));
            File.WriteAllText(Path.Combine(_tempDir, "Packages", "packages-lock.json"), @"{
  ""dependencies"": {
    ""com.arcforge.ui"": {
      ""version"": ""0.1.0"",
      ""depth"": 0,
      ""source"": ""embedded"",
      ""dependencies"": {}
    },
    ""com.unity.ugui"": {
      ""version"": ""2.0.0"",
      ""depth"": 0,
      ""source"": ""registry"",
      ""dependencies"": {}
    }
  }
}");
            var localPkgDir = Path.Combine(_tempDir, "Packages", "com.arcforge.ui");
            Directory.CreateDirectory(localPkgDir);
            File.WriteAllText(Path.Combine(localPkgDir, "package.json"), @"{
  ""name"": ""com.arcforge.ui"",
  ""version"": ""0.1.0"",
  ""displayName"": ""ArcForge UI""
}");

            var packages = PackageDiscovery.Resolve(_tempDir);

            Assert.AreEqual(2, packages.Count);

            var local = packages.First(p => p.Name == "com.arcforge.ui");
            Assert.IsTrue(local.IsLocal);
            Assert.AreEqual("0.1.0", local.Version);
            Assert.AreEqual("ArcForge UI", local.DisplayName);
            Assert.IsTrue(Directory.Exists(local.ResolvedPath));

            var registry = packages.First(p => p.Name == "com.unity.ugui");
            Assert.IsFalse(registry.IsLocal);
            Assert.AreEqual("2.0.0", registry.Version);
        }

        [Test]
        public void Resolve_EmbeddedPackage_ResolvesPathToPackagesDir()
        {
            Directory.CreateDirectory(Path.Combine(_tempDir, "Packages"));
            File.WriteAllText(Path.Combine(_tempDir, "Packages", "packages-lock.json"), @"{
  ""dependencies"": {
    ""com.test.pkg"": {
      ""version"": ""1.0.0"",
      ""depth"": 0,
      ""source"": ""embedded"",
      ""dependencies"": {}
    }
  }
}");
            var pkgDir = Path.Combine(_tempDir, "Packages", "com.test.pkg");
            Directory.CreateDirectory(pkgDir);
            File.WriteAllText(Path.Combine(pkgDir, "package.json"), @"{
  ""name"": ""com.test.pkg"",
  ""version"": ""1.0.0"",
  ""displayName"": ""Test Package""
}");

            var packages = PackageDiscovery.Resolve(_tempDir);

            Assert.AreEqual(1, packages.Count);
            Assert.AreEqual(pkgDir, packages[0].ResolvedPath);
        }

        [Test]
        public void Resolve_RegistryPackage_ResolvesPathToLibraryCache()
        {
            Directory.CreateDirectory(Path.Combine(_tempDir, "Packages"));
            File.WriteAllText(Path.Combine(_tempDir, "Packages", "packages-lock.json"), @"{
  ""dependencies"": {
    ""com.unity.ugui"": {
      ""version"": ""2.0.0"",
      ""depth"": 0,
      ""source"": ""registry"",
      ""hash"": ""abc123"",
      ""dependencies"": {}
    }
  }
}");
            var cacheDir = Path.Combine(_tempDir, "Library", "PackageCache", "com.unity.ugui@2.0.0");
            Directory.CreateDirectory(cacheDir);

            var packages = PackageDiscovery.Resolve(_tempDir);

            Assert.AreEqual(1, packages.Count);
            Assert.AreEqual(cacheDir, packages[0].ResolvedPath);
        }

        [Test]
        public void Resolve_RegistryPackageCacheMissing_SetsNullResolvedPath()
        {
            Directory.CreateDirectory(Path.Combine(_tempDir, "Packages"));
            File.WriteAllText(Path.Combine(_tempDir, "Packages", "packages-lock.json"), @"{
  ""dependencies"": {
    ""com.unity.ugui"": {
      ""version"": ""2.0.0"",
      ""depth"": 0,
      ""source"": ""registry"",
      ""dependencies"": {}
    }
  }
}");

            var packages = PackageDiscovery.Resolve(_tempDir);

            Assert.AreEqual(1, packages.Count);
            Assert.IsNull(packages[0].ResolvedPath);
        }

        [Test]
        public void Resolve_NoLockFile_FallsBackToLocalPackageScan()
        {
            var pkgDir = Path.Combine(_tempDir, "Packages", "com.test.local");
            Directory.CreateDirectory(pkgDir);
            File.WriteAllText(Path.Combine(pkgDir, "package.json"), @"{
  ""name"": ""com.test.local"",
  ""version"": ""1.0.0"",
  ""displayName"": ""Test Local""
}");

            var packages = PackageDiscovery.Resolve(_tempDir);

            Assert.AreEqual(1, packages.Count);
            Assert.AreEqual("com.test.local", packages[0].Name);
            Assert.IsTrue(packages[0].IsLocal);
            Assert.AreEqual(pkgDir, packages[0].ResolvedPath);
        }

        [Test]
        public void Resolve_MalformedLockFile_FallsBackToLocalPackageScan()
        {
            Directory.CreateDirectory(Path.Combine(_tempDir, "Packages"));
            File.WriteAllText(Path.Combine(_tempDir, "Packages", "packages-lock.json"), "NOT JSON {{{");

            var pkgDir = Path.Combine(_tempDir, "Packages", "com.test.local");
            Directory.CreateDirectory(pkgDir);
            File.WriteAllText(Path.Combine(pkgDir, "package.json"), @"{
  ""name"": ""com.test.local"",
  ""version"": ""1.0.0"",
  ""displayName"": ""Test Local""
}");

            var packages = PackageDiscovery.Resolve(_tempDir);

            Assert.AreEqual(1, packages.Count);
            Assert.IsTrue(packages[0].IsLocal);
        }

        [Test]
        public void Resolve_NoPackagesDir_ReturnsEmpty()
        {
            var packages = PackageDiscovery.Resolve(_tempDir);
            Assert.AreEqual(0, packages.Count);
        }

        [Test]
        public void Resolve_LocalSource_TreatedAsLocal()
        {
            Directory.CreateDirectory(Path.Combine(_tempDir, "Packages"));
            File.WriteAllText(Path.Combine(_tempDir, "Packages", "packages-lock.json"), @"{
  ""dependencies"": {
    ""com.local.ref"": {
      ""version"": ""file:../some-path"",
      ""depth"": 0,
      ""source"": ""local"",
      ""dependencies"": {}
    }
  }
}");
            var pkgDir = Path.Combine(_tempDir, "Packages", "com.local.ref");
            Directory.CreateDirectory(pkgDir);
            File.WriteAllText(Path.Combine(pkgDir, "package.json"), @"{
  ""name"": ""com.local.ref"",
  ""version"": ""1.0.0"",
  ""displayName"": ""Local Ref""
}");

            var packages = PackageDiscovery.Resolve(_tempDir);

            Assert.AreEqual(1, packages.Count);
            Assert.IsTrue(packages[0].IsLocal);
        }
    }
}
