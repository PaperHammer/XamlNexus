using Spectre.Console;
using XamlNexus.Models;
using XamlNexus.Utils;

namespace XamlNexus.Interactors {
    public static class InputHandler {
        public static void ShowLogo() {
            AnsiConsole.Write(new FigletText("XamlNexus").Color(Color.Cyan1));
            AnsiConsole.Write(new Rule("[grey]v1.0.0[/]").RightJustified());
        }

        public static ProjectConfig CollectUserInput() {
            // 语言选择
            var langChoice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Language / 语言")
                    .AddChoices("English", "简体中文"));

            LanguageRegistry.CurrentLanguage = langChoice == "简体中文" ? LanguageType.Chinese : LanguageType.English;

            var config = new ProjectConfig();

            // 框架选择
            config.Framework = AnsiConsole.Prompt(
                new SelectionPrompt<FrameworkType>()
                    .Title(LanguageRegistry.GetI18n(LangKeys.SelectFramework))
                    .AddChoices(Enum.GetValues<FrameworkType>()) // 自动获取所有枚举
                    .UseConverter(type => type switch {         // 优化显示美观度
                        FrameworkType.WinUI3_WPF => "WinUI3(Foreground) + WPF(Background)",
                        _ => type.ToString()
                    }));

            // 托盘选项
            config.NeedTray = AnsiConsole.Confirm(LanguageRegistry.GetI18n(LangKeys.NeedTray), false);

            // 项目名称
            config.ProjectName = AnsiConsole.Ask<string>(LanguageRegistry.GetI18n(LangKeys.ProjectName), "MyXamlNexusApp");

            return config;
        }
    }
}
