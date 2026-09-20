using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using XamlNexus.Tooling.CommandLine;
using XamlNexus.Tooling.Gallery;
using Xunit;

namespace XamlNexus.TemplateTests;

public sealed class GalleryDistributionTests : IDisposable {
    private readonly string root = Directory.CreateTempSubdirectory("xn-gallery-").FullName;
    private static readonly string[] Required = ["XamlNexus.Gallery.exe", "XamlNexus.Gallery.pri", "App.xbf", "MainWindow.xbf"];

    [Fact]
    public void CommandDoesNotRequireAProjectAndRejectsUnknownArguments() {
        Assert.Equal(CliCommand.Gallery, CliParser.Parse(["gallery"], root).Options!.Command);
        Assert.Equal(CliCommand.Help, CliParser.Parse(["gallery", "--help"], root).Options!.Command);
        Assert.False(CliParser.Parse(["gallery", "--project", root], root).Success);
    }

    [Fact]
    public async Task CachedVersionWorksOfflineAndUpgradeUsesSeparateDirectory() {
        byte[] bytes = Archive(Required);
        var manifest = Manifest(bytes);
        int downloads = 0;
        Task<Stream> Download(Uri uri, CancellationToken ct) { downloads++; return Task.FromResult<Stream>(new MemoryStream(bytes)); }
        string first = await GalleryDistribution.EnsureInstalledAsync(root, manifest, manifest.Assets[0], Download);
        string cached = await GalleryDistribution.EnsureInstalledAsync(root, manifest, manifest.Assets[0], (_, _) => throw new Exception("Offline"));
        Assert.Equal(first, cached);
        Assert.Equal(1, downloads);
        var upgrade = Manifest(bytes, "1.2.4");
        string second = await GalleryDistribution.EnsureInstalledAsync(root, upgrade, upgrade.Assets[0], Download);
        Assert.NotEqual(first, second);
        Assert.True(File.Exists(first));
        Assert.True(File.Exists(second));
    }

    [Fact]
    public async Task ConcurrentFirstLaunchDownloadsOnlyOnce() {
        byte[] bytes = Archive(Required);
        var manifest = Manifest(bytes);
        int downloads = 0;
        async Task<Stream> Download(Uri uri, CancellationToken ct) { Interlocked.Increment(ref downloads); await Task.Delay(100, ct); return new MemoryStream(bytes); }
        var paths = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ =>
            GalleryDistribution.EnsureInstalledAsync(root, manifest, manifest.Assets[0], Download)));
        Assert.Single(paths.Distinct());
        Assert.Equal(1, downloads);
    }

    [Fact]
    public async Task CorruptDownloadIsNotInstalledAndCanBeRetried() {
        byte[] bytes = Archive(Required);
        var manifest = Manifest(bytes);
        await Assert.ThrowsAsync<InvalidDataException>(() => GalleryDistribution.EnsureInstalledAsync(root, manifest, manifest.Assets[0],
            (_, _) => Task.FromResult<Stream>(new MemoryStream([1, 2, 3]))));
        Assert.Empty(Directory.GetDirectories(root));
        string exe = await GalleryDistribution.EnsureInstalledAsync(root, manifest, manifest.Assets[0],
            (_, _) => Task.FromResult<Stream>(new MemoryStream(bytes)));
        Assert.True(File.Exists(exe));
    }

    [Theory]
    [InlineData("../escape.exe")]
    [InlineData("nested/../../escape.exe")]
    [InlineData("C:/escape.exe")]
    [InlineData("/escape.exe")]
    [InlineData("file:stream")]
    public async Task TraversalIsRejectedEvenWithMatchingHash(string entry) {
        byte[] bytes = Archive([..Required, entry]);
        var manifest = Manifest(bytes);
        await Assert.ThrowsAsync<InvalidDataException>(() => GalleryDistribution.EnsureInstalledAsync(root, manifest, manifest.Assets[0],
            (_, _) => Task.FromResult<Stream>(new MemoryStream(bytes))));
        Assert.Empty(Directory.GetDirectories(root));
    }

    [Fact]
    public async Task MissingResourcesAndCancelledDownloadLeaveNoInstall() {
        byte[] bytes = Archive(["XamlNexus.Gallery.exe"]);
        var manifest = Manifest(bytes);
        await Assert.ThrowsAsync<InvalidDataException>(() => GalleryDistribution.EnsureInstalledAsync(root, manifest, manifest.Assets[0],
            (_, _) => Task.FromResult<Stream>(new MemoryStream(bytes))));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => GalleryDistribution.EnsureInstalledAsync(root, manifest, manifest.Assets[0],
            (_, _) => throw new OperationCanceledException()));
        Assert.Empty(Directory.GetDirectories(root));
    }

    [Fact]
    public void ManifestPinsVersionArchitectureUrlAndChecksum() {
        var manifest = Manifest(Archive(Required));
        string path = Path.Combine(root, "manifest.json");
        void Save(GalleryManifest value) => File.WriteAllText(path, JsonSerializer.Serialize(value));
        Save(manifest);
        Assert.Equal("1.2.3", GalleryManifest.Read(path, "1.2.3").Version);
        Assert.Throws<InvalidDataException>(() => GalleryManifest.Read(path, "1.2.4"));
        foreach (var invalid in new[] {
            manifest with { SchemaVersion = 2 },
            manifest with { Assets = [manifest.Assets[0], manifest.Assets[0]] },
            manifest with { Assets = [manifest.Assets[0] with { Url = "https://github.com/other/repo/release.zip" }] },
            manifest with { Assets = [manifest.Assets[0] with { Sha256 = "bad" }] },
            manifest with { Version = "../escape" },
        }) {
            Save(invalid);
            Assert.Throws<InvalidDataException>(() => GalleryManifest.Read(path, invalid.Version));
        }
    }

    private static GalleryManifest Manifest(byte[] zip, string version = "1.2.3") => new(1, version,
        [new("win-x64", $"https://github.com/PaperHammer/XamlNexus/releases/download/v{version}/XamlNexus.Gallery-{version}-win-x64.zip",
            Convert.ToHexString(SHA256.HashData(zip)))]);

    private static byte[] Archive(string[] entries) {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
            foreach (string entry in entries) {
                using var writer = new StreamWriter(zip.CreateEntry(entry).Open());
                writer.Write("test resource");
            }
        return output.ToArray();
    }

    public void Dispose() => Directory.Delete(root, true);
}
