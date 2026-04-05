using System.Reflection;
using Spectre.Console;
using XamlNexus.Common.Utils;

namespace XamlNexus.Common.Generators {
    public abstract class BaseConfig : IConfig {
        public static void ShowLogo() {
            AnsiConsole.Write(new FigletText("XamlNexus").Color(Color.Cyan1));
            AnsiConsole.Write(new Rule("[grey]v1.0.0[/]").RightJustified());
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

                SlnType = AnsiConsole.Prompt(
                    new SelectionPrompt<SolutionType>()
                        .Title(LanguageRegistry.GetI18n(LangKeys.SelectSolutionFormat))
                        .AddChoices(Enum.GetValues<SolutionType>())
                        .UseConverter(format => format switch {
                            SolutionType.Sln => $"{LanguageRegistry.GetI18n(LangKeys.Text_Standard_Solution)} (.sln) [.NET 8]".EscapeMarkup(),
                            //SolutionType.Slnx => $"{LanguageRegistry.GetI18n(LangKeys.Text_Modren_XML_Solution)} (.slnx) [.NET 10 with VS 2026+]".EscapeMarkup(),
                            _ => format.ToString()
                        })),

                SlnName = AnsiConsole.Prompt(
                    new TextPrompt<string>(LanguageRegistry.GetI18n(LangKeys.ProjectName))
                        .DefaultValue(ProjectConfig.GetDefaultProjectName())
                        .PromptStyle("gray")
                        .Validate(name => {
                            if (string.IsNullOrWhiteSpace(name))
                                return ValidationResult.Error($"[red]{LanguageRegistry.GetI18n(LangKeys.Text_ProjectNameEmpty)}[/]");

                            if (!name.All(c => char.IsLetterOrDigit(c) || c == '_'))
                                return ValidationResult.Error($"[red]{LanguageRegistry.GetI18n(LangKeys.Text_ProjectNameInvalidChars)}[/]");

                            if (!(char.IsLetter(name[0]) || name[0] == '_'))
                                return ValidationResult.Error($"[red]{LanguageRegistry.GetI18n(LangKeys.Text_ProjectNameStartChar)}[/]");

                            return ValidationResult.Success();
                        })),

                OutputPath = AnsiConsole.Prompt(
                    new TextPrompt<string>(LanguageRegistry.GetI18n(LangKeys.OutputPath))
                        .DefaultValue(ProjectConfig.GetDefaultOutputPath())
                        .PromptStyle("gray")
                        .Validate(path => {
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
                AnsiConsole.MarkupLine($"[yellow]Notice:[/] {LanguageRegistry.GetI18n(LangKeys.Text_ProjectNameCapitalized)} - [cyan]{config.SlnName}[/]");
            }

            return config;
        }

        public abstract ProjectConfig? ExtraComposeConfig();
    }

    public partial class ProjectConfig {
        public string SlnName { get; set; } = GetDefaultProjectName();
        public string Language { get; set; } = "zh-CN";
        public FrameworkType Framework { get; set; }
        public SolutionType SlnType { get; set; }
        public bool NeedTray { get; set; }
        public string OutputPath { get; set; } = GetDefaultOutputPath();

        public void Merge(ProjectConfig? extra) {
            if (extra == null) return;

            var properties = typeof(ProjectConfig).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties) {
                if (!prop.CanWrite) continue;

                var extraValue = prop.GetValue(extra);
                var defaultValue = GetDefaultValue(prop.PropertyType);

                if (extraValue != null && !extraValue.Equals(defaultValue)) {
                    prop.SetValue(this, extraValue);
                }
            }
        }

        private static object? GetDefaultValue(Type type) {
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }

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
