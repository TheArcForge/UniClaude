using System.Threading.Tasks;

namespace UniClaude.Editor.VersionTracker
{
    /// <summary>Fetches the latest GitHub release payload. Abstracted for tests.</summary>
    public interface IReleaseFetcher
    {
        /// <summary>Fetch the latest release. Must not throw — returns a failure result instead.</summary>
        /// <returns>Result containing raw response or error.</returns>
        Task<FetchResult> FetchLatestAsync();
    }

    /// <summary>Raw fetch outcome (before parsing).</summary>
    public class FetchResult
    {
        /// <summary>True when HTTP 200 and a body was read.</summary>
        public bool Ok;
        /// <summary>Response body (JSON) when Ok.</summary>
        public string Body;
        /// <summary>Human-readable error when !Ok.</summary>
        public string Error;
    }
}
