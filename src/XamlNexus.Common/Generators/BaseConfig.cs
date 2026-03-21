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
            // 语言选择
            var langChoice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Language / 语言")
                    .AddChoices("English", "简体中文"));

            LanguageRegistry.CurrentLanguage = langChoice == "简体中文" ? LanguageType.Chinese : LanguageType.English;

            var config = new ProjectConfig {
                // 框架选择
                Framework = AnsiConsole.Prompt(
                    new SelectionPrompt<FrameworkType>()
                        .Title(LanguageRegistry.GetI18n(LangKeys.SelectFramework))
                        .AddChoices(Enum.GetValues<FrameworkType>()) // 自动获取所有枚举
                        .UseConverter(type => type switch {
                            FrameworkType.Winui3_Wpf => "Winui3(Foreground) + Wpf(Background)".EscapeMarkup(),
                            _ => type.ToString()
                        })),

                // 解决方案类型
                SlnType = AnsiConsole.Prompt(
                    new SelectionPrompt<SolutionType>()
                        .Title(LanguageRegistry.GetI18n(LangKeys.SelectSolutionFormat))
                        .AddChoices(Enum.GetValues<SolutionType>())
                        .UseConverter(format => format switch {
                            SolutionType.Sln => "Standard Solution (.sln)".EscapeMarkup(),
                            SolutionType.Slnx => "Modern XML Solution (.slnx) [VS 2026+]".EscapeMarkup(),
                            _ => format.ToString()
                        })),

                // 项目名称
                ProjectName = AnsiConsole.Ask<string>(LanguageRegistry.GetI18n(LangKeys.ProjectName), "MyXamlNexusApp")
            };

            return config;
        }

        public abstract ProjectConfig? ExtraComposeConfig();
    }

    public partial class ProjectConfig {
        public string ProjectName { get; set; } = "MyXamlNexusApp";
        //public string Language { get; set; } = "C#";
        public FrameworkType Framework { get; set; }
        public SolutionType SlnType { get; set; }
        public bool NeedTray { get; set; }
        public string OutputPath {
            get {
#if DEBUG
                DebugUtil.RestoreOutputDir();
                return DebugUtil.OutputPath;
#else
        return Path.Combine(Environment.CurrentDirectory, ProjectName);
#endif
            }
        }

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
    }
}
