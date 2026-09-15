using XamlNexus.Common.Projects;
using Xunit;

namespace XamlNexus.TemplateTests;

public sealed class ProjectManifestTests {
    [Fact]
    public void SaveAndLoad_ValidManifest_RoundTrips() {
        string testDirectory = CreateTestDirectory();
        string manifestPath = Path.Combine(testDirectory, "xamlnexus.json");
        try {
            XamlNexusProjectManifest expected = CreateManifest();

            XamlNexusProjectManifestStore.Save(manifestPath, expected);
            XamlNexusProjectManifest actual = XamlNexusProjectManifestStore.Load(manifestPath);

            Assert.Equal(1, actual.SchemaVersion);
            Assert.Equal("1.2.3", actual.GeneratorVersion);
            Assert.Equal("SampleApp", actual.Project.Name);
            Assert.Equal("winui", actual.Project.Preset);
            Assert.Collection(
                actual.Modules,
                module => {
                    Assert.Equal("settings", module.Id);
                    Assert.Equal("template", module.Source);
                });
            Assert.False(File.Exists(manifestPath + ".tmp"));
        }
        finally {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Fact]
    public void Save_UnsupportedSchema_ThrowsWithoutCreatingFile() {
        string testDirectory = CreateTestDirectory();
        string manifestPath = Path.Combine(testDirectory, "xamlnexus.json");
        try {
            XamlNexusProjectManifest manifest = CreateManifest(schemaVersion: 99);

            InvalidDataException exception = Assert.Throws<InvalidDataException>(
                () => XamlNexusProjectManifestStore.Save(manifestPath, manifest));

            Assert.Contains("schemaVersion", exception.Message);
            Assert.False(File.Exists(manifestPath));
        }
        finally {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Fact]
    public void Save_DuplicateModuleIds_Throws() {
        string testDirectory = CreateTestDirectory();
        try {
            XamlNexusProjectManifest manifest = CreateManifest(modules: [
                CreateModule("settings"),
                CreateModule("SETTINGS"),
            ]);

            InvalidDataException exception = Assert.Throws<InvalidDataException>(
                () => XamlNexusProjectManifestStore.Save(
                    Path.Combine(testDirectory, "xamlnexus.json"),
                    manifest));

            Assert.Contains("Duplicate module id", exception.Message);
        }
        finally {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Theory]
    [InlineData("avalonia", "zh-CN", "sln")]
    [InlineData("winui", "fr-FR", "sln")]
    [InlineData("winui", "zh-CN", "invalid")]
    public void Validate_UnsupportedProjectIdentity_ReturnsErrors(
        string preset,
        string language,
        string solutionFormat) {
        XamlNexusProjectManifest manifest = CreateManifest(
            project: new XamlNexusProjectIdentity {
                Name = "SampleApp",
                Preset = preset,
                Language = language,
                SolutionFormat = solutionFormat,
            });

        Assert.NotEmpty(manifest.Validate());
    }

    [Fact]
    public void Locate_MissingManifest_ThrowsClearError() {
        string testDirectory = CreateTestDirectory();
        try {
            FileNotFoundException exception = Assert.Throws<FileNotFoundException>(
                () => XamlNexusProjectLocator.Locate(testDirectory));

            Assert.Contains("xamlnexus.json", exception.Message);
        }
        finally {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Fact]
    public void Save_BaselineContentThatDoesNotMatchHash_Throws() {
        string testDirectory = CreateTestDirectory();
        try {
            XamlNexusProjectManifest original = CreateManifest();
            var manifest = new XamlNexusProjectManifest {
                SchemaVersion = original.SchemaVersion,
                GeneratorVersion = original.GeneratorVersion,
                Project = original.Project,
                Modules = original.Modules,
                ScaffoldFiles = [new XamlNexusManagedFile {
                    Path = "App.xaml.cs",
                    Sha256 = new string('0', 64),
                    BaselineContentGzipBase64 = XamlNexusBaselineContent.Encode("content"u8.ToArray()),
                    UserEditable = true,
                }],
            };

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                XamlNexusProjectManifestStore.Save(
                    Path.Combine(testDirectory, "xamlnexus.json"),
                    manifest));

            Assert.Contains("baselineContentGzipBase64", exception.Message);
        }
        finally {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    private static XamlNexusProjectManifest CreateManifest(
        int schemaVersion = 1,
        XamlNexusProjectIdentity? project = null,
        IReadOnlyList<XamlNexusManagedModule>? modules = null) {
        return new XamlNexusProjectManifest {
            SchemaVersion = schemaVersion,
            GeneratorVersion = "1.2.3",
            Project = project ?? new XamlNexusProjectIdentity {
                Name = "SampleApp",
                Preset = "winui",
                Language = "en-US",
                SolutionFormat = "sln",
            },
            Modules = modules ?? [CreateModule("settings")],
        };
    }

    private static XamlNexusManagedModule CreateModule(string id) => new() {
        Id = id,
        Version = "1.2.3",
        Source = "template",
    };

    private static string CreateTestDirectory() {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "xamlnexus-manifest-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
