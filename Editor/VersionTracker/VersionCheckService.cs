using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace UniClaude.Editor.VersionTracker
{
    /// <summary>
    /// Orchestrates version checks: fetches, parses, caches to <see cref="UniClaudeSettings"/>,
    /// and returns a derived <see cref="CheckResult"/>. Stateless aside from the persisted settings.
    /// </summary>
    public class VersionCheckService
    {
        /// <summary>Cache TTL: re-check allowed after 24 hours.</summary>
        public static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

        /// <summary>Sentinel error string from the fetcher meaning "no releases exist yet" (HTTP 404).</summary>
        public const string NoReleasesError = "No releases published yet";

        readonly IReleaseFetcher _fetcher;
        readonly string _currentVersion;

        /// <summary>Create a new service with the given fetcher and current package version.</summary>
        /// <param name="fetcher">HTTP abstraction.</param>
        /// <param name="currentVersion">Version string from package.json (e.g. "0.2.0").</param>
        public VersionCheckService(IReleaseFetcher fetcher, string currentVersion)
        {
            _fetcher = fetcher;
            _currentVersion = currentVersion;
        }

        /// <summary>
        /// Returns the cached result built from persisted settings. Status is <see cref="CheckStatus.Unknown"/>
        /// when no cache exists.
        /// </summary>
        /// <returns>Cached check result.</returns>
        public CheckResult GetCached()
        {
            var settings = UniClaudeSettings.Load();
            return BuildResultFromSettings(settings);
        }

        /// <summary>
        /// Check for updates. When <paramref name="force"/> is false and cache is fresh, returns cached result
        /// without hitting the network. Persists successful results to settings.
        /// </summary>
        /// <param name="force">If true, always hits the fetcher even when cache is fresh.</param>
        /// <returns>Resolved check result.</returns>
        public async Task<CheckResult> CheckAsync(bool force)
        {
            var settings = UniClaudeSettings.Load();
            if (!force && IsCacheFresh(settings, DateTime.UtcNow))
            {
                return BuildResultFromSettings(settings);
            }

            var fetch = await _fetcher.FetchLatestAsync();
            var now = DateTime.UtcNow;

            if (!fetch.Ok)
            {
                if (fetch.Error == NoReleasesError)
                {
                    return new CheckResult
                    {
                        Status = CheckStatus.UpToDate,
                        CurrentVersion = _currentVersion,
                        CheckedAtIsoUtc = now.ToString("o"),
                    };
                }
                return new CheckResult
                {
                    Status = CheckStatus.Failed,
                    CurrentVersion = _currentVersion,
                    ErrorMessage = fetch.Error,
                    CheckedAtIsoUtc = now.ToString("o"),
                };
            }

            string tag, body, url, publishedAt;
            try
            {
                var json = JObject.Parse(fetch.Body);
                tag = (string)json["tag_name"];
                body = (string)json["body"];
                url = (string)json["html_url"];
                publishedAt = (string)json["published_at"];
                if (string.IsNullOrEmpty(tag))
                    return new CheckResult
                    {
                        Status = CheckStatus.Failed,
                        CurrentVersion = _currentVersion,
                        ErrorMessage = "No tag in response",
                        CheckedAtIsoUtc = now.ToString("o"),
                    };
            }
            catch (Exception ex)
            {
                return new CheckResult
                {
                    Status = CheckStatus.Failed,
                    CurrentVersion = _currentVersion,
                    ErrorMessage = "Parse error: " + ex.Message,
                    CheckedAtIsoUtc = now.ToString("o"),
                };
            }

            settings.LastVersionCheckIsoUtc = now.ToString("o");
            settings.LastKnownLatestVersion = tag;
            settings.LastKnownReleaseNotes = body;
            settings.LastKnownReleaseUrl = url;
            settings.LastKnownReleasePublishedAt = publishedAt;
            UniClaudeSettings.Save(settings);

            return BuildResultFromSettings(settings);
        }

        /// <summary>True when the last check is within <see cref="CacheTtl"/> of <paramref name="now"/>.</summary>
        /// <param name="settings">Settings instance holding the last-check timestamp.</param>
        /// <param name="now">Current UTC time.</param>
        /// <returns>True if cache is fresh.</returns>
        public static bool IsCacheFresh(UniClaudeSettings settings, DateTime now)
        {
            if (string.IsNullOrEmpty(settings.LastVersionCheckIsoUtc)) return false;
            if (!DateTime.TryParse(settings.LastVersionCheckIsoUtc,
                    null, System.Globalization.DateTimeStyles.RoundtripKind, out var last))
                return false;
            return (now - last) < CacheTtl;
        }

        CheckResult BuildResultFromSettings(UniClaudeSettings s)
        {
            if (string.IsNullOrEmpty(s.LastKnownLatestVersion))
            {
                return new CheckResult
                {
                    Status = CheckStatus.Unknown,
                    CurrentVersion = _currentVersion,
                };
            }

            return new CheckResult
            {
                Status = SemverCompare.IsNewer(s.LastKnownLatestVersion, _currentVersion)
                    ? CheckStatus.UpdateAvailable
                    : CheckStatus.UpToDate,
                CurrentVersion = _currentVersion,
                LatestVersion = s.LastKnownLatestVersion,
                ReleaseNotesMarkdown = s.LastKnownReleaseNotes,
                ReleaseUrl = s.LastKnownReleaseUrl,
                PublishedAtIsoUtc = s.LastKnownReleasePublishedAt,
                CheckedAtIsoUtc = s.LastVersionCheckIsoUtc,
            };
        }
    }
}
