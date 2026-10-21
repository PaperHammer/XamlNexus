using XamlNexus.Tooling.CommandLine;
using XamlNexus.Common.Generators;
using XamlNexus.Common.Projects;
using XamlNexus.Common.Recipes;
using XamlNexus.Common.Utils;
using XamlNexus.Generator.Winui3App;
using XamlNexus.Generator.Winui3_Wpf_App;
using XamlNexus.Recipes.BuiltIn;
using Xunit;

namespace XamlNexus.TemplateTests;

public sealed class CompositionTests : IDisposable {
    private readonly string root = Path.Combine(AppContext.BaseDirectory, "composition-tests", Guid.NewGuid().ToString("N"));
    private ProjectConfig Config => new() { SlnName = "Demo", OutputPath = root, Framework = FrameworkType.Winui3, SlnType = SolutionType.Sln };
    private static IXamlNexusRecipeCatalog Catalog(params IXamlNexusRecipe[] recipes) => new XamlNexusRecipeCatalog(recipes);

    [Fact]
    public void ParserSeparatesArchitectureProfileAndFeatures() {
        var parsed = CliParser.Parse(["new", "Demo", "--preset", "hybrid", "--profile", "standard", "--features", "sqlite,editorconfig,sqlite"], root);
        Assert.True(parsed.Success);
        Assert.Equal(FrameworkType.Winui3_Wpf, parsed.Options!.Project!.Framework);
        Assert.Equal("standard", parsed.Options.Profile);
        Assert.Equal(new[] { "sqlite", "editorconfig" }, parsed.Options.Features);
        foreach (var args in new[] {
            new[] { "new", "Demo", "--profile", "unknown" },
            new[] { "new", "Demo", "--features", "sqlite," },
            new[] { "new", "Demo", "--features", "sqlite", "--features", "editorconfig" },
            new[] { "new", "Demo", "--profile", "standard", "--profile", "standard" },
            new[] { "add", "sqlite", "--profile", "standard" },
        }) Assert.False(CliParser.Parse(args, root).Success);
    }

    [Fact]
    public void ParserAcceptsBasicWithEitherArchitectureAndFeatures() {
        foreach (string preset in new[] { "winui", "hybrid" }) {
            var parsed = CliParser.Parse(["new", "Demo", "--preset", preset, "--profile", "basic", "--features", "settings,sqlite"], root);
            Assert.True(parsed.Success);
            Assert.Equal("basic", parsed.Options!.Profile);
            Assert.Equal("basic", parsed.Options.Project!.Profile);
        }
    }

    [Fact]
    public void CreationReusesIncludedFeaturesAndDependencies() {
        var generator = new Generator(included: ["built-in"]);
        var catalog = Catalog(new Recipe("feature", ["built-in"]));
        string created = ProjectComposer.Create(new(Config, "standard", ["built-in", "feature"]), generator, catalog);
        var modules = XamlNexusProjectLocator.Locate(created).Manifest.Modules;
        Assert.Equal("template", Assert.Single(modules, module => module.Id == "built-in").Source);
        Assert.Equal("recipe", Assert.Single(modules, module => module.Id == "feature").Source);
        Assert.False(File.Exists(Path.Combine(created, "built-in.txt")));
    }

    [Fact]
    public void CreationRejectsConflictsWithIncludedModulesBeforeGenerating() {
        Assert.Throws<InvalidOperationException>(() => ProjectComposer.Create(new(Config, "standard", ["feature"]),
            new Generator(included: ["built-in"]), Catalog(new Recipe("feature", conflicts: ["built-in"]))));
        Assert.False(Directory.Exists(root));
    }

    [Theory]
    [InlineData("basic", false, "settings,app-update")]
    [InlineData("standard", false, "app-update")]
    [InlineData("basic", true, "settings,app-update")]
    [InlineData("standard", true, "app-update")]
    public void UpdateCompositionIncludesSettingsOnlyWhenMissing(string profile, bool explicitSettings, string expected) {
        var generator = new Winui3Generator();
        var plan = CompositionPlanner.ResolveForCreation("winui", profile,
            explicitSettings ? ["settings", "app-update"] : ["app-update"], BuiltInRecipeCatalog.Create(), generator.GetIncludedModuleIds(profile));
        Assert.Equal(expected.Split(','), plan.Select(recipe => recipe.Descriptor.Id));
    }

    [Fact]
    public void HybridCreationReusesBuiltInTrayDespitePureOnlyRecipe() {
        var generator = new Winui3_WpfGenerator();
        var plan = CompositionPlanner.ResolveForCreation("hybrid", "standard", ["system-tray", "settings"],
            BuiltInRecipeCatalog.Create(), generator.GetIncludedModuleIds("standard"));
        Assert.Empty(plan);
    }

    [Fact]
    public void PlannerSortsDependenciesAndDeduplicates() {
        var recipes = Catalog(new Recipe("feature", ["dependency"]), new Recipe("dependency"));
        var plan = CompositionPlanner.Resolve("winui", "standard", ["feature", "FEATURE"], recipes);
        Assert.Equal(new[] { "dependency", "feature" }, plan.Select(recipe => recipe.Descriptor.Id));
    }

    [Fact]
    public void PlannerRejectsCyclesConflictsAndUnsupportedArchitecture() {
        Assert.Throws<InvalidOperationException>(() => CompositionPlanner.Resolve("winui", "standard", ["one"], Catalog(new Recipe("one", ["two"]), new Recipe("two", ["one"]))));
        Assert.Throws<InvalidOperationException>(() => CompositionPlanner.Resolve("winui", "standard", ["one", "two"], Catalog(new Recipe("one", conflicts: ["two"]), new Recipe("two"))));
        Assert.Throws<InvalidOperationException>(() => CompositionPlanner.Resolve("hybrid", "standard", ["one"], Catalog(new Recipe("one"))));
    }

    [Fact]
    public void PreflightFailureDoesNotCreateOutput() {
        Assert.Throws<InvalidOperationException>(() => ProjectComposer.Create(new(Config, "standard", ["missing"]), new Generator(), Catalog()));
        Assert.False(Directory.Exists(root));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailureRemovesOnlyStagingAndPreservesExistingProject(bool failGenerator) {
        Directory.CreateDirectory(Path.Combine(root, "Demo"));
        string marker = Path.Combine(root, "Demo", "user.txt");
        File.WriteAllText(marker, "user work");
        Assert.Throws<InvalidOperationException>(() => ProjectComposer.Create(new(Config, "standard", ["one", "two"]),
            new Generator(failGenerator), Catalog(new Recipe("one"), new Recipe("two", fail: true))));
        Assert.Equal("user work", File.ReadAllText(marker));
        Assert.Equal(new[] { Path.Combine(root, "Demo") }, Directory.GetDirectories(root));
    }

    [Fact]
    public void SuccessMatchesPostCreationInstallationAndRetainsProfileOnRemove() {
        Directory.CreateDirectory(Path.Combine(root, "Demo"));
        var recipe = new Recipe("one");
        string created = ProjectComposer.Create(new(Config, "standard", ["one"]), new Generator(), Catalog(recipe));
        Assert.NotEqual(Path.Combine(root, "Demo"), created);
        Assert.DoesNotContain(Directory.GetDirectories(root), path => Path.GetFileName(path).StartsWith(".xamlnexus-create-"));
        var project = XamlNexusProjectLocator.Locate(created);
        Assert.True(XamlNexusProjectValidator.Validate(project).IsValid);
        Assert.Equal("standard", project.Manifest.Project.Profile);
        var directConfig = Config;
        directConfig.OutputPath = Path.Combine(root, "direct");
        new Generator().Generate(directConfig);
        var direct = XamlNexusProjectLocator.Locate(Path.Combine(directConfig.OutputPath, "Demo"));
        XamlNexusRecipeTransaction.Apply(direct, recipe);
        Assert.Equal(File.ReadAllText(Path.Combine(created, "one.txt")), File.ReadAllText(Path.Combine(direct.RootDirectory, "one.txt")));
        XamlNexusRecipeTransaction.Remove(project, recipe);
        var remaining = XamlNexusProjectLocator.Locate(created);
        Assert.Equal("standard", remaining.Manifest.Project.Profile);
        Assert.Empty(remaining.Manifest.Modules);
        Assert.False(File.Exists(Path.Combine(created, "one.txt")));
    }

    [Fact]
    public void LegacyManifestDoesNotInventProfile() {
        new Generator().Generate(Config);
        string path = Path.Combine(root, "Demo", "xamlnexus.json");
        File.WriteAllText(path, File.ReadAllText(path).Replace("\"profile\": \"standard\",", ""));
        var project = XamlNexusProjectLocator.Locate(Path.GetDirectoryName(path)!);
        XamlNexusRecipeTransaction.Apply(project, new Recipe("one"));
        Assert.Null(XamlNexusProjectManifestStore.Load(path).Project.Profile);
    }

    [Fact]
    public void DestinationCreatedDuringGenerationIsNotOverwritten() {
        var generator = new Generator(beforeReturn: _ => {
            Directory.CreateDirectory(Path.Combine(root, "Demo"));
            File.WriteAllText(Path.Combine(root, "Demo", "user.txt"), "concurrent work");
        });
        string created = ProjectComposer.Create(new(Config, "standard", []), generator, Catalog());
        Assert.NotEqual(Path.Combine(root, "Demo"), created);
        Assert.True(File.Exists(Path.Combine(created, "xamlnexus.json")));
        Assert.Equal("concurrent work", File.ReadAllText(Path.Combine(root, "Demo", "user.txt")));
        Assert.Equal(2, Directory.GetDirectories(root).Length);
    }

    [Fact]
    public void CopyFailureRemovesReservedDestinationAndReportsLockedTemporaryFile() {
        FileStream? held = null;
        string? staging = null;
        try {
            var generator = new Generator(beforeReturn: config => {
                staging = config.OutputPath;
                held = new FileStream(Path.Combine(staging, "Demo", "locked.bin"), FileMode.CreateNew, FileAccess.Write, FileShare.None);
            });
            var error = Assert.ThrowsAny<IOException>(() => ProjectComposer.Create(new(Config, "standard", []), generator, Catalog()));
            Assert.False(Directory.Exists(Path.Combine(root, "Demo")));
            Assert.NotNull(error.Data["CleanupError"]);
            Assert.Empty(Directory.GetDirectories(root));
        }
        finally {
            held?.Dispose();
            if (staging is not null && Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
    }

    private sealed class Generator(bool fail = false, Action<ProjectConfig>? beforeReturn = null, string[]? included = null) : IGenerator {
        public IReadOnlyList<string> GetIncludedModuleIds(string profile) => included ?? [];
        public bool Generate(ProjectConfig config) => GenerateProject(config).Success;
        public GenerationResult GenerateProject(ProjectConfig config, Action<GenerationProgress>? progress = null) {
            string target = Path.Combine(config.OutputPath, config.SlnName);
            foreach (string file in new[] { "Demo.sln", "Directory.Build.props", "Demo.UI/Demo.UI.csproj", "Demo.Common/Demo.Common.csproj" }) {
                string full = Path.Combine(target, file);
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                File.WriteAllText(full, "<Project />");
            }
            XamlNexusProjectManifestStore.Save(Path.Combine(target, "xamlnexus.json"), new() {
                GeneratorVersion = "1.0.3", Modules = GetIncludedModuleIds(config.Profile)
                    .Select(id => new XamlNexusManagedModule { Id = id, Version = "1.0.3", Source = "template" }).ToArray(),
                Project = new() { Name = "Demo", Preset = "winui", Profile = config.Profile, Language = "en-US", SolutionFormat = "sln" },
            });
            beforeReturn?.Invoke(config);
            return fail ? new(null, new InvalidOperationException("Simulated generation failure.")) : new(target, null);
        }
    }

    private sealed class Recipe(string id, string[]? dependencies = null, string[]? conflicts = null, bool fail = false) : IXamlNexusRecipe {
        public XamlNexusRecipeDescriptor Descriptor { get; } = new() {
            Id = id, Version = "1.0.0", DisplayName = id, SupportedPresets = ["winui"],
            Dependencies = dependencies ?? [], Conflicts = conflicts ?? [],
        };
        public XamlNexusRecipePlan CreatePlan(XamlNexusRecipeContext context) => fail
            ? throw new InvalidOperationException("Simulated installation failure.")
            : new() { Changes = [XamlNexusRecipeFileChange.CreateText(id + ".txt", id)] };
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
}
