using Spectre.Console;
using XamlNexus.Common.Generators;
using XamlNexus.Common.Utils;
using XamlNexus.Models.Attributes;

namespace XamlNexus.Generator.Winui3App {
    [Generator(FrameworkType.Winui3)]
    public class Winui3Generator : BaseGenerator {
        public override void Generate(ProjectConfig config) {
            try {
                AnsiConsole.MarkupLine($"\n[bold blue]{LanguageRegistry.GetI18n(LangKeys.Start)} - {config.SlnName}[/]");

                string outputRoot = Path.Combine(config.OutputPath, config.SlnName);
                if (!Directory.Exists(outputRoot)) Directory.CreateDirectory(outputRoot);

                AnsiConsole.Progress()
                    .AutoRefresh(true)
                    .Columns([
                        new TaskDescriptionColumn(){Alignment = Justify.Left},
                        new ProgressBarColumn(),
                        new PercentageColumn(),
                        new SpinnerColumn(Spinner.Known.Aesthetic),
                    ])
                    .Start(ctx => {
                        var generatedProjects = CopyModules(config, outputRoot, ctx);
                        CreateSln(config, outputRoot, generatedProjects, ctx);
                    });
                ShowSuccessReport(config, outputRoot);
            }
            catch (Exception ex) {
                Directory.Delete(config.OutputPath, true);
                AnsiConsole.MarkupLine($"\n[bold red]{LanguageRegistry.GetI18n(LangKeys.Error)}[/]");
                AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
            }
        }

        private List<(string Path, string? Folder)> CopyModules(ProjectConfig config, string outputRoot, ProgressContext ctx) {
            var generatedProjects = new List<(string Path, string? Folder)>();
            var tokens = GetTemplateTokens(config);
            string templateRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "Winui3");
            var projects = new[] {
                (Name: "Winui3_XamlNexus.UI", SlnFolder: null),
                (Name: "Winui3_XamlNexus.Common", SlnFolder: null),
                (Name: "Winui3_XamlNexus.Models", SlnFolder: null),
                (Name: "Winui3_XamlNexus.UIComponent", SlnFolder: null),
                (Name: "Winui3_XamlNexus.MainPanel", SlnFolder: "Panels"),
                (Name: "Winui3_XamlNexus.AppSettingsPanel", SlnFolder: "Panels")
            };

            var moduleTask = ctx.AddTask($"[yellow]{LanguageRegistry.GetI18n(LangKeys.GeneratingModules)} Modules[/]", maxValue: projects.Length);
            foreach (var (Name, SlnFolder) in projects) {
                string originalFolderName = Path.GetFileName(Name);
                string destFolderName = originalFolderName.Replace("Winui3_XamlNexus", config.SlnName);
                moduleTask.Description = $"  [yellow]> Generating:[/] [cyan]{destFolderName}[/]";
                string sourceProjectPath = Path.Combine(templateRoot, Name);
                if (Directory.Exists(sourceProjectPath)) {
                    string destProjectPath = Path.Combine(outputRoot, destFolderName);

                    ProcessDirectory(sourceProjectPath, destProjectPath, tokens);

                    string fullCsprojPath = Path.Combine(destProjectPath, destFolderName + ".csproj");
                    generatedProjects.Add((fullCsprojPath, SlnFolder));
                }
                moduleTask.Increment(1);
            }
            moduleTask.Description = "[bold green]Modules Generated[/]";

            return generatedProjects;
        }

        private void CreateSln(ProjectConfig config, string outputRoot, List<(string Path, string? Folder)> projects, ProgressContext ctx) {
            var slnTask = ctx.AddTask($"[yellow]{LanguageRegistry.GetI18n(LangKeys.GeneratingSolution)}[/]", maxValue: 100);
            string slnName = config.SlnName;
            string slnType = config.SlnType.ToString().ToLower();
            //string slnType = config.SlnType == SolutionType.Slnx ? "slnx" : "sln";
            string subCmd = $"new {slnType} -n \"{slnName}\"";
            var isSlnAvaliable = ShellExecutor.Run("dotnet", subCmd, outputRoot);
            string slnPath = Path.Combine(outputRoot, $"{slnName}.{slnType}");
            if (!isSlnAvaliable || !File.Exists(slnPath)) {
                string errorTmpl = LanguageRegistry.GetI18n(LangKeys.ErrorCreateSolution);
                throw new Exception(string.Format(errorTmpl, slnPath));
            }
            slnTask.Value = 20;

            double incrementPerProject = 80.0 / projects.Count;
            foreach (var project in projects) {
                string relativePath = Path.GetRelativePath(outputRoot, project.Path);
                slnTask.Description = $"  [yellow]> Linking:[/] [cyan]{Path.GetFileName(relativePath)}[/]";

                string cmd = $"sln \"{slnPath}\" add \"{relativePath}\"";
                if (!string.IsNullOrEmpty(project.Folder)) {
                    cmd += $" --solution-folder \"{project.Folder}\"";
                }

                var isAdded = ShellExecutor.Run("dotnet", cmd, outputRoot);
                if (!isAdded) {
                    string errorTmpl = LanguageRegistry.GetI18n(LangKeys.ErrorAddProject);
                    throw new Exception(string.Format(errorTmpl, relativePath));
                }
                slnTask.Increment(incrementPerProject);
            }

            slnTask.Value = 100;
            slnTask.Description = "[bold green]Solution Created[/]";
        }

        private void ShowSuccessReport(ProjectConfig config, string outputRoot) {
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("[cyan]Property[/]");
            table.AddColumn("[green]Value[/]");
            table.AddRow("Project", config.SlnName);
            table.AddRow("Framework", config.Framework.ToString());
            table.AddRow("Format", config.SlnType.ToString());
            table.AddRow("Output", outputRoot);
            AnsiConsole.Write(table);

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($" [bold green]{LanguageRegistry.GetI18n(LangKeys.SuccessMessage)}[/]");
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

                    // 替换内容中的 Token (Namespace, SlnName 等)
                    foreach (var token in tokens) {
                        content = content.Replace(token.Key, token.Value);
                    }

                    File.WriteAllText(destFile, content);
                }
                else {
                    File.Copy(file, destFile, true);
                }
            }

            foreach (string dir in Directory.GetDirectories(sourcePath)) {
                string dirName = Path.GetFileName(dir);
                if (dirName == "bin" || dirName == "obj" || dirName == ".vs") continue;

                string newDirName = dirName;
                foreach (var token in tokens) newDirName = newDirName.Replace(token.Key, token.Value);
                ProcessDirectory(dir, Path.Combine(destPath, newDirName), tokens);
            }
        }

        private Dictionary<string, string> GetTemplateTokens(ProjectConfig config) {
            return new Dictionary<string, string> {
                { "Winui3_XamlNexus", config.SlnName },
                { "PROJECT_NAME", config.SlnName },
                { "SAFE_NAMESPACE", config.SlnName.Replace(" ", "_") }
            };
        }
    }
}
