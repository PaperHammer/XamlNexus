using XamlNexus.Common.CommandLine;
using XamlNexus.Common.Utils;
using Xunit;

namespace XamlNexus.TemplateTests;

public sealed class CliParserTests {
    [Fact]
    public void Parse_UpgradeResolveFrom_AllowsPreview() {
        var result = CliParser.Parse(["upgrade", "--resolve-from", "resolved", "--dry-run"], Environment.CurrentDirectory);
        Assert.True(result.Success);
        Assert.Equal(Path.Combine(Environment.CurrentDirectory, "resolved"), result.Options!.ResolveFromPath);
        Assert.True(result.Options.DryRun);
    }

    [Fact]
    public void Parse_Slnx_SelectsXmlSolution() {
        var result = CliParser.Parse(["new", "App", "--solution-format", "slnx"], WorkingDirectory);
        Assert.True(result.Success);
        Assert.Equal(SolutionType.Slnx, result.Options!.Project!.SlnType);
    }
    private static readonly string WorkingDirectory = Path.GetFullPath(
        Path.Combine(Path.GetTempPath(), "xamlnexus-cli-tests"));

    [Fact]
    public void Parse_NoArguments_UsesInteractiveMode() {
        CliParseResult result = CliParser.Parse([], WorkingDirectory);

        Assert.True(result.Success);
        Assert.Equal(CliCommand.Interactive, result.Options!.Command);
    }

    [Theory]
    [InlineData("--help", CliCommand.Help)]
    [InlineData("-h", CliCommand.Help)]
    [InlineData("help", CliCommand.Help)]
    [InlineData("--version", CliCommand.Version)]
    [InlineData("version", CliCommand.Version)]
    public void Parse_InformationalCommand_ReturnsExpectedCommand(
        string argument,
        CliCommand expected) {
        CliParseResult result = CliParser.Parse([argument], WorkingDirectory);

        Assert.True(result.Success);
        Assert.Equal(expected, result.Options!.Command);
    }

    [Fact]
    public void Parse_NewWithDefaults_CreatesWinuiConfiguration() {
        CliParseResult result = CliParser.Parse(["new", "sampleApp"], WorkingDirectory);

        Assert.True(result.Success);
        Assert.Equal(CliCommand.New, result.Options!.Command);
        Assert.Equal("SampleApp", result.Options.Project!.SlnName);
        Assert.Equal(FrameworkType.Winui3, result.Options.Project.Framework);
        Assert.Equal("zh-CN", result.Options.Project.Language);
        Assert.Equal(SolutionType.Sln, result.Options.Project.SlnType);
        Assert.Equal(WorkingDirectory, result.Options.Project.OutputPath);
    }

    [Fact]
    public void Parse_NewWithOptions_CreatesHybridConfiguration() {
        CliParseResult result = CliParser.Parse(
            ["new", "HybridApp", "--preset", "winui-wpf", "--output", "projects", "--language", "en"],
            WorkingDirectory);

        Assert.True(result.Success);
        Assert.Equal(FrameworkType.Winui3_Wpf, result.Options!.Project!.Framework);
        Assert.Equal("en-US", result.Options.Project.Language);
        Assert.Equal(Path.Combine(WorkingDirectory, "projects"), result.Options.Project.OutputPath);
    }

    [Fact]
    public void Parse_NewSupportsNamedProjectOption() {
        CliParseResult result = CliParser.Parse(
            ["new", "--name", "NamedApp", "--preset", "hybrid"],
            WorkingDirectory);

        Assert.True(result.Success);
        Assert.Equal("NamedApp", result.Options!.Project!.SlnName);
    }

    [Theory]
    [InlineData("list", CliCommand.List)]
    [InlineData("validate", CliCommand.Validate)]
    public void Parse_ProjectCommand_DefaultsToCurrentDirectory(
        string command,
        CliCommand expected) {
        CliParseResult result = CliParser.Parse([command], WorkingDirectory);

        Assert.True(result.Success);
        Assert.Equal(expected, result.Options!.Command);
        Assert.Equal(WorkingDirectory, result.Options.ProjectPath);
    }

    [Fact]
    public void Parse_ValidateWithExplicitProject_ResolvesRelativePath() {
        CliParseResult result = CliParser.Parse(
            ["validate", "--project", "projects/SampleApp", "--json"],
            WorkingDirectory);

        Assert.True(result.Success);
        Assert.Equal(CliCommand.Validate, result.Options!.Command);
        Assert.True(result.Options.JsonOutput);
        Assert.Equal(
            Path.Combine(WorkingDirectory, "projects", "SampleApp"),
            result.Options.ProjectPath);
    }

    [Fact]
    public void Parse_Recipes_ReturnsCatalogCommand() {
        CliParseResult result = CliParser.Parse(["recipes", "--json"], WorkingDirectory);

        Assert.True(result.Success);
        Assert.Equal(CliCommand.Recipes, result.Options!.Command);
        Assert.True(result.Options.JsonOutput);
    }

    [Fact]
    public void Parse_AddRecipe_ResolvesProjectPath() {
        CliParseResult result = CliParser.Parse(
            ["add", "editorconfig", "--project", "projects/SampleApp"],
            WorkingDirectory);

        Assert.True(result.Success);
        Assert.Equal(CliCommand.Add, result.Options!.Command);
        Assert.Equal("editorconfig", result.Options.RecipeId);
        Assert.Equal(
            Path.Combine(WorkingDirectory, "projects", "SampleApp"),
            result.Options.ProjectPath);
    }

    [Fact]
    public void Parse_RemoveRecipe_ResolvesProjectPath() {
        CliParseResult result = CliParser.Parse(
            ["remove", "sqlite", "--project", "projects/SampleApp"],
            WorkingDirectory);

        Assert.True(result.Success);
        Assert.Equal(CliCommand.Remove, result.Options!.Command);
        Assert.Equal("sqlite", result.Options.RecipeId);
        Assert.Equal(
            Path.Combine(WorkingDirectory, "projects", "SampleApp"),
            result.Options.ProjectPath);
    }

    [Fact]
    public void Parse_UpdateRecipe_ResolvesProjectPath() {
        CliParseResult result = CliParser.Parse(
            ["update", "sqlite", "--project", "projects/SampleApp"],
            WorkingDirectory);

        Assert.True(result.Success);
        Assert.Equal(CliCommand.Update, result.Options!.Command);
        Assert.Equal("sqlite", result.Options.RecipeId);
        Assert.Equal(
            Path.Combine(WorkingDirectory, "projects", "SampleApp"),
            result.Options.ProjectPath);
    }

    [Fact]
    public void Parse_RecipeChangeFlags_EnableDryRunAndJson() {
        CliParseResult result = CliParser.Parse(
            ["remove", "sqlite", "--dry-run", "--json"],
            WorkingDirectory);

        Assert.True(result.Success);
        Assert.Equal(CliCommand.Remove, result.Options!.Command);
        Assert.True(result.Options.DryRun);
        Assert.True(result.Options.JsonOutput);
    }

    [Fact]
    public void Parse_DoctorWithJson_ResolvesProjectPath() {
        CliParseResult result = CliParser.Parse(
            ["doctor", "--project", "projects/SampleApp", "--json"],
            WorkingDirectory);

        Assert.True(result.Success);
        Assert.Equal(CliCommand.Doctor, result.Options!.Command);
        Assert.True(result.Options.JsonOutput);
        Assert.Equal(
            Path.Combine(WorkingDirectory, "projects", "SampleApp"),
            result.Options.ProjectPath);
    }

    [Fact]
    public void Parse_Upgrade_ResolvesProjectPath() {
        CliParseResult result = CliParser.Parse(
            ["upgrade", "--project", "projects/SampleApp", "--dry-run", "--json"],
            WorkingDirectory);

        Assert.True(result.Success);
        Assert.Equal(CliCommand.Upgrade, result.Options!.Command);
        Assert.True(result.Options.DryRun);
        Assert.True(result.Options.JsonOutput);
        Assert.Equal(
            Path.Combine(WorkingDirectory, "projects", "SampleApp"),
            result.Options.ProjectPath);
    }

    [Fact]
    public void Parse_UpgradeConflictOutput_ResolvesRelativeDirectory() {
        CliParseResult result = CliParser.Parse(
            ["upgrade", "--conflict-output", "artifacts/conflicts", "--json"],
            WorkingDirectory);

        Assert.True(result.Success);
        Assert.Equal(
            Path.Combine(WorkingDirectory, "artifacts", "conflicts"),
            result.Options!.ConflictOutputPath);
    }

    [Theory]
    [InlineData("serve", "Unknown command")]
    [InlineData("new", "project name is required")]
    [InlineData("new Bad-Name", "project name must start")]
    [InlineData("new App --unknown value", "Unknown option")]
    [InlineData("new App --preset", "requires a value")]
    [InlineData("new App --preset avalonia", "Unknown preset")]
    [InlineData("new App --language fr-FR", "Unsupported language")]
    [InlineData("new App --solution-format invalid", "Unsupported solution format")]
    [InlineData("list one two", "only be specified once")]
    [InlineData("validate --project", "requires a value")]
    [InlineData("list --json --json", "only be specified once")]
    [InlineData("validate --json --json", "only be specified once")]
    [InlineData("add", "Recipe id is required")]
    [InlineData("add one two", "Only one Recipe id")]
    [InlineData("remove", "Recipe id is required")]
    [InlineData("remove one two", "Only one Recipe id")]
    [InlineData("update", "Recipe id is required")]
    [InlineData("update one two", "Only one Recipe id")]
    [InlineData("add one --dry-run --dry-run", "only be specified once")]
    [InlineData("remove one --json --json", "only be specified once")]
    [InlineData("upgrade --dry-run --dry-run", "only be specified once")]
    [InlineData("upgrade --json --json", "only be specified once")]
    [InlineData("upgrade --conflict-output one --conflict-output two", "only be specified once")]
    [InlineData("upgrade --dry-run --conflict-output conflicts", "cannot be combined")]
    [InlineData("upgrade --resolve-from one --conflict-output two", "cannot be combined")]
    [InlineData("upgrade --resolve-from one --resolve-from two", "only be specified once")]
    [InlineData("doctor --json --json", "only be specified once")]
    [InlineData("doctor --unknown", "Unknown option")]
    [InlineData("recipes unexpected", "does not accept")]
    [InlineData("recipes --json --json", "only be specified once")]
    public void Parse_InvalidArguments_ReturnsUsageError(string commandLine, string expectedError) {
        CliParseResult result = CliParser.Parse(commandLine.Split(' '), WorkingDirectory);

        Assert.False(result.Success);
        Assert.Contains(expectedError, result.Error!, StringComparison.OrdinalIgnoreCase);
    }
}
