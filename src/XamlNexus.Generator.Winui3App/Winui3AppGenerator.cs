using System.Reflection;
using Spectre.Console;
using XamlNexus.Common.Generators;
using XamlNexus.Common.Utils;
using XamlNexus.Models.Attributes;
using static XamlNexus.Common.Utils.LanguageRegistry;

namespace XamlNexus.Generator.Winui3App {
    [Generator(FrameworkType.Winui3)]
    public class Winui3AppGenerator : BaseGenerator {
        public override void Generate(ProjectConfig config) {
            string projectDir = Path.Combine(config.OutputPath, config.ProjectName);

            var tokens = new Dictionary<string, string> {
                { "PROJECT_NAME", config.ProjectName },
                { "SAFE_NAMESPACE", config.ProjectName.Replace(" ", "_") }
            };

            string csprojPath = Path.Combine(projectDir, $"{config.ProjectName}.csproj");

            AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("cyan"))
                .Start(GetI18n(LangKeys.Generating), ctx => {

                    // 初始化项目
                    ctx.Status(GetI18n(LangKeys.SetupStructure));
                    if (!ShellExecutor.Run("dotnet", $"new winui -n {config.ProjectName} -o \"{projectDir}\" --force", config.OutputPath)) {
                        throw new Exception(GetI18n(LangKeys.TemplateNotFound));
                    }

                    // 应用项目配置
                    ctx.Status(GetI18n(LangKeys.ApplyingConfig));
                    WriteFile(csprojPath, GetResource("csproj.txt"), tokens);

                    // 配置应用服务
                    ctx.Status(GetI18n(LangKeys.ConfigServices));
                    WriteFile(Path.Combine(projectDir, "Nlog.config"), GetResource("NlogConfig.txt"), tokens);
                    WriteFile(Path.Combine(projectDir, "App.xaml.cs"), GetResource("AppXamlCs.txt"), tokens);

                    // 解决方案
                    ctx.Status(GetI18n(LangKeys.Finalizing));
                    string slnPath = Path.Combine(config.OutputPath, $"{config.ProjectName}.sln");

                    if (!File.Exists(slnPath)) {
                        ShellExecutor.Run("dotnet", $"new sln -n {config.ProjectName}", config.OutputPath);
                    }
                    ShellExecutor.Run("dotnet", $"sln \"{slnPath}\" add \"{csprojPath}\"", config.OutputPath);
                });

            AnsiConsole.WriteLine();

            var grid = new Grid();
            grid.AddColumn(new GridColumn().NoWrap());
            grid.AddColumn();

            grid.AddRow($"[bold green]✔ {GetI18n(LangKeys.Success)}[/]", "");
            grid.AddEmptyRow();
            grid.AddRow($"[grey]{GetI18n(LangKeys.ProjectName)}[/]", $"[white]{config.ProjectName}[/]");
            grid.AddRow($"[grey]{GetI18n(LangKeys.Location)}:[/]", $"[white]{projectDir}[/]");
            grid.AddRow($"[grey]Framework:[/]", "[white]WinUI3 Desktop[/]");

            AnsiConsole.Write(grid);

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[grey]{GetI18n(LangKeys.NextSteps)}[/]");
            AnsiConsole.MarkupLine($"  [cyan]cd[/] {config.ProjectName}");
            AnsiConsole.MarkupLine("  [cyan]dotnet build[/]");
            AnsiConsole.WriteLine();
        }

        private static string GetResource(string fileName) {
            var assembly = Assembly.GetExecutingAssembly();
            string resourcePath = $"XamlNexus.Template.Winui3App.Templates.{fileName}";

            using var stream = assembly.GetManifestResourceStream(resourcePath) ?? throw new Exception($"Resource missing: {fileName}");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
