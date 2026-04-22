using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace UniClaude.Editor.VersionTracker
{
    /// <summary>
    /// Real <see cref="IReleaseFetcher"/> that hits GitHub's releases/latest endpoint
    /// for a fixed owner/repo. Uses a short timeout and never throws.
    /// </summary>
    public class GitHubReleaseFetcher : IReleaseFetcher
    {
        const string Url = "https://api.github.com/repos/TheArcForge/UniClaude/releases/latest";
        const string UserAgent = "UniClaude-Unity-Editor";
        static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        /// <summary>Fetch the latest release. Returns an error result on any exception.</summary>
        /// <returns>Fetch outcome.</returns>
        public async Task<FetchResult> FetchLatestAsync()
        {
            try
            {
                using var http = new HttpClient { Timeout = Timeout };
                http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
                http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

                using var resp = await http.GetAsync(Url);
                if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return new FetchResult { Ok = false, Error = VersionCheckService.NoReleasesError };
                if (resp.StatusCode == (System.Net.HttpStatusCode)403)
                    return new FetchResult { Ok = false, Error = "GitHub rate limit hit; try later" };
                if (!resp.IsSuccessStatusCode)
                    return new FetchResult { Ok = false, Error = "HTTP " + (int)resp.StatusCode };

                var body = await resp.Content.ReadAsStringAsync();
                return new FetchResult { Ok = true, Body = body };
            }
            catch (TaskCanceledException)
            {
                return new FetchResult { Ok = false, Error = "Network timeout" };
            }
            catch (HttpRequestException ex)
            {
                return new FetchResult { Ok = false, Error = "Network error: " + ex.Message };
            }
            catch (Exception ex)
            {
                return new FetchResult { Ok = false, Error = "Unexpected: " + ex.Message };
            }
        }
    }
}
