using Spectre.Console;
using XamlNexus.Models;
using XamlNexus.Utils;

namespace XamlNexus.Interactors {
    public static class InputHandler {
        public static LocaleResources CurrentLocale { get; set; } = LanguageRegistry.English;

        public static void ShowLogo() {
            AnsiConsole.Write(new FigletText("XamlNexus").Color(Color.Cyan1));
            AnsiConsole.Write(new Rule("[grey]v1.0.0[/]").RightJustified());
        }

        public static ProjectConfig CollectUserInput() {
            var langChoice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Language / 语言")
                    .AddChoices("English", "简体中文"));

            CurrentLocale = langChoice == "简体中文" ? LanguageRegistry.Chinese : LanguageRegistry.English;

            // 后续使用 CurrentLocale
            var config = new ProjectConfig();
            config.Framework = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title(CurrentLocale.SelectFramework)
                    .AddChoices("WPF", "Avalonia"));

            return config;
        }
    }
}
