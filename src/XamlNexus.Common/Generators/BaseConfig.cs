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
                    .Title("Language / 语言")
                    .AddChoices("English", "简体中文"));

            LanguageRegistry.CurrentLanguage = langChoice == "简体中文" ? LanguageType.Chinese : LanguageType.English;

            var config = new ProjectConfig {
                Framework = AnsiConsole.Prompt(
                    new SelectionPrompt<FrameworkType>()
                        .Title(LanguageRegistry.GetI18n(LangKeys.SelectFramework))
                        .AddChoices(Enum.GetValues<FrameworkType>())
                        .UseConverter(type => type switch {
                            FrameworkType.Winui3_Wpf => "Winui3(Foreground) + Wpf(Background)".EscapeMarkup(),
                            _ => type.ToString()
                        })),

                SlnType = AnsiConsole.Prompt(
                    new SelectionPrompt<SolutionType>()
                        .Title(LanguageRegistry.GetI18n(LangKeys.SelectSolutionFormat))
                        .AddChoices(Enum.GetValues<SolutionType>())
                        .UseConverter(format => format switch {
                            SolutionType.Sln => "Standard Solution (.sln) [.NET 8]".EscapeMarkup(),
                            //SolutionType.Slnx => "Modern XML Solution (.slnx) [.NET 10 with VS 2026+]".EscapeMarkup(),
                            _ => format.ToString()
                        })),

                SlnName = AnsiConsole.Prompt(
                    new TextPrompt<string>(LanguageRegistry.GetI18n(LangKeys.ProjectName))
                        .DefaultValue(ProjectConfig.GetDefaultProjectName())
                        .PromptStyle("gray")
                        .Validate(name => {
                            if (string.IsNullOrWhiteSpace(name))
                                return ValidationResult.Error("[red]Project name cannot be empty[/]");

                            // 是否包含非法路径字符或特殊符号
                            if (name.Any(c => Path.GetInvalidFileNameChars().Contains(c) || char.IsWhiteSpace(c)))
                                return ValidationResult.Error("[red]Project name contains invalid characters or spaces[/]");

                            return ValidationResult.Success();
                        })),

                OutputPath = AnsiConsole.Prompt(
                    new TextPrompt<string>(LanguageRegistry.GetI18n(LangKeys.OutputPath))
                        .DefaultValue(ProjectConfig.GetDefaultOutputPath())
                        .PromptStyle("gray")
                        .Validate(path => {
                            if (string.IsNullOrWhiteSpace(path))
                                return ValidationResult.Error("[red]Path cannot be empty[/]");

                            // 检查是否包含非法路径字符
                            if (path.Any(c => Path.GetInvalidPathChars().Contains(c)))
                                return ValidationResult.Error("[red]Path contains invalid characters[/]");

                            // 校验是否是绝对路径
                            if (!Path.IsPathRooted(path))
                                return ValidationResult.Error("[red]Please enter a full absolute path[/]");

                            return ValidationResult.Success();
                        })),
            };

            return config;
        }

        public abstract ProjectConfig? ExtraComposeConfig();
    }

    public partial class ProjectConfig {
        public string SlnName { get; set; } = GetDefaultProjectName();
        //public string Language { get; set; } = "C#";
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
        public static string GetDefaultOutputPath() => DebugUtil.OutputPath;
    }
}
