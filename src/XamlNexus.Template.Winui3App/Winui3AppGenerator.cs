using System.Reflection;
using Spectre.Console;
using XamlNexus.Common.Generators;
using XamlNexus.Common.Utils;
using XamlNexus.Models;
using XamlNexus.Models.Attributes;

namespace XamlNexus.Template.Winui3App {
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
                .Start("Creating project...", ctx => {
                    // 初始化项目
                    ctx.Status("Setting up project structure...");
                    if (!ShellExecutor.Run(
                            "dotnet",
                            $"new winui -n {config.ProjectName} -o \"{projectDir}\" --force",
                            config.OutputPath)) {
                        throw new Exception(
                            "Project template not found.\n\n" +
                            "Install required templates:\n" +
                            "  dotnet workload install windowsappsdk");
                    }

                    // 应用项目配置
                    ctx.Status("Applying project configuration...");
                    WriteFile(csprojPath, GetResource("Csproj.txt"), tokens);

                    // 配置应用服务
                    ctx.Status("Configuring application services...");
                    WriteFile(Path.Combine(projectDir, "Nlog.config"),
                        GetResource("NlogConfig.txt"), tokens);

                    WriteFile(Path.Combine(projectDir, "App.xaml.cs"),
                        GetResource("AppXamlCs.txt"), tokens);

                    // 解决方案
                    ctx.Status("Finalizing workspace...");
                    string slnPath = Path.Combine(config.OutputPath, $"{config.ProjectName}.sln");

                    if (!File.Exists(slnPath)) {
                        ShellExecutor.Run(
                            "dotnet",
                            $"new sln -n {config.ProjectName}",
                            config.OutputPath);
                    }

                    ShellExecutor.Run(
                        "dotnet",
                        $"sln \"{slnPath}\" add \"{csprojPath}\"",
                        config.OutputPath);
                });

            AnsiConsole.WriteLine();

            var grid = new Grid();
            grid.AddColumn(new GridColumn().NoWrap());
            grid.AddColumn();

            grid.AddRow("[bold green]✔ Project created successfully[/]", "");
            grid.AddEmptyRow();
            grid.AddRow("[grey]Name:[/]", $"[white]{config.ProjectName}[/]");
            grid.AddRow("[grey]Location:[/]", $"[white]{projectDir}[/]");
            grid.AddRow("[grey]Configuration:[/]", "[white]WinUI3 Desktop[/]");

            AnsiConsole.Write(grid);

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]Next steps:[/]");
            AnsiConsole.MarkupLine($"  [cyan]cd[/] {config.ProjectName}");
            AnsiConsole.MarkupLine("  [cyan]dotnet build[/]");
            AnsiConsole.WriteLine();
        }

        private string GetResource(string fileName) {
            var assembly = Assembly.GetExecutingAssembly();
            string resourcePath = $"XamlNexus.Template.Winui3App.Templates.{fileName}";

            using var stream = assembly.GetManifestResourceStream(resourcePath);
            if (stream == null)
                throw new Exception($"Unable to load embedded resource: {fileName}");

            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}