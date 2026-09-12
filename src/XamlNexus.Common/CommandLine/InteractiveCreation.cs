using XamlNexus.Common.Utils;
using Spectre.Console;
using XamlNexus.Common.Generators;
using XamlNexus.Common.Projects;
using XamlNexus.Common.Recipes;

namespace XamlNexus.Common.CommandLine;

public static class InteractiveCreation {
    public static IReadOnlyList<IXamlNexusRecipe> GetChoices(ProjectConfig config, IGenerator generator, IXamlNexusRecipeCatalog catalog) {
        string preset = config.Framework == Utils.FrameworkType.Winui3_Wpf ? "hybrid" : "winui";
        var included = generator.GetIncludedModuleIds(config.Profile).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return catalog.Recipes.Where(recipe => !included.Contains(recipe.Descriptor.Id)
            && recipe.Descriptor.SupportedPresets.Contains(preset, StringComparer.OrdinalIgnoreCase))
            .OrderBy(recipe => recipe.Descriptor.Id, StringComparer.Ordinal).ToArray();
    }

    public static CompositionRequest Compose(ProjectConfig config, IGenerator generator, IXamlNexusRecipeCatalog catalog) {
        LanguageRegistry.CurrentLanguage = config.Language == "zh-CN" ? LanguageType.Chinese : LanguageType.English;
        config.Profile = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title(LanguageRegistry.GetText("Wizard_ChooseProfile"))
            .AddChoices("standard", "basic")
            .UseConverter(profile => profile == "standard"
                ? LanguageRegistry.GetText("Wizard_StandardProfile")
                : LanguageRegistry.GetText("Wizard_BasicProfile")));
        string preset = config.Framework == Utils.FrameworkType.Winui3_Wpf ? "hybrid" : "winui";
        var choices = GetChoices(config, generator, catalog);
        while (true) {
            var selected = choices.Count == 0 ? [] : AnsiConsole.Prompt(new MultiSelectionPrompt<IXamlNexusRecipe>()
                .Title(LanguageRegistry.GetText("Wizard_ChooseCapabilities"))
                .NotRequired()
                .InstructionsText(LanguageRegistry.GetText("Wizard_SelectionInstructions"))
                .AddChoices(choices)
                .UseConverter(recipe => Markup.Escape($"{recipe.Descriptor.Id} — {recipe.Descriptor.DisplayName}")));
            IReadOnlyList<IXamlNexusRecipe> resolved;
            try {
                resolved = CompositionPlanner.ResolveForCreation(preset, config.Profile, selected.Select(recipe => recipe.Descriptor.Id),
                    catalog, generator.GetIncludedModuleIds(config.Profile));
            }
            catch (InvalidOperationException exception) {
                AnsiConsole.MarkupLine($"[red]{Markup.Escape(exception.Message)}[/]");
                AnsiConsole.WriteLine(LanguageRegistry.GetText("Wizard_AdjustSelection"));
                continue;
            }
            var table = new Table().AddColumn(LanguageRegistry.GetText("Wizard_ProjectConfiguration")).AddColumn(LanguageRegistry.GetText("Wizard_Value"));
            table.AddRow(LanguageRegistry.GetText("Wizard_Name"), Markup.Escape(config.SlnName));
            table.AddRow(LanguageRegistry.GetText("Wizard_OutputDirectory"), Markup.Escape(config.OutputPath));
            table.AddRow(LanguageRegistry.GetText("Wizard_Architecture"), preset);
            table.AddRow(LanguageRegistry.GetText("Wizard_Profile"), config.Profile);
            table.AddRow(LanguageRegistry.GetText("Wizard_CapabilitiesWithDependencies"),
                resolved.Count == 0 ? LanguageRegistry.GetText("Wizard_None") : Markup.Escape(string.Join(", ", resolved.Select(recipe => recipe.Descriptor.Id))));
            AnsiConsole.Write(table);
            return new(config, config.Profile, selected.Select(recipe => recipe.Descriptor.Id).ToArray());
        }
    }
}
