using XamlNexus.Common.Generators;
using XamlNexus.Common.Utils;
using Xunit;

namespace XamlNexus.TemplateTests;

[CollectionDefinition("Generation console isolation", DisableParallelization = true)]
public sealed class GenerationConsoleIsolationCollection { }

[Collection("Generation console isolation")]
public sealed class GenerationBoundaryTests : IDisposable {
    private readonly string root = Directory.CreateTempSubdirectory("xn-generation-boundary-").FullName;

    [Fact]
    public void GeneratorProducesResultAndProgressWithoutConsoleOutput() {
        var template = Path.Combine(root, "template");
        Directory.CreateDirectory(Path.Combine(template, "Seed.UI"));
        File.WriteAllText(Path.Combine(template, "Seed.UI", "Seed.UI.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>");
        var config = Config();
        var events = new List<GenerationProgress>();
        TextWriter originalOut = Console.Out, originalError = Console.Error;
        using var output = new StringWriter();
        using var error = new StringWriter();
        GenerationResult result;
        try {
            Console.SetOut(output);
            Console.SetError(error);
            result = new Generator(template).GenerateProject(config, events.Add);
        }
        finally { Console.SetOut(originalOut); Console.SetError(originalError); }
        Assert.True(result.Success, result.Error?.ToString());
        Assert.Equal(Path.Combine(config.OutputPath, "Demo"), result.OutputRoot);
        Assert.True(File.Exists(Path.Combine(result.GetOutputOrThrow(), "xamlnexus.json")));
        Assert.Equal("", output.ToString());
        Assert.Equal("", error.ToString());
        Assert.Contains(events, e => e.Stage == GenerationStage.CopyModules && e.Completed == e.Total);
        Assert.Contains(events, e => e.Stage == GenerationStage.CreateSolution && e.Completed == e.Total);
        Assert.Equal(new GenerationProgress(GenerationStage.WriteManifest, 1, 1), events.Last());
        Assert.DoesNotContain(typeof(BaseGenerator).Assembly.GetReferencedAssemblies(), a => a.Name == "Spectre.Console");
    }

    [Fact]
    public void FailureRetainsOriginalExceptionAndCleansOnlyOwnedOutput() {
        var failure = new IOException("injected failure");
        var config = Config();
        Directory.CreateDirectory(config.OutputPath);
        var sentinel = Path.Combine(config.OutputPath, "keep.txt");
        File.WriteAllText(sentinel, "keep");
        var result = new Generator(Path.Combine(root, "missing"), failure).GenerateProject(config);
        Assert.False(result.Success);
        Assert.Null(result.OutputRoot);
        Assert.Same(failure, result.Error);
        Assert.Same(failure, Assert.Throws<IOException>(() => result.GetOutputOrThrow()));
        Assert.False(Directory.Exists(Path.Combine(config.OutputPath, "Demo")));
        Assert.Equal("keep", File.ReadAllText(sentinel));
    }

    [Fact]
    public void InvalidConfigurationFailsBeforeOutputOrProgress() {
        var config = Config();
        config.SlnName = "../outside";
        var events = new List<GenerationProgress>();
        var result = new Generator(root).GenerateProject(config, events.Add);
        Assert.IsType<ArgumentException>(result.Error);
        Assert.Empty(events);
        Assert.False(Directory.Exists(config.OutputPath));
    }

    private ProjectConfig Config() => new() { SlnName = "Demo", OutputPath = Path.Combine(root, "output"), Language = "en-US" };
    private sealed class Generator(string template, Exception? failure = null) : BaseGenerator {
        protected override string TemplateRoot => template;
        protected override FrameworkType Framework => FrameworkType.Winui3;
        protected override string GetPresetId() => "winui";
        protected override string GetTemplatePrefix() => "Seed";
        protected override Dictionary<string, string> GetCustomTokens(ProjectConfig config) => new() { ["Seed"] = config.SlnName };
        protected override IEnumerable<string> GetManagedModuleIds() => [];
        protected override IEnumerable<(string Name, string? Folder)> GetProjects() {
            if (failure is not null) throw failure;
            return [("Seed.UI", null)];
        }
    }
    public void Dispose() => Directory.Delete(root, true);
}
