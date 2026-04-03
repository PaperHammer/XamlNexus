using Spectre.Console;
using System.Reflection;
using System.Xml.Linq;
using XamlNexus.Common.Utils;
using XamlNexus.Models.Attributes;

namespace XamlNexus.Common.Generators {
    public abstract class BaseGenerator : IGenerator {
        public void Generate(ProjectConfig config) {
            try {
                OnBeforeGenerate(config);

                AnsiConsole.MarkupLine($"\n[bold blue]{LanguageRegistry.GetI18n(LangKeys.Text_Start)} - {config.SlnName}[/]");

                string outputRoot = PrepareOutput(config);

                AnsiConsole.Progress()
                    .AutoRefresh(true)
                    .Columns(GetProgressColumns())
                    .Start(ctx => {
                        var projects = CopyModulesInternal(config, outputRoot, ctx);
                        CreateSlnInternal(config, outputRoot, projects, ctx);
                    });

                ShowSuccessReport(config, outputRoot);

                OnAfterGenerate(config, outputRoot);
            }
            catch (Exception ex) {
                OnError(config, ex);
            }
        }

        protected abstract string TemplateRoot { get; }

        protected abstract IEnumerable<(string Name, string? Folder)> GetProjects();

        #region Hooks

        protected virtual void OnBeforeGenerate(ProjectConfig config) { }

        protected virtual void OnAfterGenerate(ProjectConfig config, string outputRoot) { }

        protected virtual void OnError(ProjectConfig config, Exception ex) {
            if (Directory.Exists(config.OutputPath))
                Directory.Delete(config.OutputPath, true);

            AnsiConsole.MarkupLine($"\n[bold red]{LanguageRegistry.GetI18n(LangKeys.Text_Error)}[/]");
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
        }

        #endregion

        #region Core Pipeline

        private string PrepareOutput(ProjectConfig config) {
            string outputRoot = Path.Combine(config.OutputPath, config.SlnName);

            if (!Directory.Exists(outputRoot))
                Directory.CreateDirectory(outputRoot);

            return outputRoot;
        }

        private List<(string Path, string? Folder)> CopyModulesInternal(
            ProjectConfig config,
            string outputRoot,
            ProgressContext ctx) {
            var result = new List<(string Path, string? Folder)>();
            var tokens = GetTemplateTokens(config);
            var projects = GetProjects().ToList();

            var task = ctx.AddTask($"[yellow]{LanguageRegistry.GetI18n(LangKeys.Text_Generating_Module)}[/]", maxValue: projects.Count);

            foreach (var (Name, Folder) in projects) {
                string destName = TransformProjectName(Name, config);

                task.Description = $"  [yellow]> {LanguageRegistry.GetI18n(LangKeys.Text_Generating)}:[/] [cyan]{destName}[/]";

                string sourcePath = Path.Combine(TemplateRoot, Name);
                if (!Directory.Exists(sourcePath))
                    throw new Exception($"{LanguageRegistry.GetI18n(LangKeys.Text_Internal_Error)}");

                string destPath = Path.Combine(outputRoot, destName);

                ProcessDirectory(sourcePath, destPath, tokens);

                string csprojPath = Path.Combine(destPath, destName + ".csproj");

                InjectProjectMetadata(csprojPath, GetTemplateType());

                result.Add((csprojPath, Folder));

                task.Increment(1);
            }

            task.Description = $"[bold green]{LanguageRegistry.GetI18n(LangKeys.Text_Modules_Generated)}[/]";

            return result;
        }

        private void CreateSlnInternal(
            ProjectConfig config,
            string outputRoot,
            List<(string Path, string? Folder)> projects,
            ProgressContext ctx) {
            var slnTask = ctx.AddTask($"[yellow]{LanguageRegistry.GetI18n(LangKeys.Text_Generating_Solution)}[/]", maxValue: 100);

            string slnName = config.SlnName;
            string slnType = config.SlnType.ToString().ToLower();

            string cmd = $"new {slnType} -n \"{slnName}\"";
            var ok = ShellExecutor.Run("dotnet", cmd, outputRoot);

            string slnPath = Path.Combine(outputRoot, $"{slnName}.{slnType}");

            if (!ok || !File.Exists(slnPath))
                throw new Exception($"{LanguageRegistry.GetI18n(LangKeys.Text_Fail_To_Create_Sln)}: {slnPath}");

            slnTask.Value = 20;

            double step = 80.0 / projects.Count;

            foreach (var project in projects) {
                string relativePath = Path.GetRelativePath(outputRoot, project.Path);

                slnTask.Description = $"  [yellow]> {LanguageRegistry.GetI18n(LangKeys.Text_Linking)}:[/] [cyan]{Path.GetFileName(relativePath)}[/]";

                string addCmd = $"sln \"{slnPath}\" add \"{relativePath}\"";

                if (!string.IsNullOrEmpty(project.Folder))
                    addCmd += $" --solution-folder \"{project.Folder}\"";

                var added = ShellExecutor.Run("dotnet", addCmd, outputRoot);

                if (!added)
                    throw new Exception($"{LanguageRegistry.GetI18n(LangKeys.Text_Fail_To_Link_Project)}: {relativePath}");

                slnTask.Increment(step);
            }

            slnTask.Value = 100;
            slnTask.Description = $"[bold green]{LanguageRegistry.GetI18n(LangKeys.Text_Soluton_Created)}[/]";
        }

        #endregion

        #region Template Processing

        protected void ProcessDirectory(string sourcePath, string destPath, Dictionary<string, string> tokens) {
            if (!Directory.Exists(destPath))
                Directory.CreateDirectory(destPath);

            string currentDirName = Path.GetFileName(sourcePath);
            bool skipProcessing = GetSkipCopyDirs()
                .Any(x => string.Equals(x, currentDirName, StringComparison.OrdinalIgnoreCase));

            foreach (string file in Directory.GetFiles(sourcePath)) {
                string fileName = Path.GetFileName(file);

                if (skipProcessing || fileName.EndsWith(".user") || fileName == "bin" || fileName == "obj")
                    continue;

                string newFileName = ReplaceTokens(fileName, tokens);
                string destFile = Path.Combine(destPath, newFileName);

                if (IsTextFile(file)) {
                    string content = File.ReadAllText(file);
                    content = ReplaceTokens(content, tokens);
                    File.WriteAllText(destFile, content);
                }
                else {
                    File.Copy(file, destFile, true);
                }
            }

            foreach (string dir in Directory.GetDirectories(sourcePath)) {
                string dirName = Path.GetFileName(dir);

                if (dirName == "bin" || dirName == "obj" || dirName == ".vs" || dirName == "Plugins")
                    continue;

                string newDirName = ReplaceTokens(dirName, tokens);

                ProcessDirectory(dir, Path.Combine(destPath, newDirName), tokens);
            }
        }

        protected bool IsTextFile(string path) {
            string ext = Path.GetExtension(path).ToLower();

            string[] textExts = [
                ".cs", ".xaml", ".csproj", ".sln", ".slnx",
                ".json", ".xml", ".config", ".txt", ".md",
                ".resw", ".resx",
                ".manifest", ".appxmanifest",
                ".proto"
            ];

            return textExts.Contains(ext);
        }

        private string ReplaceTokens(string input, Dictionary<string, string> tokens) {
            foreach (var token in tokens)
                input = input.Replace(token.Key, token.Value);

            return input;
        }

        #endregion

        #region Metadata Injection

        protected void InjectProjectMetadata(string csprojPath, string templateType) {
            if (!File.Exists(csprojPath)) return;

            var doc = XDocument.Load(csprojPath);
            var project = doc.Root;
            if (project == null) return;

            var propertyGroup = project.Elements("PropertyGroup")
                .FirstOrDefault(x => x.Attribute("Label")?.Value == "XamlNexus");

            if (propertyGroup == null) {
                propertyGroup = new XElement("PropertyGroup",
                    new XAttribute("Label", "XamlNexus"));

                project.Add(propertyGroup);
            }

            string version = GetGeneratorVersion();

            SetOrUpdate(propertyGroup, "Description", "Generated by XamlNexus Generator");
            SetOrUpdate(propertyGroup, "XamlNexusVersion", version);
            SetOrUpdate(propertyGroup, "XamlNexusTemplate", templateType);

            doc.Save(csprojPath);
        }

        protected virtual string GetGeneratorVersion() {
            var assembly = Assembly.GetExecutingAssembly();

            var infoVersion = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            string version = infoVersion
                ?? assembly.GetName().Version?.ToString()
                ?? "1.0.0";

            var parts = version.Split('.', '-', '+');

            return parts.Length >= 3
                ? $"{parts[0]}.{parts[1]}.{parts[2]}"
                : version;
        }

        private void SetOrUpdate(XElement parent, string name, string value) {
            var element = parent.Element(name);

            if (element == null)
                parent.Add(new XElement(name, value));
            else
                element.Value = value;
        }

        #endregion

        #region Strategy / Customization

        protected virtual string TransformProjectName(string name, ProjectConfig config) {
            return name.Replace(GetTemplatePrefix(), config.SlnName);
        }

        protected virtual string GetTemplatePrefix() => "XamlNexus";

        protected virtual string GetTemplateType() {
            var attr = GetType().GetCustomAttribute<GeneratorAttribute>();
            return attr?.Framework.ToString() ?? "Unknown";
        }

        protected virtual string[] GetSkipCopyDirs() => [];

        protected virtual Dictionary<string, string> GetTemplateTokens(ProjectConfig config) {
            var tokens = GetBaseTokens(config);
            var extra = GetCustomTokens(config);

            foreach (var kv in extra) {
                tokens[kv.Key] = kv.Value; // 覆盖 or 新增
            }

            return tokens;
        }

        protected virtual Dictionary<string, string> GetBaseTokens(ProjectConfig config) {
            return new Dictionary<string, string> {
                { "{{DEFAULT_LANGUAGE}}", config.Language ?? "zh-CN" }
            };
        }

        protected virtual Dictionary<string, string> GetCustomTokens(ProjectConfig config) {
            return [];
        }

        protected virtual ProgressColumn[] GetProgressColumns() {
            return [
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn(Spinner.Known.Aesthetic)
            ];
        }

        protected virtual void ShowSuccessReport(ProjectConfig config, string outputRoot) {
            var table = new Table().Border(TableBorder.Rounded);

            table.AddColumn($"[cyan]{LanguageRegistry.GetI18n(LangKeys.Text_Property)}[/]");
            table.AddColumn($"[green]{LanguageRegistry.GetI18n(LangKeys.Text_Value)}[/]");

            table.AddRow(LanguageRegistry.GetI18n(LangKeys.Text_Project), config.SlnName);
            table.AddRow(LanguageRegistry.GetI18n(LangKeys.Text_Framework), config.Framework.ToString());
            table.AddRow(LanguageRegistry.GetI18n(LangKeys.Text_Format), config.SlnType.ToString());
            table.AddRow(LanguageRegistry.GetI18n(LangKeys.Text_OutputPath), outputRoot);

            AnsiConsole.Write(table);

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($" [bold green]{LanguageRegistry.GetI18n(LangKeys.Text_Success)}[/]");
        }

        #endregion
    }
}
