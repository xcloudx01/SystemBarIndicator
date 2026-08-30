using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace SystemBarIndicator.Updates
{
    /// <summary>A newer published release, once the API answer has been vetted.</summary>
    public sealed class UpdateInfo
    {
        public required Version Version { get; init; }
        public required string ZipAssetName { get; init; }
        public required string ZipUrl { get; init; }
        public string? ChecksumsUrl { get; init; }
    }

    /// <summary>Asks the GitHub Releases API whether a newer build is published.</summary>
    public sealed class UpdateChecker
    {
        private const string LatestReleaseUrl =
            "https://api.github.com/repos/coldneuro/SystemBarIndicator/releases/latest";

        // Shared with UpdateInstaller: one connection pool for the API and the downloads.
        internal static readonly HttpClient Http = CreateClient();

        private static HttpClient CreateClient()
        {
            // A separate, shorter connect timeout so a black-holed host gives up quickly
            // instead of occupying the full request timeout. It bounds establishing the
            // connection only — DNS, proxy, TLS and a stalled response still fall under
            // the overall Timeout below.
            var handler = new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(5) };
            var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
            // GitHub rejects requests without a User-Agent.
            client.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("SystemBarIndicator", CurrentVersion().ToString(3)));
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            return client;
        }

        /// <summary>The version this build reports; release tags are compared against it.</summary>
        public static Version CurrentVersion() =>
            UpdateVersion.Normalize(typeof(UpdateChecker).Assembly.GetName().Version ?? new Version(0, 0));

        /// <summary>
        /// Null when this build is current — or when the latest release carries no usable
        /// win-x64 archive, which must read as "nothing to install", not as an error.
        /// </summary>
        public async Task<UpdateInfo?> CheckAsync(CancellationToken ct)
        {
            using var response = await Http.GetAsync(LatestReleaseUrl, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var release = await JsonSerializer
                .DeserializeAsync(stream, GitHubJsonContext.Default.GitHubRelease, ct)
                .ConfigureAwait(false);

            var latest = UpdateVersion.ParseTag(release?.TagName);
            if (release == null || latest == null || !UpdateVersion.IsNewer(CurrentVersion(), latest))
                return null;

            var assets = release.Assets ?? new List<GitHubAsset>();
            string? zipName = UpdateAssets.PickZip(assets.Select(a => a.Name));
            string? zipUrl = DownloadUrlFor(assets, zipName);
            if (zipName == null || zipUrl == null)
                return null;

            string? checksumsName = UpdateAssets.PickChecksums(assets.Select(a => a.Name));
            return new UpdateInfo
            {
                Version = latest,
                ZipAssetName = zipName,
                ZipUrl = zipUrl,
                ChecksumsUrl = DownloadUrlFor(assets, checksumsName)
            };
        }

        private static string? DownloadUrlFor(List<GitHubAsset> assets, string? name)
        {
            if (name == null)
                return null;

            string? url = assets
                .FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase))
                ?.DownloadUrl;
            return url != null && url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                ? url
                : null;
        }
    }

    internal sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubAsset>? Assets { get; set; }
    }

    internal sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? DownloadUrl { get; set; }
    }

    [JsonSerializable(typeof(GitHubRelease))]
    internal sealed partial class GitHubJsonContext : JsonSerializerContext
    {
    }
}
