using Spectre.Console;
using XamlNexus.Common.Utils;
using XamlNexus.Common.CommandLine;

namespace XamlNexus.Common.Generators {
    public static class BaseConfig {
        public static void ShowLogo(string version) {
            AnsiConsole.Write(new FigletText("XamlNexus").Color(Color.Cyan1));
            AnsiConsole.Write(new Rule($"[grey]v{version}[/]").RightJustified());
        }

        public static ProjectConfig BaseComposeConfig() {
            var langChoice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("语言 / Language")
                    .AddChoices("简体中文", "English"));

            LanguageRegistry.CurrentLanguage = langChoice == "简体中文" ? LanguageType.Chinese : LanguageType.English;

            var config = new ProjectConfig {
                Language = LanguageRegistry.CurrentLanguage == LanguageType.Chinese ? "zh-CN" : "en-US",

                Framework = AnsiConsole.Prompt(
                    new SelectionPrompt<FrameworkType>()
                        .Title(LanguageRegistry.GetI18n(LangKeys.SelectFramework))
                        .AddChoices(Enum.GetValues<FrameworkType>())
                        .UseConverter(type => type switch {
                            FrameworkType.Winui3_Wpf => $"Winui3({LanguageRegistry.GetI18n(LangKeys.Text_Frontend)}) + Wpf({LanguageRegistry.GetI18n(LangKeys.Text_Backend)})".EscapeMarkup(),
                            _ => type.ToString()
                        })),

                SlnType = AnsiConsole.Prompt(new SelectionPrompt<SolutionType>()
                    .Title(LanguageRegistry.GetI18n("SelectSolutionFormat"))
                    .AddChoices(SolutionType.Sln, SolutionType.Slnx)),

                SlnName = AnsiConsole.Prompt(
                    new WizardTextPrompt(LanguageRegistry.GetI18n(LangKeys.ProjectName),
                        ProjectConfig.GetDefaultProjectName(), name => {
                            if (string.IsNullOrWhiteSpace(name))
                                return ValidationResult.Error($"[red]{LanguageRegistry.GetI18n(LangKeys.Text_ProjectNameEmpty)}[/]");

                            if (!name.All(c => char.IsLetterOrDigit(c) || c == '_'))
                                return ValidationResult.Error($"[red]{LanguageRegistry.GetI18n(LangKeys.Text_ProjectNameInvalidChars)}[/]");

                            if (!(char.IsLetter(name[0]) || name[0] == '_'))
                                return ValidationResult.Error($"[red]{LanguageRegistry.GetI18n(LangKeys.Text_ProjectNameStartChar)}[/]");

                            return ValidationResult.Success();
                        })),

                OutputPath = AnsiConsole.Prompt(
                    new WizardTextPrompt(LanguageRegistry.GetI18n(LangKeys.OutputPath),
                        ProjectConfig.GetDefaultOutputPath(), path => {
                            if (string.IsNullOrWhiteSpace(path))
                                return ValidationResult.Error($"[red]{LanguageRegistry.GetI18n(LangKeys.Text_PathEmpty)}[/]");

                            if (path.Any(c => Path.GetInvalidPathChars().Contains(c)))
                                return ValidationResult.Error($"[red]{LanguageRegistry.GetI18n(LangKeys.Text_PathInvalidChars)}[/]");

                            if (!Path.IsPathRooted(path))
                                return ValidationResult.Error($"[red]{LanguageRegistry.GetI18n(LangKeys.Text_PathNotAbsolute)}[/]");

                            return ValidationResult.Success();
                        })),
            };

            if (char.IsLower(config.SlnName[0])) {
                config.SlnName = char.ToUpper(config.SlnName[0]) + config.SlnName.Substring(1);
                AnsiConsole.MarkupLine($"[yellow]{LanguageRegistry.GetI18n(LangKeys.Text_Notice)}:[/] {LanguageRegistry.GetI18n(LangKeys.Text_ProjectNameCapitalized)} - [cyan]{config.SlnName}[/]");
            }

            return config;
        }

    }

    public partial class ProjectConfig {
        public string Profile { get; set; } = "standard";
        public string SlnName { get; set; } = GetDefaultProjectName();
        public string Language { get; set; } = "zh-CN";
        public FrameworkType Framework { get; set; }
        public SolutionType SlnType { get; set; }
        public string OutputPath { get; set; } = GetDefaultOutputPath();

        public static string GetDefaultProjectName() => "MyXamlNexusApp";
        public static string GetDefaultOutputPath() {
#if DEBUG
            return Path.Combine(Environment.CurrentDirectory, "debug");
#else
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
#endif
        }
    }
}
