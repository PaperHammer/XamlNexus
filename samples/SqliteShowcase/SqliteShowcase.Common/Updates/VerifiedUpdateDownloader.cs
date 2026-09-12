using System.Security.Cryptography;
using System.Text;

namespace SqliteShowcase.Common.Updates {
    public sealed class AppUpdateDownloadProgressEventArgs(
        long bytesReceived,
        long? totalBytes) : EventArgs {
        public long BytesReceived { get; } = bytesReceived;
        public long? TotalBytes { get; } = totalBytes;
        public double? Percentage => TotalBytes is > 0
            ? Math.Min(100d, BytesReceived * 100d / TotalBytes.Value)
            : null;
    }

    public interface IAppUpdateDownloader {
        Task<string> DownloadAndVerifyAsync(
            AppReleaseInfo release,
            string destinationRoot,
            CancellationToken cancellationToken = default,
            IProgress<AppUpdateDownloadProgressEventArgs>? progress = null);
    }

    public sealed class VerifiedUpdateDownloader : IAppUpdateDownloader {
        public VerifiedUpdateDownloader(HttpClient httpClient, long maxInstallerBytes = DefaultMaxInstallerBytes) {
            ArgumentNullException.ThrowIfNull(httpClient);
            if (maxInstallerBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxInstallerBytes));

            _httpClient = httpClient;
            _maxInstallerBytes = maxInstallerBytes;
        }

        public async Task<string> DownloadAndVerifyAsync(
            AppReleaseInfo release,
            string destinationRoot,
            CancellationToken cancellationToken = default,
            IProgress<AppUpdateDownloadProgressEventArgs>? progress = null) {
            ArgumentNullException.ThrowIfNull(release);
            ArgumentException.ThrowIfNullOrWhiteSpace(destinationRoot);

            var installerName = GetInstallerName(release);
            var root = Path.GetFullPath(destinationRoot);
            Directory.CreateDirectory(root);
            var stagingDirectory = Path.Combine(root, $"{release.Version}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(stagingDirectory);
            var installerPath = Path.Combine(stagingDirectory, installerName);

            try {
                var expectedHash = await DownloadExpectedHashAsync(release.Sha256Uri, cancellationToken)
                    .ConfigureAwait(false);
                var actualHash = await DownloadInstallerAsync(
                    release.DownloadUri, installerPath, cancellationToken, progress)
                    .ConfigureAwait(false);

                if (!CryptographicOperations.FixedTimeEquals(expectedHash, actualHash)) {
                    throw new InvalidDataException("The downloaded installer SHA-256 hash does not match the release manifest.");
                }

                return installerPath;
            }
            catch {
                TryDeleteDirectory(stagingDirectory);
                throw;
            }
        }

        private async Task<byte[]> DownloadExpectedHashAsync(Uri sha256Uri, CancellationToken cancellationToken) {
            using var response = await _httpClient.GetAsync(
                sha256Uri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            if (response.Content.Headers.ContentLength is > MaxHashFileBytes) {
                throw new InvalidDataException("The SHA-256 file is larger than 4 KiB.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var buffer = new MemoryStream();
            var chunk = new byte[1024];
            while (true) {
                var read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
                if (read == 0) break;
                if (buffer.Length + read > MaxHashFileBytes) {
                    throw new InvalidDataException("The SHA-256 file is larger than 4 KiB.");
                }
                buffer.Write(chunk, 0, read);
            }

            var hashText = Encoding.UTF8.GetString(buffer.ToArray()).Trim();
            var hashToken = hashText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (hashToken is null || hashToken.Length != Sha256HexLength || !hashToken.All(Uri.IsHexDigit)) {
                throw new InvalidDataException("The SHA-256 file does not begin with a valid 64-character hash.");
            }

            return Convert.FromHexString(hashToken);
        }

        private async Task<byte[]> DownloadInstallerAsync(
            Uri downloadUri,
            string installerPath,
            CancellationToken cancellationToken,
            IProgress<AppUpdateDownloadProgressEventArgs>? progress) {
            using var response = await _httpClient.GetAsync(
                downloadUri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var contentLength = response.Content.Headers.ContentLength;
            if (contentLength.HasValue && contentLength.Value > _maxInstallerBytes) {
                throw new InvalidDataException("The installer is larger than the configured download limit.");
            }

            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var output = new FileStream(
                installerPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var chunk = new byte[81920];
            long totalBytes = 0;
            progress?.Report(new AppUpdateDownloadProgressEventArgs(0, contentLength));
            while (true) {
                var read = await input.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
                if (read == 0) break;

                totalBytes += read;
                if (totalBytes > _maxInstallerBytes) {
                    throw new InvalidDataException("The installer is larger than the configured download limit.");
                }

                hash.AppendData(chunk, 0, read);
                await output.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                progress?.Report(new AppUpdateDownloadProgressEventArgs(totalBytes, contentLength));
            }

            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            return hash.GetHashAndReset();
        }

        private static string GetInstallerName(AppReleaseInfo release) {
            var name = Uri.UnescapeDataString(Path.GetFileName(release.DownloadUri.AbsolutePath));
            var extension = Path.GetExtension(name);
            if (string.IsNullOrWhiteSpace(name)
                || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || !AllowedInstallerExtensions.Contains(extension)) {
                throw new InvalidDataException("The update URL must point to a supported Windows installer file.");
            }

            return name;
        }

        private static void TryDeleteDirectory(string directory) {
            try {
                if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
            }
            catch {
                // Preserve the download or verification exception. A later cleanup pass can remove stale staging data.
            }
        }

        public const long DefaultMaxInstallerBytes = 1024L * 1024L * 1024L;
        private const int MaxHashFileBytes = 4 * 1024;
        private const int Sha256HexLength = 64;
        private static readonly HashSet<string> AllowedInstallerExtensions = new(StringComparer.OrdinalIgnoreCase) {
            ".exe", ".msi", ".msix", ".msixbundle", ".appinstaller",
        };
        private readonly HttpClient _httpClient;
        private readonly long _maxInstallerBytes;
    }
}
