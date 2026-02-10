using Spectre.Console;
using XamlNexus.Generators;
using XamlNexus.Generators.Interfaces;
using XamlNexus.Interactors;

namespace XamlNexus {
    internal class Program {
        static void Main(string[] args) {
            InputHandler.ShowLogo();

            var config = InputHandler.CollectUserInput();

            IGenerator generator = config.Framework switch {
                "WPF" => new WpfGenerator(),
                _ => throw new NotImplementedException()
            };

            AnsiConsole.Status().Start("执行中...", ctx => {
                generator.Generate(config);
            });

            AnsiConsole.MarkupLine("[bold green]项目已成功初始化！[/]");
        }
    }
}
