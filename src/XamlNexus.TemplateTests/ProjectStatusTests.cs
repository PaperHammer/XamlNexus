using XamlNexus.Common.Projects;
using XamlNexus.Common.Recipes;
using XamlNexus.Tooling.Diagnostics;
using Xunit;

namespace XamlNexus.TemplateTests;

public sealed class ProjectStatusTests : IDisposable {
    private readonly string root = Directory.CreateTempSubdirectory("xamlnexus-status-tests-").FullName;

    [Theory]
    [InlineData("1.0.0-beta.2", "1.0.0-beta.10", -1)]
    [InlineData("1.0.0", "1.0.0-beta.10", 1)]
    public void VersionComparisonUsesPrereleaseOrder(string left, string right, int expectedSign) {
        Assert.Equal(expectedSign, Math.Sign(XamlNexusRecipeVersion.Compare(left, right)));
    }

    [Fact]
    public void StatusReportsScaffoldAndRecipeUpdatesWithSuggestedCommands() {
        File.WriteAllText(Path.Combine(root, "StatusApp.sln"), "Microsoft Visual Studio Solution File, Format Version 12.00\r\nGlobal\r\nEndGlobal\r\n");
        Directory.CreateDirectory(Path.Combine(root, "StatusApp.UI"));
        Directory.CreateDirectory(Path.Combine(root, "StatusApp.Common"));
        File.WriteAllText(Path.Combine(root, "StatusApp.UI", "StatusApp.UI.csproj"), "<Project />");
        File.WriteAllText(Path.Combine(root, "StatusApp.Common", "StatusApp.Common.csproj"), "<Project />");
        XamlNexusProjectManifestStore.Save(Path.Combine(root, "xamlnexus.json"), new() {
            GeneratorVersion = "1.0.0",
            Project = new() { Name = "StatusApp", Preset = "winui", Profile = "basic", Language = "en-US", SolutionFormat = "sln" },
            Modules = [new() { Id = "sample", Version = "1.0.0", Source = "recipe", Files = [] }],
        });
        var catalog = new XamlNexusRecipeCatalog([new Recipe("sample", "2.0.0")]);

        XamlNexusProjectStatusReport report = XamlNexusProjectStatus.Create(
            XamlNexusProjectLocator.Locate(root), catalog, "1.1.0", probeEnvironment: false);

        Assert.Equal("updateAvailable", report.ScaffoldState);
        Assert.Equal("updateAvailable", Assert.Single(report.Recipes).State);
        Assert.Contains("xamlnexus update --all --dry-run", report.SuggestedCommands);
        Assert.Contains("xamlnexus upgrade --dry-run", report.SuggestedCommands);
    }

    private sealed class Recipe(string id, string version) : IXamlNexusRecipe {
        public XamlNexusRecipeDescriptor Descriptor { get; } = new() {
            Id = id, Version = version, DisplayName = id, SupportedPresets = ["winui"],
        };
        public XamlNexusRecipePlan CreatePlan(XamlNexusRecipeContext context) => new() { Changes = [] };
    }

    public void Dispose() => Directory.Delete(root, recursive: true);
}
