using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace XamlNexus.TemplateTests;

public sealed class JsonSaverTests : IDisposable {
    private readonly string root = Path.Combine(AppContext.BaseDirectory, "json-save-tests", Guid.NewGuid().ToString("N"));

    private static Task Save(bool hybrid, string path, bool fail = false) {
        JsonConverter[] converters = fail ? [new FailingConverter()] : [];
        return hybrid
            ? Winui3_Wpf_XamlNexus.Common.Utils.Storage.JsonSaver.SaveAsync(path, new SaveProbe("replacement"), SaveProbeContext.Default, converters)
            : Winui3_XamlNexus.Common.Utils.Storage.JsonSaver.SaveAsync(path, new SaveProbe("replacement"), SaveProbeContext.Default, converters);
    }

    private string ExistingFile() {
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "settings.json");
        File.WriteAllText(path, "{\"Value\":\"original\"}");
        return path;
    }

    private static Task<SaveProbe> Recover(bool hybrid, string path) => hybrid
        ? Winui3_Wpf_XamlNexus.Common.Utils.Storage.JsonSaver.LoadOrCreateAsync(path, SaveProbeContext.Default, () => new SaveProbe("default"))
        : Winui3_XamlNexus.Common.Utils.Storage.JsonSaver.LoadOrCreateAsync(path, SaveProbeContext.Default, () => new SaveProbe("default"));

    [Theory]
    [InlineData(false, "{broken")]
    [InlineData(true, "{broken")]
    [InlineData(false, "null")]
    [InlineData(true, "null")]
    public async Task CorruptSettings_AreBackedUpBeforeDefaultsAreSaved(bool hybrid, string corrupt) {
        string path = ExistingFile();
        File.WriteAllText(path, corrupt);
        Assert.Equal("default", (await Recover(hybrid, path)).Value);
        string backup = Assert.Single(Directory.GetFiles(root, "*.corrupt.*.bak"));
        Assert.Equal(corrupt, File.ReadAllText(backup));
        Assert.Equal("default", (await Recover(hybrid, path)).Value);
        Assert.Single(Directory.GetFiles(root, "*.corrupt.*.bak"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnreadableSettings_AreNotReset(bool hybrid) {
        string path = ExistingFile();
        string original = File.ReadAllText(path);
        using (var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None)) {
            var error = await Record.ExceptionAsync(() => Recover(hybrid, path));
            Assert.True(error is IOException or UnauthorizedAccessException);
        }
        Assert.Equal(original, File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(root, "*.bak"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingSettings_CreateDefaultsWithoutBackup(bool hybrid) {
        string path = Path.Combine(root, "nested", "settings.json");
        Assert.Equal("default", (await Recover(hybrid, path)).Value);
        Assert.True(File.Exists(path));
        Assert.Empty(Directory.GetFiles(root, "*.bak", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ValidSettings_AreLoadedWithoutRewriting(bool hybrid) {
        string path = ExistingFile();
        string original = File.ReadAllText(path);
        Assert.Equal("original", (await Recover(hybrid, path)).Value);
        Assert.Equal(original, File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(root, "*.bak"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedRecoveryWrite_PreservesCorruptFileAndBackup(bool hybrid) {
        string path = ExistingFile();
        File.WriteAllText(path, "{broken");
        using (var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
            var error = await Record.ExceptionAsync(() => Recover(hybrid, path));
            Assert.True(error is IOException or UnauthorizedAccessException);
        }
        Assert.Equal("{broken", File.ReadAllText(path));
        string backup = Assert.Single(Directory.GetFiles(root, "*.corrupt.*.bak"));
        Assert.Equal("{broken", File.ReadAllText(backup));
        Assert.Empty(Directory.GetFiles(root, "*.tmp"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SerializationFailure_PreservesExistingFile(bool hybrid) {
        string path = ExistingFile();
        string original = File.ReadAllText(path);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Save(hybrid, path, fail: true));
        Assert.Equal(original, File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(root, "*.tmp"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LockedDestination_PreservesOriginalAndRemovesTemporaryFile(bool hybrid) {
        string path = ExistingFile();
        using (var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None)) {
            var error = await Record.ExceptionAsync(() => Save(hybrid, path));
            Assert.True(error is IOException or UnauthorizedAccessException,
                $"Expected a file access failure, got {error?.GetType().Name ?? "no exception"}.");
        }
        Assert.Contains("original", File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(root, "*.tmp"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuccessfulSave_CreatesAndReplacesCompleteJson(bool hybrid) {
        string path = Path.Combine(root, "nested", "settings.json");
        await Save(hybrid, path);
        await Save(hybrid, path);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal("replacement", document.RootElement.GetProperty("Value").GetString());
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp"));
    }

    private sealed class FailingConverter : JsonConverter<SaveProbe> {
        public override SaveProbe? Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => throw new NotSupportedException();
        public override void Write(Utf8JsonWriter writer, SaveProbe value, JsonSerializerOptions options) {
            writer.WriteStartObject();
            writer.WriteString("partial", "data");
            throw new InvalidOperationException("Injected serialization failure");
        }
    }

    public void Dispose() {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}

public sealed record SaveProbe(string Value);
[JsonSerializable(typeof(SaveProbe))]
internal partial class SaveProbeContext : JsonSerializerContext { }
