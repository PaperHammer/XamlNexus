using Spectre.Console;
using System.Reflection;
using System.Xml.Linq;
using XamlNexus.Common.Projects;
using XamlNexus.Common.Utils;

namespace XamlNexus.Common.Generators {
    public abstract class BaseGenerator : IGenerator {
        public IReadOnlyList<string> GetIncludedModuleIds(string profile) {
            if (profile is not ("standard" or "basic"))
                throw new ArgumentException("Profile must be standard or basic.", nameof(profile));

            // 只有 basic 模式下的 settings 会被过滤
            return [.. GetManagedModuleIds().Where(id => !(profile == "basic" && id == "settings")).Distinct(StringComparer.OrdinalIgnoreCase)];
        }

        public bool Generate(ProjectConfig config) => Generate(config, reportSuccess: true);

        public bool Generate(ProjectConfig config, bool reportSuccess) {
            string? outputRoot = null;

            try {
                if (config.Profile is not ("standard" or "basic"))
                    throw new ArgumentException("Profile must be standard or basic.");

                OnBeforeGenerate(config);

                AnsiConsole.MarkupLine($"\n[bold blue]{LanguageRegistry.GetI18n(LangKeys.Text_Start)} - {config.SlnName}[/]");

                outputRoot = PrepareOutput(config);

                AnsiConsole.Progress()
                    .AutoRefresh(true)
                    .Columns(GetProgressColumns())
                    .Start(ctx => {
                        var projects = CopyModulesInternal(config, outputRoot, ctx);
                        CreateSlnInternal(config, outputRoot, projects, ctx);
                    });

                CommandLine.CreationReport.RunFinishing(() => WriteProjectManifest(config, outputRoot));
                if (reportSuccess) ShowSuccessReport(config, outputRoot);

                OnAfterGenerate(config, outputRoot);

                return true;
            }
            catch (Exception ex) {
                CleanupGeneratedOutput(config.OutputPath, outputRoot, ex);
                OnError(config, ex);
                return false;
            }
        }

        protected abstract string TemplateRoot { get; }

        protected abstract IEnumerable<(string Name, string? Folder)> GetProjects();

        #region Hooks

        protected virtual void OnBeforeGenerate(ProjectConfig config, bool reportSuccess = true) { }

        protected virtual void OnAfterGenerate(ProjectConfig config, string outputRoot) { }

        protected virtual void OnError(ProjectConfig config, Exception ex) {
            AnsiConsole.MarkupLine($"\n[bold red]{LanguageRegistry.GetI18n(LangKeys.Text_Error)}[/]");
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
        }

        private static void CleanupGeneratedOutput(string outputPath, string? outputRoot, Exception generationException) {
            if (string.IsNullOrWhiteSpace(outputRoot) || !Directory.Exists(outputRoot))
                return;

            try {
                string parentPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(outputPath));
                string generatedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(outputRoot));
                string? generatedParent = Directory.GetParent(generatedPath)?.FullName;

                // The generator only owns the direct child directory created for this run.
                // Never recursively delete the configured output directory or a path outside it.
                if (!string.Equals(
                        Path.TrimEndingDirectorySeparator(generatedParent ?? string.Empty),
                        parentPath,
                        StringComparison.OrdinalIgnoreCase)) {
                    generationException.Data["CleanupError"] =
                        $"Refused to clean an unsafe generated path: {generatedPath}";
                    return;
                }

                Directory.Delete(generatedPath, true);
            }
            catch (Exception cleanupException) {
                // Preserve the original generation error while retaining cleanup diagnostics.
                generationException.Data["CleanupError"] = cleanupException.Message;
            }
        }

        #endregion

        #region Core Pipeline

        private string PrepareOutput(ProjectConfig config) {
            return ProjectOutputReservation.Create(config.OutputPath, config.SlnName);
        }

        private List<(string Path, string? Folder)> CopyModulesInternal(
            ProjectConfig config,
            string outputRoot,
            ProgressContext ctx) {
            var result = new List<(string Path, string? Folder)>();
            var tokens = GetTemplateTokens(config);
            var projects = GetProjects().Where(project => config.Profile != "basic"
                || !project.Name.EndsWith(".AppSettingsPanel", StringComparison.Ordinal)).ToList();

            CopyTemplateRootAssets(
                Path.Combine(Directory.GetParent(TemplateRoot)?.FullName ?? string.Empty, "Shared"),
                outputRoot,
                tokens);
            CopyTemplateRootAssets(TemplateRoot, outputRoot, tokens);

            var task = ctx.AddTask($"[yellow]{LanguageRegistry.GetI18n(LangKeys.Text_Generating_Module)}[/]", maxValue: projects.Count);

            foreach (var (Name, Folder) in projects) {
                string destName = TransformProjectName(Name, config);

                task.Description = $"  [yellow]> {LanguageRegistry.GetI18n(LangKeys.Text_Generating)}:[/] [cyan]{destName}[/]";

                string sourcePath = Path.Combine(TemplateRoot, Name);
                if (!Directory.Exists(sourcePath))
                    throw new DirectoryNotFoundException(
                        $"{LanguageRegistry.GetI18n(LangKeys.Text_Internal_Error)}" +
                        $"{Environment.NewLine}Template directory: {sourcePath}");

                string destPath = Path.Combine(outputRoot, destName);

                ProcessDirectory(sourcePath, destPath, tokens);

                if (config.Profile == "basic" && destName.EndsWith(".UI", StringComparison.Ordinal)) {
                    File.Delete(Path.Combine(destPath, "Modules", "SettingsModule.cs"));
                    string uiProject = Path.Combine(destPath, destName + ".csproj");
                    var document = XDocument.Load(uiProject);
                    document.Descendants().Where(element => element.Name.LocalName == "ProjectReference"
                        && ((string?)element.Attribute("Include"))?.Contains(".AppSettingsPanel") == true).Remove();
                    document.Save(uiProject);
                }

                string csprojPath = Path.Combine(destPath, destName + ".csproj");

                InjectProjectMetadata(csprojPath, GetTemplateType());

                // Panel 项目按命名约定统一归入解决方案的 Panels 文件夹。
                result.Add((csprojPath, Path.GetFileNameWithoutExtension(csprojPath)
                    .EndsWith("Panel", StringComparison.OrdinalIgnoreCase) ? "Panels" : Folder));

                task.Increment(1);
            }

            task.Description = $"[bold green]{LanguageRegistry.GetI18n(LangKeys.Text_Modules_Generated)}[/]";

            return result;
        }

        private void CopyTemplateRootAssets(
            string sourceRoot,
            string outputRoot,
            Dictionary<string, string> tokens) {
            if (!Directory.Exists(sourceRoot)) return;

            foreach (string file in Directory.GetFiles(sourceRoot)) {
                CopyTemplateFile(file, Path.Combine(outputRoot, ReplaceTokens(Path.GetFileName(file), tokens)), tokens);
            }

            foreach (string directoryName in new[] { ".github", "eng" }) {
                string sourceDirectory = Path.Combine(sourceRoot, directoryName);
                if (Directory.Exists(sourceDirectory)) {
                    ProcessDirectory(sourceDirectory, Path.Combine(outputRoot, directoryName), tokens);
                }
            }
        }

        private void CreateSlnInternal(
            ProjectConfig config,
            string outputRoot,
            List<(string Path, string? Folder)> projects,
            ProgressContext ctx) {
            var slnTask = ctx.AddTask($"[yellow]{LanguageRegistry.GetI18n(LangKeys.Text_Generating_Solution)}[/]", maxValue: 100);

            string slnName = config.SlnName;
            string slnType = config.SlnType.ToString().ToLower();

            var sdk = ShellExecutor.Run("dotnet", "--version", outputRoot);
            bool supportsFormat = sdk.Success
                && Version.TryParse(sdk.StandardOutput.Trim().Split('-')[0], out var version)
                && version >= new Version(9, 0, 200);
            if (config.SlnType == SolutionType.Slnx && !supportsFormat)
                throw new InvalidOperationException("SLNX requires the selected .NET SDK to be 9.0.200 or newer. Use --solution-format sln with older SDKs.");

            // Older SDKs create SLN by default and do not recognize --format.
            string cmd = $"new sln -n \"{slnName}\"" + (supportsFormat ? $" --format {slnType}" : "");
            var createResult = ShellExecutor.Run("dotnet", cmd, outputRoot);

            string slnPath = Path.Combine(outputRoot, $"{slnName}.{slnType}");

            if (!createResult.Success || !File.Exists(slnPath))
                throw new Exception(
                    $"{LanguageRegistry.GetI18n(LangKeys.Text_Fail_To_Create_Sln)}: {slnPath}" +
                    $"{Environment.NewLine}dotnet {cmd}" +
                    $"{Environment.NewLine}Exit code: {createResult.ExitCode}" +
                    $"{Environment.NewLine}{createResult.DiagnosticOutput}");

            slnTask.Value = 20;

            // Newer SDKs recursively add referenced projects by default, placing
            // panels at the root before their explicit folder assignment runs.
            // Detect the option rather than passing it to SDKs that lack it.
            var addHelp = ShellExecutor.Run("dotnet", "sln add --help", outputRoot);
            bool supportsReferenceOption = addHelp.Success &&
                addHelp.StandardOutput.Contains("--include-references", StringComparison.Ordinal);
            double step = 80.0 / projects.Count;

            foreach (var project in projects) {
                string relativePath = Path.GetRelativePath(outputRoot, project.Path);

                slnTask.Description = $"  [yellow]> {LanguageRegistry.GetI18n(LangKeys.Text_Linking)}:[/] [cyan]{Path.GetFileName(relativePath)}[/]";

                string addCmd = $"sln \"{slnPath}\" add \"{relativePath}\"";

                if (!string.IsNullOrEmpty(project.Folder))
                    addCmd += $" --solution-folder \"{project.Folder}\"";

                if (supportsReferenceOption)
                    addCmd += " --include-references false";

                var addResult = ShellExecutor.Run("dotnet", addCmd, outputRoot);

                if (!addResult.Success)
                    throw new Exception(
                        $"{LanguageRegistry.GetI18n(LangKeys.Text_Fail_To_Link_Project)}: {relativePath}" +
                        $"{Environment.NewLine}dotnet {addCmd}" +
                        $"{Environment.NewLine}Exit code: {addResult.ExitCode}" +
                        $"{Environment.NewLine}{addResult.DiagnosticOutput}");

                slnTask.Increment(step);
            }

            string solutionText = File.ReadAllText(slnPath);
            string startupName = config.SlnName + (Framework == FrameworkType.Winui3 ? ".UI" : "");
            string startupPath = $"{startupName}/{startupName}.csproj";
            if (config.SlnType == SolutionType.Slnx) {
                // dotnet sln add can omit platform mappings. WinUI does not support
                // the implicit Any CPU configuration used by a minimal SLNX file.
                var document = XDocument.Parse(solutionText);
                var root = document.Root!;
                root.Element("Configurations")?.Remove();
                root.AddFirst(new XElement("Configurations",
                    new[] { "x64", "x86", "ARM64" }.Select(platform =>
                        new XElement("Platform", new XAttribute("Name", platform)))));
                foreach (var entry in root.Descendants("Project")) {
                    bool isStartup = entry.Attribute("Path")!.Value.Replace('\\', '/')
                        .Equals(startupPath, StringComparison.OrdinalIgnoreCase);
                    entry.SetAttributeValue("DefaultStartup", isStartup ? "true" : null);
                    string projectPath = Path.Combine(outputRoot, entry.Attribute("Path")!.Value);
                    var projectXml = XDocument.Load(projectPath);
                    bool hasArchitecturePlatforms = projectXml.Descendants().Any(element =>
                        element.Name.LocalName == "Platforms" && element.Value.Split(';').Contains("x64"));
                    entry.Elements("Platform").Remove();
                    // Libraries without explicit platforms keep Any CPU; the UI executable
                    // declares architecture platforms and follows the solution configuration.
                    entry.Add(new XElement("Platform",
                        new XAttribute("Project", hasArchitecturePlatforms ? "*" : "Any CPU")));
                }
                solutionText = document.ToString();
                File.WriteAllText(slnPath, solutionText, new System.Text.UTF8Encoding(false));
            }
            else {
                // SLN has no shared startup-project field. Visual Studio uses the first
                // project on initial open, before a per-user selection has been saved.
                var blocks = System.Text.RegularExpressions.Regex.Matches(solutionText,
                    @"(?m)^Project\([^\r\n]+\r?\n[\s\S]*?^EndProject[^\S\r\n]*(?:\r?\n|$)");
                var startup = blocks.Cast<System.Text.RegularExpressions.Match>().Single(block =>
                    block.Value.Split('\n')[0].Replace('\\', '/')
                        .Contains($"\"{startupPath}\"", StringComparison.OrdinalIgnoreCase));
                int firstProject = blocks[0].Index;
                solutionText = solutionText.Remove(startup.Index, startup.Length)
                    .Insert(firstProject, startup.Value);
                File.WriteAllText(slnPath, solutionText, new System.Text.UTF8Encoding(false));
            }
            string normalizedSolution = XamlNexusSolutionGuid.NormalizeProjectGuids(solutionText);
            normalizedSolution = SolutionDocuments.Update(normalizedSolution, slnPath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase),
                Directory.EnumerateFiles(outputRoot, "*", SearchOption.TopDirectoryOnly)
                    .Where(path => path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)).Select(path => Path.GetFileName(path)), []);
            if (!normalizedSolution.Equals(solutionText, StringComparison.Ordinal))
                File.WriteAllText(slnPath, normalizedSolution, new System.Text.UTF8Encoding(false));

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
                CopyTemplateFile(file, destFile, tokens);
            }

            foreach (string dir in Directory.GetDirectories(sourcePath)) {
                string dirName = Path.GetFileName(dir);

                if (dirName == "bin" || dirName == "obj" || dirName == ".vs" || dirName == "Plugins")
                    continue;

                string newDirName = ReplaceTokens(dirName, tokens);

                ProcessDirectory(dir, Path.Combine(destPath, newDirName), tokens);
            }
        }

        private void CopyTemplateFile(
            string sourceFile,
            string destinationFile,
            Dictionary<string, string> tokens) {
            if (IsTextFile(sourceFile)) {
                string content = ReplaceTokens(File.ReadAllText(sourceFile), tokens);
                File.WriteAllText(destinationFile, content);
            }
            else {
                File.Copy(sourceFile, destinationFile, true);
            }
        }

        protected bool IsTextFile(string path) {
            string ext = Path.GetExtension(path).ToLower();

            string[] textExts = [
                ".cs", ".xaml", ".csproj", ".sln", ".slnx",
                ".json", ".xml", ".config", ".txt", ".md",
                ".yml", ".yaml", ".ps1",
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

        private void WriteProjectManifest(ProjectConfig config, string outputRoot) {
            string version = GetGeneratorVersion();
            var manifest = new XamlNexusProjectManifest {
                GeneratorVersion = version,
                Project = new XamlNexusProjectIdentity {
                    Name = config.SlnName,
                    Profile = config.Profile,
                    Preset = GetPresetId(),
                    Language = config.Language,
                    SolutionFormat = config.SlnType.ToString().ToLowerInvariant(),
                },
                Modules = GetIncludedModuleIds(config.Profile)
                    .Select(id => new XamlNexusManagedModule {
                        Id = id,
                        Version = version,
                        Source = "template",
                    })
                    .ToArray(),
                ScaffoldFiles = CollectScaffoldFiles(outputRoot),
            };

            XamlNexusProjectManifestStore.Save(
                Path.Combine(outputRoot, "xamlnexus.json"),
                manifest);
        }

        private static IReadOnlyList<XamlNexusManagedFile> CollectScaffoldFiles(string outputRoot) {
            return Directory.EnumerateFiles(outputRoot, "*", SearchOption.AllDirectories)
                .Select(path => new {
                    FullPath = path,
                    RelativePath = Path.GetRelativePath(outputRoot, path).Replace('\\', '/'),
                })
                .Where(file => IsScaffoldFile(file.RelativePath))
                .OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
                .Select(file => new XamlNexusManagedFile {
                    Path = file.RelativePath,
                    Sha256 = ComputeSha256(file.FullPath),
                    BaselineContentGzipBase64 = XamlNexusBaselineContent.Encode(File.ReadAllBytes(file.FullPath)),
                    UserEditable = !IsInfrastructureFile(file.RelativePath),
                })
                .ToArray();
        }

        private static bool IsScaffoldFile(string relativePath) {
            if (relativePath.Equals("xamlnexus.json", StringComparison.OrdinalIgnoreCase) ||
                relativePath.EndsWith(".user", StringComparison.OrdinalIgnoreCase)) {
                return false;
            }
            string[] segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return !segments.Any(segment => segment.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals(".vs", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("Plugins", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsInfrastructureFile(string relativePath) =>
            relativePath.Equals("Directory.Build.props", StringComparison.OrdinalIgnoreCase) ||
            relativePath.Equals("RELEASING.md", StringComparison.OrdinalIgnoreCase) ||
            relativePath.Equals("RELEASING.zh-CN.md", StringComparison.OrdinalIgnoreCase) ||
            relativePath.Equals("update-manifest.example.json", StringComparison.OrdinalIgnoreCase) ||
            relativePath.StartsWith(".github/", StringComparison.OrdinalIgnoreCase) ||
            relativePath.StartsWith("eng/", StringComparison.OrdinalIgnoreCase) ||
            relativePath.Contains(".Common/Updates/", StringComparison.OrdinalIgnoreCase) ||
            relativePath.EndsWith("/Modules/IXamlNexusModule.cs", StringComparison.OrdinalIgnoreCase);

        private static string ComputeSha256(string path) {
            using FileStream stream = File.OpenRead(path);
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)).ToLowerInvariant();
        }

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
            // The common generator library has its own assembly version. The manifest must
            // track the installed CLI/tool version that orchestrated this generation run.
            var assembly = Assembly.GetEntryAssembly() ?? GetType().Assembly;

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

        protected abstract FrameworkType Framework { get; }

        protected virtual string GetTemplateType() => Framework.ToString();

        protected abstract string GetPresetId();

        protected virtual IEnumerable<string> GetManagedModuleIds() {
            return [
                "app-shell",
                "arcxaml",
                "localization",
                "logging",
                "settings",
                "single-instance",
                "updater",
                "github-release"
            ];
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
                { "{{DEFAULT_LANGUAGE}}", config.Language ?? "zh-CN" },
                { "{{SOLUTION_FORMAT}}", config.SlnType.ToString().ToLowerInvariant() }
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
            CommandLine.CreationReport.Write(config, outputRoot);
        }

        #endregion
    }
}
