using Spectre.Console;
using XamlNexus.Common.Generators;
using XamlNexus.Common.Utils;
using XamlNexus.Models.Attributes;

namespace XamlNexus.Generator.Winui3App {
    [Generator(FrameworkType.Winui3)]
    public class Winui3Generator : BaseGenerator {
        protected Dictionary<string, string> GetTemplateTokens(ProjectConfig config) {
            return new Dictionary<string, string> {
                { "Winui3_XamlNexus", config.ProjectName },
                { "PROJECT_NAME", config.ProjectName },
                { "SAFE_NAMESPACE", config.ProjectName.Replace(" ", "_") }
            };
        }

        public override void Generate(ProjectConfig config) {            
            string outputRoot = config.OutputPath;
            var tokens = GetTemplateTokens(config);
            var generatedProjects = new List<(string Path, string? Folder)>();

            string templateRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "Winui3");

            AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .Start(LanguageRegistry.GetI18n(LangKeys.Generating), ctx => {

                    // 2. 搬运并重命名所有子项目
                    // 根据你截图中的项目列表
                    var projects = new[] {
                        (Name: "Winui3_XamlNexus.UI",          SlnFolder: (string?)null),
                        (Name: "Winui3_XamlNexus.Common",      SlnFolder: (string?)null),
                        (Name: "Winui3_XamlNexus.Models",      SlnFolder: (string?)null),
                        (Name: "Winui3_XamlNexus.UIComponent", SlnFolder: (string?)null),
                        (Name: "Panels/Winui3_XamlNexus.MainPanel",        SlnFolder: "Panels"),
                        (Name: "Panels/Winui3_XamlNexus.AppSettingsPanel", SlnFolder: "Panels")
                    };

                    foreach (var item in projects) {
                        ctx.Status($"{LanguageRegistry.GetI18n(LangKeys.SetupStructure)}: {item.Name}");

                        // 1. 获取物理路径
                        // 注意：Path.GetFileName 会自动处理 "Panels/..." 这种情况，只拿最后的文件夹名
                        string originalFolderName = Path.GetFileName(item.Name);
                        string sourceProjectPath = Path.Combine(templateRoot, item.Name);

                        if (!Directory.Exists(sourceProjectPath)) continue;

                        // 2. 物理搬运与重命名
                        string destFolderName = originalFolderName.Replace("Winui3_XamlNexus", config.ProjectName);
                        string destProjectPath = Path.Combine(outputRoot, destFolderName);

                        ProcessDirectory(sourceProjectPath, destProjectPath, tokens);

                        // 3. 记录生成的项目路径及其对应的解决方案虚拟目录
                        string csprojName = destFolderName + ".csproj";
                        string fullCsprojPath = Path.Combine(destProjectPath, csprojName);

                        // 将 (项目路径, 虚拟目录名) 存入列表
                        generatedProjects.Add((Path: fullCsprojPath, Folder: item.SlnFolder));
                    }

                    // 3. 生成解决方案
                    ctx.Status(LanguageRegistry.GetI18n(LangKeys.Finalizing));
                    CreateSolution(config, generatedProjects);
                });

            ShowSuccessReport(config);
        }

        private void CreateSolution(ProjectConfig config, List<(string Path, string? Folder)> projects) {
            string slnName = config.ProjectName;
            string slnType = config.SlnType == SolutionType.Slnx ? "slnx" : "sln";

            ShellExecutor.Run("dotnet", $"new {slnType} -n {slnName}", config.OutputPath);
            string slnPath = Path.Combine(config.OutputPath, $"{slnName}.{slnType}");

            foreach (var project in projects) {
                string relativePath = Path.GetRelativePath(config.OutputPath, project.Path);

                // 构建命令
                string cmd = $"sln \"{slnPath}\" add \"{relativePath}\"";

                // 如果配置了虚拟目录，则追加参数
                if (!string.IsNullOrEmpty(project.Folder)) {
                    cmd += $" --solution-folder \"{project.Folder}\"";
                }

                ShellExecutor.Run("dotnet", cmd, config.OutputPath);
            }
        }

        private void ShowSuccessReport(ProjectConfig config) {
            AnsiConsole.WriteLine();
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("[cyan]Property[/]");
            table.AddColumn("[green]Value[/]");
            table.AddRow("Project", config.ProjectName);
            table.AddRow("Format", config.SlnType.ToString());
            table.AddRow("Output", config.OutputPath);
            AnsiConsole.Write(table);

            AnsiConsole.MarkupLine($"[yellow]Tip:[/] cd [white]{config.ProjectName}[/] && dotnet build");
        }

        protected void ProcessDirectory(string sourcePath, string destPath, Dictionary<string, string> tokens) {
            if (!Directory.Exists(destPath)) Directory.CreateDirectory(destPath);

            foreach (string file in Directory.GetFiles(sourcePath)) {
                string fileName = Path.GetFileName(file);
                if (fileName.EndsWith(".user") || fileName == "bin" || fileName == "obj") continue;

                // 替换文件名中的 Token
                string newFileName = fileName;
                foreach (var token in tokens) {
                    newFileName = newFileName.Replace(token.Key, token.Value);
                }

                string destFile = Path.Combine(destPath, newFileName);

                if (IsTextFile(file)) {
                    string content = File.ReadAllText(file);

                    // 替换内容中的 Token (Namespace, ProjectName 等)
                    foreach (var token in tokens) {
                        content = content.Replace(token.Key, token.Value);
                    }

                    //// 特殊处理：集成 .csproj 路径优化
                    //if (file.EndsWith(".csproj")) {
                    //    content = InjectPathOptimization(content);
                    //}

                    File.WriteAllText(destFile, content);
                }
                else {
                    File.Copy(file, destPath, true);
                }
            }

            // 递归子目录
            foreach (string dir in Directory.GetDirectories(sourcePath)) {
                string dirName = Path.GetFileName(dir);
                if (dirName == "bin" || dirName == "obj" || dirName == ".vs") continue;

                string newDirName = dirName;
                foreach (var token in tokens) newDirName = newDirName.Replace(token.Key, token.Value);
                ProcessDirectory(dir, Path.Combine(destPath, newDirName), tokens);
            }
        }

        //private string InjectPathOptimization(string content) {
        //    // 定义要注入的配置
        //    string optimizationNodes =
        //        "\n    " +
        //        "\n    <AppendRuntimeIdentifierToOutputPath>false</AppendRuntimeIdentifierToOutputPath>" +
        //        "\n    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>" +
        //        "\n    <OutputPath>bin\\$(Configuration)\\</OutputPath>" +
        //        "\n    <EnableMsixTooling Condition=\"'$(Configuration)' == 'Debug'\">false</EnableMsixTooling>\n  ";

        //    // 寻找第一个 PropertyGroup 的闭合标签并在此之前插入
        //    if (content.Contains("</PropertyGroup>")) {
        //        var index = content.IndexOf("</PropertyGroup>");
        //        return content.Insert(index, optimizationNodes);
        //    }

        //    return content;
        //}

        private bool IsTextFile(string path) {
            string ext = Path.GetExtension(path).ToLower();
            string[] textExts = { ".cs", ".xaml", ".csproj", ".sln", ".slnx", ".json", ".xml", ".config", ".txt", ".md" };
            return textExts.Contains(ext);
        }
    }
}
