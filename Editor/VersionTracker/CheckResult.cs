namespace UniClaude.Editor.VersionTracker
{
    /// <summary>Outcome of a version check, consumed by the UI layer.</summary>
    public enum CheckStatus
    {
        /// <summary>Not yet checked this session and no cached result.</summary>
        Unknown,
        /// <summary>Current version is greater than or equal to latest known release.</summary>
        UpToDate,
        /// <summary>A newer release exists.</summary>
        UpdateAvailable,
        /// <summary>Last check failed (network, rate limit, parse).</summary>
        Failed,
    }

    /// <summary>Snapshot of a version-check result.</summary>
    public class CheckResult
    {
        /// <summary>Derived status (computed from current vs. latest).</summary>
        public CheckStatus Status;
        /// <summary>Version from package.json at check time.</summary>
        public string CurrentVersion;
        /// <summary>Latest release tag name, or null on failure / no releases.</summary>
        public string LatestVersion;
        /// <summary>Release notes markdown, or null.</summary>
        public string ReleaseNotesMarkdown;
        /// <summary>HTML URL of the release page, or null.</summary>
        public string ReleaseUrl;
        /// <summary>ISO-8601 published-at timestamp, or null.</summary>
        public string PublishedAtIsoUtc;
        /// <summary>Human-readable error message when Status == Failed.</summary>
        public string ErrorMessage;
        /// <summary>ISO-8601 UTC timestamp of when this check ran.</summary>
        public string CheckedAtIsoUtc;
    }
}
