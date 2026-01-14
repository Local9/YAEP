using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;

namespace YAEP.Services
{
    /// <summary>
    /// Service for checking GitHub releases.
    /// </summary>
    public class GitHubReleaseService : IDisposable
    {
        private const string GITHUB_API_BASE = "https://api.github.com";
        private const string REPO_OWNER = "Local9";
        private const string REPO_NAME = "YAEP";

        private readonly HttpClient _httpClient;
        private readonly string _currentVersion;

        public GitHubReleaseService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "YAEP-UpdateChecker");

            Version? assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
            _currentVersion = assemblyVersion?.ToString() ?? "1.0.0";
        }

        /// <summary>
        /// Checks for a new release on GitHub.
        /// </summary>
        /// <returns>Release information if a new version is available, null otherwise.</returns>
        public async Task<GitHubReleaseInfo?> CheckForUpdateAsync()
        {
            try
            {
                string apiUrl = $"{GITHUB_API_BASE}/repos/{REPO_OWNER}/{REPO_NAME}/releases/latest";
                HttpResponseMessage response = await _httpClient.GetAsync(apiUrl);

                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"Failed to check for updates: {response.StatusCode}");
                    return null;
                }

                GitHubRelease? release = await response.Content.ReadFromJsonAsync<GitHubRelease>();
                if (release == null || string.IsNullOrEmpty(release.TagName))
                {
                    return null;
                }

                string latestVersion = release.TagName.TrimStart('v', 'V');
                string currentVersion = _currentVersion.TrimStart('v', 'V');

                if (IsNewerVersion(latestVersion, currentVersion))
                {
                    return new GitHubReleaseInfo
                    {
                        Version = latestVersion,
                        TagName = release.TagName,
                        Name = release.Name ?? release.TagName,
                        Body = release.Body ?? string.Empty,
                        HtmlUrl = release.HtmlUrl,
                        PublishedAt = release.PublishedAt
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking for updates: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Compares two version strings to determine if the first is newer.
        /// </summary>
        private bool IsNewerVersion(string version1, string version2)
        {
            try
            {
                Version v1 = ParseVersion(version1);
                Version v2 = ParseVersion(version2);
                return v1 > v2;
            }
            catch
            {
                return string.Compare(version1, version2, StringComparison.OrdinalIgnoreCase) > 0;
            }
        }

        /// <summary>
        /// Parses a version string, handling formats like "1.0.13" or "1.0.13.0".
        /// </summary>
        private Version ParseVersion(string versionString)
        {
            string cleanVersion = System.Text.RegularExpressions.Regex.Replace(
                versionString,
                @"[^\d.]",
                "");

            if (Version.TryParse(cleanVersion, out Version? version))
            {
                return version;
            }

            string[] parts = cleanVersion.Split('.');
            if (parts.Length >= 2
                && int.TryParse(parts[0], out int major)
                && int.TryParse(parts[1], out int minor))
            {
                return new Version(
                    major,
                    minor,
                    parts.Length > 2 && int.TryParse(parts[2], out int build) ? build : 0);
            }

            throw new ArgumentException($"Invalid version string: {versionString}");
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    /// <summary>
    /// GitHub release API response model.
    /// </summary>
    internal class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;

        [JsonPropertyName("published_at")]
        public DateTime PublishedAt { get; set; }
    }

    /// <summary>
    /// Simplified release information for the application.
    /// </summary>
    public class GitHubReleaseInfo
    {
        public string Version { get; set; } = string.Empty;
        public string TagName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string HtmlUrl { get; set; } = string.Empty;
        public DateTime PublishedAt { get; set; }
    }
}
