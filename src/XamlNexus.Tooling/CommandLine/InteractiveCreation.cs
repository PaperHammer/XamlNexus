using XamlNexus.Common.Utils;
using Spectre.Console;
using XamlNexus.Common.Generators;
using XamlNexus.Common.Projects;
using XamlNexus.Common.Recipes;

namespace XamlNexus.Tooling.CommandLine;

public static class InteractiveCreation {
    public static IReadOnlyList<IXamlNexusRecipe> GetChoices(ProjectConfig config, IGenerator generator, IXamlNexusRecipeCatalog catalog) {
        string preset = config.Framework == FrameworkType.Winui3_Wpf ? "hybrid" : "winui";
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
        string preset = config.Framework == FrameworkType.Winui3_Wpf ? "hybrid" : "winui";
        var choices = GetChoices(config, generator, catalog);
        while (true) {
            var selected = SelectCapabilities(AnsiConsole.Console, choices);
            IReadOnlyList<IXamlNexusRecipe> resolved;
            try {
                resolved = CompositionPlanner.ResolveForCreation(preset, config.Profile, selected.Select(recipe => recipe.Descriptor.Id),
                    catalog, generator.GetIncludedModuleIds(config.Profile));
            }
            catch (InvalidOperationException exception) {
                AnsiConsole.MarkupLine($"[red]{Markup.Escape(LanguageRegistry.GetExceptionMessage(exception))}[/]");
                AnsiConsole.WriteLine(LanguageRegistry.GetText("Wizard_AdjustSelection"));
                continue;
            }
            var table = new Table().AddColumn(LanguageRegistry.GetText("Wizard_ProjectConfiguration")).AddColumn(LanguageRegistry.GetText("Wizard_Value"));
            table.AddRow(LanguageRegistry.GetText("Wizard_Name"), Markup.Escape(config.SlnName));
            table.AddRow(LanguageRegistry.GetText("Wizard_OutputDirectory"), Markup.Escape(config.OutputPath));
            table.AddRow(LanguageRegistry.GetText("Wizard_Architecture"), preset);
            table.AddRow(LanguageRegistry.GetText("Wizard_Profile"), config.Profile);
            table.AddRow(LanguageRegistry.GetText("Wizard_CapabilitiesWithDependencies"),
                resolved.Count == 0 ? LanguageRegistry.GetText("Wizard_None") : Markup.Escape(string.Join(", ", resolved.Select(recipe => RecipeCommandNames.ToCommandName(recipe.Descriptor.Id)))));
            AnsiConsole.Write(table);
            return new(config, config.Profile, selected.Select(recipe => recipe.Descriptor.Id).ToArray());
        }
    }

    internal static IReadOnlyList<IXamlNexusRecipe> SelectCapabilities(
        IAnsiConsole console, IReadOnlyList<IXamlNexusRecipe> choices) {
        if (choices.Count == 0) return [];
        var selected = new bool[choices.Count];
        int cursor = 0;

        Rows Render() {
            var rows = new List<Spectre.Console.Rendering.IRenderable> {
                new Markup(LanguageRegistry.GetText("Wizard_ChooseCapabilities")), Text.Empty
            };
            for (int index = 0; index < choices.Count; index++) {
                var recipe = choices[index].Descriptor;
                string marker = selected[index] ? "[●]" : "[ ]";
                string label = $"{(index == cursor ? ">" : " ")} {marker} {RecipeCommandNames.ToCommandName(recipe.Id)} — {recipe.DisplayName}";
                rows.Add(new Text(label, index == cursor ? new Style(Color.Blue) : Style.Plain));
            }
            rows.Add(Text.Empty);
            rows.Add(new Markup(LanguageRegistry.GetText("Wizard_SelectionInstructions")));
            return new Rows(rows);
        }

        console.Live(Render()).AutoClear(true).Start(context => {
            while (true) {
                // Live displays do not refresh automatically while ReadKey blocks.
                context.Refresh();
                var key = console.Input.ReadKey(intercept: true);
                if (key is null) throw new InvalidOperationException("Interactive input ended before selection was confirmed.");
                switch (key.Value.Key) {
                    case ConsoleKey.Enter:
                        return;
                    case ConsoleKey.UpArrow:
                        cursor = Math.Max(0, cursor - 1);
                        break;
                    case ConsoleKey.DownArrow:
                        cursor = Math.Min(choices.Count - 1, cursor + 1);
                        break;
                    case ConsoleKey.Spacebar:
                        selected[cursor] = !selected[cursor];
                        break;
                }
                context.UpdateTarget(Render());
            }
        });
        return choices.Where((_, index) => selected[index]).ToArray();
    }
}
