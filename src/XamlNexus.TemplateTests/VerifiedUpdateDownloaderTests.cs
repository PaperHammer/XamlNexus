using System.Net;
using System.Security.Cryptography;
using System.Text;
using Winui3_Wpf_XamlNexus.Common.Updates;
using Xunit;

namespace XamlNexus.TemplateTests;

public sealed class VerifiedUpdateDownloaderTests {
    [Fact]
    public async Task DownloadAndVerifyAsync_WritesInstallerWhenHashMatches() {
        var installer = Encoding.UTF8.GetBytes("trusted installer payload");
        using var client = CreateClient(installer, Convert.ToHexString(SHA256.HashData(installer)));
        var downloader = new VerifiedUpdateDownloader(client);
        var destination = CreateTestDirectory();

        try {
            var path = await downloader.DownloadAndVerifyAsync(CreateRelease(), destination);

            Assert.True(File.Exists(path));
            Assert.Equal(installer, await File.ReadAllBytesAsync(path));
            Assert.Equal("MyApp-2.0.0.exe", Path.GetFileName(path));
        }
        finally {
            DeleteTestDirectory(destination);
        }
    }

    [Fact]
    public async Task DownloadAndVerifyAsync_ReportsByteProgress() {
        var installer = Encoding.UTF8.GetBytes("trusted installer payload");
        using var client = CreateClient(installer, Convert.ToHexString(SHA256.HashData(installer)));
        var downloader = new VerifiedUpdateDownloader(client);
        var destination = CreateTestDirectory();
        var values = new List<AppUpdateDownloadProgressEventArgs>();

        try {
            await downloader.DownloadAndVerifyAsync(
                CreateRelease(),
                destination,
                progress: new InlineProgress<AppUpdateDownloadProgressEventArgs>(values.Add));

            Assert.NotEmpty(values);
            Assert.Equal(0, values[0].BytesReceived);
            Assert.Equal(installer.Length, values[^1].BytesReceived);
            Assert.Equal(installer.Length, values[^1].TotalBytes);
            Assert.Equal(100d, values[^1].Percentage);
        }
        finally {
            DeleteTestDirectory(destination);
        }
    }

    [Fact]
    public async Task DownloadAndVerifyAsync_DeletesStagingDirectoryWhenCancelled() {
        var installer = Encoding.UTF8.GetBytes("trusted installer payload");
        using var client = CreateClient(installer, Convert.ToHexString(SHA256.HashData(installer)));
        var downloader = new VerifiedUpdateDownloader(client);
        var destination = CreateTestDirectory();
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        try {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => downloader.DownloadAndVerifyAsync(
                CreateRelease(),
                destination,
                cancellationSource.Token));

            Assert.Empty(Directory.EnumerateFileSystemEntries(destination));
        }
        finally {
            DeleteTestDirectory(destination);
        }
    }

    [Fact]
    public async Task DownloadAndVerifyAsync_DeletesStagingDirectoryWhenHashDoesNotMatch() {
        var installer = Encoding.UTF8.GetBytes("tampered installer payload");
        using var client = CreateClient(installer, new string('0', 64));
        var downloader = new VerifiedUpdateDownloader(client);
        var destination = CreateTestDirectory();

        try {
            await Assert.ThrowsAsync<InvalidDataException>(
                () => downloader.DownloadAndVerifyAsync(CreateRelease(), destination));

            Assert.Empty(Directory.EnumerateFileSystemEntries(destination));
        }
        finally {
            DeleteTestDirectory(destination);
        }
    }

    [Fact]
    public async Task DownloadAndVerifyAsync_DeletesStagingDirectoryWhenInstallerExceedsLimit() {
        var installer = Encoding.UTF8.GetBytes("installer larger than configured limit");
        using var client = CreateClient(installer, Convert.ToHexString(SHA256.HashData(installer)));
        var downloader = new VerifiedUpdateDownloader(client, maxInstallerBytes: 4);
        var destination = CreateTestDirectory();

        try {
            await Assert.ThrowsAsync<InvalidDataException>(
                () => downloader.DownloadAndVerifyAsync(CreateRelease(), destination));

            Assert.Empty(Directory.EnumerateFileSystemEntries(destination));
        }
        finally {
            DeleteTestDirectory(destination);
        }
    }

    [Fact]
    public async Task DownloadAndVerifyAsync_RejectsUnsupportedInstallerExtension() {
        using var client = CreateClient([], new string('0', 64));
        var downloader = new VerifiedUpdateDownloader(client);
        var destination = CreateTestDirectory();
        var release = CreateRelease(downloadUri: new Uri("https://updates.example/archive.zip"));

        try {
            await Assert.ThrowsAsync<InvalidDataException>(
                () => downloader.DownloadAndVerifyAsync(release, destination));
        }
        finally {
            DeleteTestDirectory(destination);
        }
    }

    private static HttpClient CreateClient(byte[] installer, string hashText) {
        return new HttpClient(new UpdateAssetHandler(installer, hashText));
    }

    private static AppReleaseInfo CreateRelease(Uri? downloadUri = null) {
        return new AppReleaseInfo(
            new Version(2, 0, 0),
            downloadUri ?? new Uri("https://updates.example/MyApp-2.0.0.exe"),
            new Uri("https://updates.example/MyApp-2.0.0.exe.sha256"),
            "Changes");
    }

    private static string CreateTestDirectory() {
        var directory = Path.Combine(AppContext.BaseDirectory, "update-test-data", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteTestDirectory(string directory) {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }

    private sealed class UpdateAssetHandler(byte[] installer, string hashText) : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) {
            HttpContent content = request.RequestUri?.AbsolutePath.EndsWith(".sha256", StringComparison.Ordinal) == true
                ? new StringContent(hashText + "  MyApp-2.0.0.exe", Encoding.UTF8, "text/plain")
                : new ByteArrayContent(installer);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                Content = content,
                RequestMessage = request,
            });
        }
    }

    private sealed class InlineProgress<T>(Action<T> callback) : IProgress<T> {
        public void Report(T value) => callback(value);
    }
}
