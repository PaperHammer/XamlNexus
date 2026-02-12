using System.Reflection;
using Spectre.Console;
using XamlNexus.Common.Generators;
using XamlNexus.Models;
using XamlNexus.Models.Attributes;

namespace XamlNexus.Template.Winui3App {
    [Generator(FrameworkType.Winui3)]
    public class Winui3AppGenerator : BaseGenerator {
        public override void Generate(ProjectConfig config) {
            var tokens = new Dictionary<string, string>
             {
                { "PROJECT_NAME", config.ProjectName },
                { "SAFE_NAMESPACE", config.ProjectName.Replace(" ", "_") }
            };

            string projectDir = Path.Combine(config.OutputPath, config.ProjectName);

            // 创建解决方案文件
            AnsiConsole.MarkupLine($"[grey]Running:[/] 正在为 {config.ProjectName} 创建 .sln 文件...");
            GenerateSolution(config.OutputPath, config.ProjectName);

            // 生成项目文件 (.csproj)
            AnsiConsole.MarkupLine($"[grey]Running:[/] 正在为 {config.ProjectName} 创建 .csproj 文件...");
            string csprojContent = GetResource("Csproj.txt");
            WriteFile(Path.Combine(projectDir, $"{config.ProjectName}.csproj"), csprojContent, tokens);

            // 生成必要代码
            AnsiConsole.MarkupLine($"[grey]Running:[/] 正在为 {config.ProjectName} 创建 必要项目文件...");
            WriteFile(Path.Combine(projectDir, "App.xaml"), GetResource("AppXaml.txt"), tokens);
            WriteFile(Path.Combine(projectDir, "App.xaml.cs"), GetResource("AppXamlCs.txt"), tokens);
            WriteFile(Path.Combine(projectDir, "MainWindow.xaml"), GetResource("MainWindowXaml.txt"), tokens);
            WriteFile(Path.Combine(projectDir, "MainWindow.xaml.cs"), GetResource("MainWindowXamlCs.txt"), tokens);
            WriteFile(Path.Combine(projectDir, "app.manifest"), GetResource("Manifest.txt"), tokens);
        }

        private string GetResource(string fileName) {
            var assembly = Assembly.GetExecutingAssembly();
            // 资源名称通常是 [命名空间].Templates.[文件名]
            string resourcePath = $"XamlNexus.Template.Winui3App.Templates.{fileName}";
            using var stream = assembly.GetManifestResourceStream(resourcePath);
            using var reader = new StreamReader(stream ?? throw new Exception($"找不到资源: {resourcePath}"));
            return reader.ReadToEnd();
        }
    }
}
