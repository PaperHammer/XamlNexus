using XamlNexus.Common.CommandLine;
using XamlNexus.Common.Projects;
using XamlNexus.Common.Recipes;
using Xunit;

namespace XamlNexus.TemplateTests;

public sealed class RecipeBatchTests : IDisposable {
    private readonly string root = Directory.CreateTempSubdirectory("xamlnexus-batch-tests-").FullName;
    private const string ProjectPath = "Demo.UI/Demo.UI.csproj";
    private XamlNexusProjectContext Context => XamlNexusProjectLocator.Locate(root);
    public RecipeBatchTests() {
        foreach (string relative in new[] { "Demo.sln", "Directory.Build.props", ProjectPath, "Demo.Common/Demo.Common.csproj" }) {
            string path = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "<Project />");
        }
        XamlNexusProjectManifestStore.Save(Path.Combine(root, "xamlnexus.json"), new() {
            GeneratorVersion = "1.0.3", Modules = [],
            Project = new() { Name = "Demo", Preset = "winui", Profile = "standard", Language = "en-US", SolutionFormat = "sln" },
        });
    }
    private Dictionary<string, string> Snapshot() => Directory.GetFiles(root, "*", SearchOption.AllDirectories)
        .ToDictionary(path => Path.GetRelativePath(root, path), File.ReadAllText);
    private void AssertSnapshot(Dictionary<string, string> expected) {
        var actual = Snapshot();
        Assert.Equal(expected.Count, actual.Count);
        foreach (var (path, content) in expected) Assert.Equal(content, actual[path]);
    }
    private XamlNexusRecipeBatchPlan Prepare() => XamlNexusRecipeTransaction.PrepareApplyBatch(Context, [new Recipe("one"), new Recipe("two")]);

    [Fact]
    public void PreviewIsReadOnlyAndCommitCombinesSharedProjectChanges() {
        var before = Snapshot();
        var plan = Prepare();
        AssertSnapshot(before);
        Assert.Equal(2, plan.Recipes.Count);
        Assert.Equal(1, plan.ChangedFiles.Count(path => path == ProjectPath));
        XamlNexusRecipeTransaction.ApplyBatch(Context, plan);
        string xml = File.ReadAllText(Path.Combine(root, ProjectPath));
        Assert.Contains("Package.one", xml);
        Assert.Contains("Package.two", xml);
        Assert.Equal(new[] { "one", "two" }, Context.Manifest.Modules.Select(module => module.Id));
        Assert.Equal("standard", Context.Manifest.Project.Profile);
        Assert.True(XamlNexusProjectValidator.Validate(Context).IsValid);
    }

    [Fact]
    public void LaterPlanningFailurePreservesOriginalProject() {
        var before = Snapshot();
        Assert.Throws<InvalidOperationException>(() => XamlNexusRecipeTransaction.PrepareApplyBatch(Context, [new Recipe("one"), new Recipe("two", fail: true)]));
        AssertSnapshot(before);
    }

    [Fact]
    public void ManifestWriteFailureRollsBackBothRecipesAndSharedProject() {
        var before = Snapshot();
        var plan = Prepare();
        using var held = new FileStream(Context.ManifestPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var error = Assert.Throws<XamlNexusRecipeException>(() => XamlNexusRecipeTransaction.ApplyBatch(Context, plan));
        Assert.Equal("XR1303", error.Code);
        AssertSnapshot(before);
        Assert.False(Directory.Exists(Path.Combine(root, "one")));
        Assert.False(Directory.Exists(Path.Combine(root, "two")));
    }

    [Theory]
    [InlineData("Demo.UI/Demo.UI.csproj", "XR1211")]
    [InlineData("xamlnexus.json", "XR1803")]
    [InlineData("one/service.txt", "XR1206")]
    public void ConcurrentChangesRejectWholeBatch(string relative, string code) {
        var context = Context;
        var plan = Prepare();
        string path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.AppendAllText(path, " ");
        var before = Snapshot();
        var error = Assert.Throws<XamlNexusRecipeException>(() => XamlNexusRecipeTransaction.ApplyBatch(context, plan));
        Assert.Equal(code, error.Code);
        AssertSnapshot(before);
    }

    [Fact]
    public void PlannerReusesInstalledDependenciesAndRejectsReverseConflicts() {
        XamlNexusManagedModule[] installed = [new() { Id = "one", Version = "1.0.0", Source = "builtin" }];
        var catalog = new XamlNexusRecipeCatalog([new Recipe("two", dependencies: ["one"])]);
        var plan = CompositionPlanner.Resolve("winui", "standard", ["two"], catalog, installed);
        Assert.Equal("two", Assert.Single(plan).Descriptor.Id);
        var conflicting = new XamlNexusRecipeCatalog([new Recipe("one", conflicts: ["two"]), new Recipe("two")]);
        Assert.Throws<InvalidOperationException>(() => CompositionPlanner.Resolve("winui", "standard", ["two"], conflicting, installed));
        Assert.Throws<XamlNexusRecipeException>(() => CompositionPlanner.Resolve("winui", "standard", ["one"], conflicting, installed));
    }

    [Theory]
    [InlineData("one,two", true)]
    [InlineData("one", true)]
    [InlineData("one,", false)]
    [InlineData(",one", false)]
    [InlineData("one,,two", false)]
    public void ParserValidatesBatchIds(string ids, bool success) => Assert.Equal(success, CliParser.Parse(["add", ids], root).Success);

    private sealed class Recipe(string id, bool fail = false, string[]? dependencies = null, string[]? conflicts = null) : IXamlNexusRecipe {
        public XamlNexusRecipeDescriptor Descriptor { get; } = new() {
            Id = id, Version = "1.0.0", DisplayName = id, SupportedPresets = ["winui"],
            Dependencies = dependencies ?? [], Conflicts = conflicts ?? [],
        };
        public XamlNexusRecipePlan CreatePlan(XamlNexusRecipeContext context) => fail
            ? throw new InvalidOperationException("Simulated planning failure")
            : new() {
                Changes = [XamlNexusRecipeFileChange.CreateText(id + "/service.txt", id)],
                ProjectOperations = [new AddPackageReferenceOperation(ProjectPath, "Package." + id, "1.0.0")],
            };
    }
    public void Dispose() => Directory.Delete(root, recursive: true);
}
