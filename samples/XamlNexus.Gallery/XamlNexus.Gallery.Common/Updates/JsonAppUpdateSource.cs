using System.Text.Json;
using XamlNexus.Gallery.Common.Events;

namespace XamlNexus.Gallery.Common.Updates {
    public sealed record AppReleaseInfo(
        Version Version,
        Uri DownloadUri,
        Uri Sha256Uri,
        string Changelog);

    public interface IAppUpdateSource {
        Task<AppReleaseInfo> GetLatestReleaseAsync(
            bool includePreview,
            CancellationToken cancellationToken = default);
    }

    public static class AppUpdateVersionEvaluator {
        public static AppUpdateStatus Evaluate(Version currentVersion, Version latestVersion) {
            ArgumentNullException.ThrowIfNull(currentVersion);
            ArgumentNullException.ThrowIfNull(latestVersion);

            return currentVersion.CompareTo(latestVersion) switch {
                < 0 => AppUpdateStatus.Available,
                0 => AppUpdateStatus.Uptodate,
                _ => AppUpdateStatus.Invalid,
            };
        }
    }

    /// <summary>
    /// Reads a small, provider-neutral update manifest over HTTPS.
    /// The manifest can be hosted by GitHub Releases, object storage, or any CDN.
    /// </summary>
    public sealed class JsonAppUpdateSource : IAppUpdateSource {
        public JsonAppUpdateSource(HttpClient httpClient, Uri manifestUri) {
            ArgumentNullException.ThrowIfNull(httpClient);
            ArgumentNullException.ThrowIfNull(manifestUri);

            if (!manifestUri.IsAbsoluteUri || manifestUri.Scheme != Uri.UriSchemeHttps) {
                throw new ArgumentException("The update manifest URI must use HTTPS.", nameof(manifestUri));
            }

            _httpClient = httpClient;
            _manifestUri = manifestUri;
        }

        public async Task<AppReleaseInfo> GetLatestReleaseAsync(
            bool includePreview,
            CancellationToken cancellationToken = default) {
            using var response = await _httpClient.GetAsync(
                _manifestUri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            if (response.Content.Headers.ContentLength is > MaxManifestBytes) {
                throw new InvalidDataException("The update manifest is larger than 1 MiB.");
            }

            await response.Content.LoadIntoBufferAsync(MaxManifestBytes).ConfigureAwait(false);
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var manifest = await JsonSerializer.DeserializeAsync<UpdateManifest>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidDataException("The update manifest is empty.");

            var release = includePreview ? manifest.Preview ?? manifest.Stable : manifest.Stable;
            if (release is null) {
                throw new InvalidDataException("The update manifest does not define the requested release channel.");
            }

            return ParseRelease(release);
        }

        private static AppReleaseInfo ParseRelease(ReleaseManifest release) {
            var versionText = release.Version?.Trim().TrimStart('v', 'V');
            if (!Version.TryParse(versionText, out var version)) {
                throw new InvalidDataException("The update manifest contains an invalid version.");
            }

            var downloadUri = ParseHttpsUri(release.DownloadUrl, "downloadUrl");
            var sha256Uri = ParseHttpsUri(release.Sha256Url, "sha256Url");
            return new AppReleaseInfo(version, downloadUri, sha256Uri, release.Changelog ?? string.Empty);
        }

        private static Uri ParseHttpsUri(string? value, string propertyName) {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) {
                throw new InvalidDataException($"The update manifest property '{propertyName}' must be an HTTPS URI.");
            }

            return uri;
        }

        private sealed class UpdateManifest {
            public ReleaseManifest? Stable { get; init; }
            public ReleaseManifest? Preview { get; init; }
        }

        private sealed class ReleaseManifest {
            public string? Version { get; init; }
            public string? DownloadUrl { get; init; }
            public string? Sha256Url { get; init; }
            public string? Changelog { get; init; }
        }

        private const long MaxManifestBytes = 1024 * 1024;
        private static readonly JsonSerializerOptions JsonOptions = new() {
            PropertyNameCaseInsensitive = true,
        };
        private readonly HttpClient _httpClient;
        private readonly Uri _manifestUri;
    }
}
