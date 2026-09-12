using System.Net;
using System.Text;
using Winui3_Wpf_XamlNexus.Common.Events;
using Winui3_Wpf_XamlNexus.Common.Updates;
using Xunit;

namespace XamlNexus.TemplateTests;

public sealed class JsonAppUpdateSourceTests {
    [Fact]
    public async Task GetLatestReleaseAsync_ReadsStableRelease() {
        using var client = CreateClient(ValidManifest);
        var source = new JsonAppUpdateSource(client, ManifestUri);

        var release = await source.GetLatestReleaseAsync(includePreview: false);

        Assert.Equal(new Version(1, 2, 3), release.Version);
        Assert.Equal("https://updates.example/app-1.2.3.exe", release.DownloadUri.OriginalString);
        Assert.Equal("Stable changes", release.Changelog);
    }

    [Fact]
    public async Task GetLatestReleaseAsync_SelectsPreviewRelease() {
        using var client = CreateClient(ValidManifest);
        var source = new JsonAppUpdateSource(client, ManifestUri);

        var release = await source.GetLatestReleaseAsync(includePreview: true);

        Assert.Equal(new Version(2, 0, 0), release.Version);
        Assert.Equal("Preview changes", release.Changelog);
    }

    [Fact]
    public async Task GetLatestReleaseAsync_RejectsInsecureAssetUri() {
        const string manifest = """
            {
              "stable": {
                "version": "1.0.0",
                "downloadUrl": "http://updates.example/app.exe",
                "sha256Url": "https://updates.example/app.exe.sha256"
              }
            }
            """;
        using var client = CreateClient(manifest);
        var source = new JsonAppUpdateSource(client, ManifestUri);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => source.GetLatestReleaseAsync(includePreview: false));
    }

    [Theory]
    [InlineData("1.0.0", "1.0.1", AppUpdateStatus.Available)]
    [InlineData("1.0.0", "1.0.0", AppUpdateStatus.Uptodate)]
    [InlineData("2.0.0", "1.9.0", AppUpdateStatus.Invalid)]
    public void Evaluate_ReturnsExpectedStatus(
        string current,
        string latest,
        AppUpdateStatus expected) {
        var actual = AppUpdateVersionEvaluator.Evaluate(new Version(current), new Version(latest));

        Assert.Equal(expected, actual);
    }

    private static HttpClient CreateClient(string responseBody) {
        return new HttpClient(new StubHttpMessageHandler(responseBody));
    }

    private sealed class StubHttpMessageHandler(string responseBody) : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) {
            var response = new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
                RequestMessage = request,
            };
            return Task.FromResult(response);
        }
    }

    private static readonly Uri ManifestUri = new("https://updates.example/update-manifest.json");
    private const string ValidManifest = """
        {
          "stable": {
            "version": "v1.2.3",
            "downloadUrl": "https://updates.example/app-1.2.3.exe",
            "sha256Url": "https://updates.example/app-1.2.3.exe.sha256",
            "changelog": "Stable changes"
          },
          "preview": {
            "version": "2.0.0",
            "downloadUrl": "https://updates.example/app-2.0.0.exe",
            "sha256Url": "https://updates.example/app-2.0.0.exe.sha256",
            "changelog": "Preview changes"
          }
        }
        """;
}
