using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using UniClaude.Editor.VersionTracker;

namespace UniClaude.Editor.Tests.VersionTracker
{
    class FakeFetcher : IReleaseFetcher
    {
        public FetchResult Next;
        public int CallCount;
        public Task<FetchResult> FetchLatestAsync()
        {
            CallCount++;
            return Task.FromResult(Next);
        }
    }

    [TestFixture]
    public class VersionCheckServiceTests
    {
        string _tmpDir;
        string _originalDir;

        [SetUp]
        public void Setup()
        {
            _tmpDir = Path.Combine(Path.GetTempPath(), "uc-ver-" + System.Guid.NewGuid());
            Directory.CreateDirectory(_tmpDir);
            _originalDir = UniClaudeSettings.SettingsDir;
            UniClaudeSettings.SettingsDir = _tmpDir;
        }

        [TearDown]
        public void Tear()
        {
            UniClaudeSettings.SettingsDir = _originalDir;
            try { if (_tmpDir != null && Directory.Exists(_tmpDir)) Directory.Delete(_tmpDir, true); }
            catch { /* best-effort cleanup */ }
        }

        const string SamplePayload = @"{
            ""tag_name"": ""v0.3.0"",
            ""body"": ""## Changes\n- thing"",
            ""html_url"": ""https://github.com/TheArcForge/UniClaude/releases/tag/v0.3.0"",
            ""published_at"": ""2026-04-20T08:00:00Z""
        }";

        [Test]
        public async Task Check_ReturnsUpdateAvailable_WhenLatestIsNewer()
        {
            var fake = new FakeFetcher { Next = new FetchResult { Ok = true, Body = SamplePayload } };
            var svc = new VersionCheckService(fake, currentVersion: "0.2.0");

            var r = await svc.CheckAsync(force: true);

            Assert.AreEqual(CheckStatus.UpdateAvailable, r.Status);
            Assert.AreEqual("v0.3.0", r.LatestVersion);
            Assert.AreEqual("0.2.0", r.CurrentVersion);
            Assert.IsTrue(r.ReleaseNotesMarkdown.Contains("thing"));
            Assert.IsNotNull(r.CheckedAtIsoUtc);
        }

        [Test]
        public async Task Check_ReturnsUpToDate_WhenEqual()
        {
            var fake = new FakeFetcher { Next = new FetchResult { Ok = true, Body = SamplePayload } };
            var svc = new VersionCheckService(fake, currentVersion: "0.3.0");

            var r = await svc.CheckAsync(force: true);

            Assert.AreEqual(CheckStatus.UpToDate, r.Status);
        }

        [Test]
        public async Task Check_ReturnsFailed_OnFetchError()
        {
            var fake = new FakeFetcher { Next = new FetchResult { Ok = false, Error = "offline" } };
            var svc = new VersionCheckService(fake, currentVersion: "0.2.0");

            var r = await svc.CheckAsync(force: true);

            Assert.AreEqual(CheckStatus.Failed, r.Status);
            Assert.AreEqual("offline", r.ErrorMessage);
        }

        [Test]
        public async Task Check_ReturnsFailed_OnMalformedJson()
        {
            var fake = new FakeFetcher { Next = new FetchResult { Ok = true, Body = "not json" } };
            var svc = new VersionCheckService(fake, currentVersion: "0.2.0");

            var r = await svc.CheckAsync(force: true);

            Assert.AreEqual(CheckStatus.Failed, r.Status);
        }

        [Test]
        public async Task Check_PersistsToSettings()
        {
            var fake = new FakeFetcher { Next = new FetchResult { Ok = true, Body = SamplePayload } };
            var svc = new VersionCheckService(fake, currentVersion: "0.2.0");

            await svc.CheckAsync(force: true);

            var loaded = UniClaudeSettings.Load();
            Assert.AreEqual("v0.3.0", loaded.LastKnownLatestVersion);
            Assert.IsNotNull(loaded.LastVersionCheckIsoUtc);
        }

        [Test]
        public void GetCached_ReturnsUnknown_WhenNoCache()
        {
            var svc = new VersionCheckService(new FakeFetcher(), currentVersion: "0.2.0");
            var r = svc.GetCached();
            Assert.AreEqual(CheckStatus.Unknown, r.Status);
        }

        [Test]
        public async Task CheckAsync_ForceFalse_UsesCache_WhenFresh()
        {
            var fake = new FakeFetcher { Next = new FetchResult { Ok = true, Body = SamplePayload } };
            var svc = new VersionCheckService(fake, currentVersion: "0.2.0");

            await svc.CheckAsync(force: true);
            await svc.CheckAsync(force: false);

            Assert.AreEqual(1, fake.CallCount);
        }

        [Test]
        public async Task CheckAsync_ForceTrue_BypassesCache()
        {
            var fake = new FakeFetcher { Next = new FetchResult { Ok = true, Body = SamplePayload } };
            var svc = new VersionCheckService(fake, currentVersion: "0.2.0");

            await svc.CheckAsync(force: true);
            await svc.CheckAsync(force: true);

            Assert.AreEqual(2, fake.CallCount);
        }

        [Test]
        public void IsCacheFresh_FalseWhenOlderThan24h()
        {
            var settings = new UniClaudeSettings
            {
                LastVersionCheckIsoUtc = System.DateTime.UtcNow.AddHours(-25).ToString("o"),
            };
            Assert.IsFalse(VersionCheckService.IsCacheFresh(settings, System.DateTime.UtcNow));
        }

        [Test]
        public void IsCacheFresh_TrueWhenWithin24h()
        {
            var settings = new UniClaudeSettings
            {
                LastVersionCheckIsoUtc = System.DateTime.UtcNow.AddHours(-1).ToString("o"),
            };
            Assert.IsTrue(VersionCheckService.IsCacheFresh(settings, System.DateTime.UtcNow));
        }

        [Test]
        public void IsCacheFresh_FalseWhenNull()
        {
            var settings = new UniClaudeSettings { LastVersionCheckIsoUtc = null };
            Assert.IsFalse(VersionCheckService.IsCacheFresh(settings, System.DateTime.UtcNow));
        }

        [Test]
        public async Task Check_Returns404AsUpToDate()
        {
            var fake = new FakeFetcher { Next = new FetchResult { Ok = false, Error = "No releases published yet" } };
            var svc = new VersionCheckService(fake, currentVersion: "0.2.0");
            var r = await svc.CheckAsync(force: true);
            Assert.AreEqual(CheckStatus.UpToDate, r.Status);
        }
    }
}
