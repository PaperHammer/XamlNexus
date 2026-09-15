using Xunit;

namespace XamlNexus.TemplateTests;

public sealed class DataDirectoryCopyTests : IDisposable {
    private readonly string root = Path.Combine(AppContext.BaseDirectory, "directory-copy-tests", Guid.NewGuid().ToString("N"));

    private static Task Copy(bool hybrid, string source, string destination) => hybrid
        ? Winui3_Wpf_XamlNexus.Common.Utils.Storage.DataDirectoryCopy.CopyAsync(source, destination)
        : Winui3_XamlNexus.Common.Utils.Storage.DataDirectoryCopy.CopyAsync(source, destination);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Copy_PreservesOriginalFilesAndEmptyDirectories(bool hybrid) {
        string source = Path.Combine(root, "source");
        string destination = Path.Combine(root, "destination");
        Directory.CreateDirectory(Path.Combine(source, "empty"));
        Directory.CreateDirectory(Path.Combine(source, "nested"));
        File.WriteAllText(Path.Combine(source, "nested", "data.txt"), "user data");
        await Copy(hybrid, source, destination);
        Assert.Equal("user data", File.ReadAllText(Path.Combine(source, "nested", "data.txt")));
        Assert.Equal("user data", File.ReadAllText(Path.Combine(destination, "nested", "data.txt")));
        Assert.True(Directory.Exists(Path.Combine(destination, "empty")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Copy_RejectsOccupiedDestinationWithoutChangingEitherSide(bool hybrid) {
        string source = Path.Combine(root, "source");
        string destination = Path.Combine(root, "destination");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(destination);
        File.WriteAllText(Path.Combine(source, "data.txt"), "source data");
        File.WriteAllText(Path.Combine(destination, "data.txt"), "existing user data");
        await Assert.ThrowsAsync<IOException>(() => Copy(hybrid, source, destination));
        Assert.Equal("source data", File.ReadAllText(Path.Combine(source, "data.txt")));
        Assert.Equal("existing user data", File.ReadAllText(Path.Combine(destination, "data.txt")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Copy_RejectsOverlappingAndEquivalentPathsBeforeWriting(bool hybrid) {
        string source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "data.txt"), "keep");
        foreach (string destination in new[] { source, Path.Combine(source, "child"), root, Path.Combine(source, "..", "source") })
            await Assert.ThrowsAsync<IOException>(() => Copy(hybrid, source, destination));
        Assert.False(Directory.Exists(Path.Combine(source, "child")));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(source, "data.txt")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Copy_LockedSourceFailsWithoutDeletingOrTruncatingOriginal(bool hybrid) {
        string source = Path.Combine(root, "source");
        string destination = Path.Combine(root, "destination");
        Directory.CreateDirectory(source);
        string file = Path.Combine(source, "data.txt");
        File.WriteAllText(file, "keep locked data");
        using (var held = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) {
            await Assert.ThrowsAsync<IOException>(() => Copy(hybrid, source, destination));
        }
        Assert.Equal("keep locked data", File.ReadAllText(file));
        Assert.False(File.Exists(Path.Combine(destination, "data.txt")));
    }

    public void Dispose() {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
