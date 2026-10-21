using XamlNexus.Tooling.CommandLine;
using XamlNexus.Common.Generators;
using XamlNexus.Common.Projects;
using XamlNexus.Common.Utils;
using XamlNexus.Generator.Winui3App;
using XamlNexus.Generator.Winui3_Wpf_App;
using XamlNexus.Recipes.BuiltIn;
using Xunit;

namespace XamlNexus.TemplateTests;

public sealed class InteractiveCreationTests {
    [Theory]
    [InlineData(false, "standard", "app-update,editorconfig,sqlite,system-tray")]
    [InlineData(false, "basic", "app-update,editorconfig,settings,sqlite,system-tray")]
    [InlineData(true, "standard", "editorconfig,sqlite")]
    [InlineData(true, "basic", "editorconfig,settings,sqlite")]
    public void ChoicesExcludeIncludedAndUnsupportedCapabilities(bool hybrid, string profile, string expected) {
        var config = new ProjectConfig { Framework = hybrid ? FrameworkType.Winui3_Wpf : FrameworkType.Winui3, Profile = profile };
        IGenerator generator = hybrid ? new Winui3_WpfGenerator() : new Winui3Generator();
        var choices = InteractiveCreation.GetChoices(config, generator, BuiltInRecipeCatalog.Create());
        Assert.Equal(expected.Split(','), choices.Select(recipe => recipe.Descriptor.Id));
    }

    [Fact]
    public void BasicUpdateSelectionResolvesSettingsForSummaryAndGeneration() {
        var config = new ProjectConfig { Framework = FrameworkType.Winui3, Profile = "basic" };
        var generator = new Winui3Generator();
        var catalog = BuiltInRecipeCatalog.Create();
        var selected = InteractiveCreation.GetChoices(config, generator, catalog).Single(recipe => recipe.Descriptor.Id == "app-update");
        var resolved = CompositionPlanner.ResolveForCreation("winui", config.Profile, [selected.Descriptor.Id], catalog, generator.GetIncludedModuleIds(config.Profile));
        Assert.Equal(new[] { "settings", "app-update" }, resolved.Select(recipe => recipe.Descriptor.Id));
    }
}
