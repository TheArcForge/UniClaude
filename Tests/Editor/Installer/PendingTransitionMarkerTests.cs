using System.IO;
using NUnit.Framework;
using UniClaude.Editor.Installer;

namespace UniClaude.Editor.Tests.Installer
{
    public class PendingTransitionMarkerTests
    {
        [Test]
        public void RoundTrip_PreservesAllFields()
        {
            var tmpDir = Path.Combine(Path.GetTempPath(), "uc-marker-" + System.Guid.NewGuid());
            Directory.CreateDirectory(tmpDir);
            try
            {
                var path = Path.Combine(tmpDir, "pending-transition.json");
                var marker = new PendingTransitionMarker
                {
                    Kind = "to-standard",
                    UnityPid = 12345,
                    UnityAppPath = "/Applications/Unity.app/Contents/MacOS/Unity",
                    ProjectPath = "/Users/alice/proj",
                    PackagePath = "/Users/alice/proj/Packages/com.arcforge.uniclaude",
                    StatusPath = "/Users/alice/proj/Library/UniClaude/transition-status.json",
                    CreatedAt = "2026-04-22T18:30:00.000Z",
                };
                PendingTransitionMarker.Write(path, marker);

                var back = PendingTransitionMarker.Read(path);
                Assert.AreEqual(marker.Kind, back.Kind);
                Assert.AreEqual(marker.UnityPid, back.UnityPid);
                Assert.AreEqual(marker.UnityAppPath, back.UnityAppPath);
                Assert.AreEqual(marker.ProjectPath, back.ProjectPath);
                Assert.AreEqual(marker.PackagePath, back.PackagePath);
                Assert.AreEqual(marker.StatusPath, back.StatusPath);
                Assert.AreEqual(marker.CreatedAt, back.CreatedAt);
            }
            finally
            {
                Directory.Delete(tmpDir, true);
            }
        }

        [Test]
        public void Read_ReturnsNullWhenMissing()
        {
            var tmpDir = Path.Combine(Path.GetTempPath(), "uc-marker-" + System.Guid.NewGuid());
            Directory.CreateDirectory(tmpDir);
            try
            {
                var path = Path.Combine(tmpDir, "does-not-exist.json");
                Assert.IsNull(PendingTransitionMarker.Read(path));
            }
            finally
            {
                Directory.Delete(tmpDir, true);
            }
        }
    }
}
