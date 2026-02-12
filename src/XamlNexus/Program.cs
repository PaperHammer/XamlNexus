using Spectre.Console;
using XamlNexus.Interactors;
using XamlNexus.Utils;

namespace XamlNexus {
    internal class Program {
        static void Main(string[] args) {
            InputHandler.ShowLogo();

            try {
                // 获取配置
                var config = InputHandler.CollectUserInput();

                // 获取对应的生成器
                var generator = GeneratorFactory.GetGenerator(config.Framework);

                // 执行并展示进度
                AnsiConsole.Status()
                    .Spinner(Spinner.Known.Aesthetic)
                    .Start(LanguageRegistry.GetI18n(LangKeys.Generating), ctx => {
                        generator.Generate(config);
                    });

                AnsiConsole.MarkupLine($"\n[bold green]{LanguageRegistry.GetI18n(LangKeys.Success)}");
                AnsiConsole.MarkupLine($"[grey]{LanguageRegistry.GetI18n(LangKeys.Route)}: {config.OutputPath}[/]");
            }
            catch (Exception ex) {
                AnsiConsole.WriteException(ex);
            }
        }
    }
}
