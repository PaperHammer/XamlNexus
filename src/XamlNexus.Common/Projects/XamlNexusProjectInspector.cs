namespace XamlNexus.Common.Projects;

public sealed record XamlNexusProjectContext(string RootDirectory, string ManifestPath, XamlNexusProjectManifest Manifest);

public static class XamlNexusProjectLocator {
    public static XamlNexusProjectContext Locate(string startPath) {
        ArgumentException.ThrowIfNullOrWhiteSpace(startPath);

        string fullPath = Path.GetFullPath(startPath);
        if (File.Exists(fullPath)) {
            if (!Path.GetFileName(fullPath).Equals("xamlnexus.json", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"The project path must be a directory or an xamlnexus.json file: {fullPath}");

            return LoadManifest(fullPath);
        }

        if (!Directory.Exists(fullPath))
            throw new DirectoryNotFoundException($"Project path not found: {fullPath}");

        var directory = new DirectoryInfo(fullPath);
        while (directory is not null) {
            string manifestPath = Path.Combine(directory.FullName, "xamlnexus.json");
            if (File.Exists(manifestPath)) return LoadManifest(manifestPath);
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not find xamlnexus.json from '{fullPath}' or any parent directory.");
    }

    private static XamlNexusProjectContext LoadManifest(string manifestPath) {
        string fullManifestPath = Path.GetFullPath(manifestPath);
        ProjectPathSafety.EnsureNoLinks(fullManifestPath);
        string root = Path.GetDirectoryName(fullManifestPath)
            ?? throw new InvalidOperationException($"Manifest has no parent directory: {fullManifestPath}");
        return new XamlNexusProjectContext(
            root,
            fullManifestPath,
            XamlNexusProjectManifestStore.Load(fullManifestPath));
    }
}

public enum ProjectValidationSeverity {
    Error,
    Warning,
}

public sealed record ProjectValidationIssue(
    ProjectValidationSeverity Severity,
    string Code,
    string Message,
    string? RelativePath = null);

public sealed record XamlNexusProjectValidationReport(
    string RootDirectory,
    IReadOnlyList<ProjectValidationIssue> Issues) {
    public bool IsValid => Issues.All(issue => issue.Severity != ProjectValidationSeverity.Error);
}

public static class XamlNexusProjectValidator {
    public static XamlNexusProjectValidationReport Validate(XamlNexusProjectContext context) {
        ArgumentNullException.ThrowIfNull(context);

        var issues = new List<ProjectValidationIssue>();
        var checkedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string projectName = context.Manifest.Project.Name;

        RequireFile($"{projectName}.{context.Manifest.Project.SolutionFormat}", ProjectValidationErrors.SolutionMissing);
        RequireFile("Directory.Build.props", ProjectValidationErrors.VersionPropertiesMissing);
        RequireFile($"{projectName}.UI/{projectName}.UI.csproj", ProjectValidationErrors.WinuiProjectMissing);
        RequireFile($"{projectName}.Common/{projectName}.Common.csproj", ProjectValidationErrors.CommonProjectMissing);

        if (context.Manifest.Project.Preset == "hybrid")
            RequireFile($"{projectName}/{projectName}.csproj", ProjectValidationErrors.HybridHostMissing);

        foreach (XamlNexusManagedModule module in context.Manifest.Modules) {
            if (module.Source == "recipe") {
                ValidateRecipeFiles(module);
                continue;
            }

            switch (module.Id.ToLowerInvariant()) {
                case "app-shell":
                    RequireFile($"{projectName}.UI/MainWindow.xaml", ProjectValidationErrors.ShellMissing);
                    break;
                case "arcxaml":
                    RequireFile($"{projectName}.UIComponent/{projectName}.UIComponent.csproj", ProjectValidationErrors.UiComponentsMissing);
                    break;
                case "localization":
                    RequireFile($"{projectName}.UIComponent/Strings/en-US/Resources.resw", ProjectValidationErrors.EnglishResourcesMissing);
                    RequireFile($"{projectName}.UIComponent/Strings/zh-CN/Resources.resw", ProjectValidationErrors.ChineseResourcesMissing);
                    break;
                case "logging":
                    RequireFile($"{projectName}.Common/Logging/ArcLog.cs", ProjectValidationErrors.LoggingMissing);
                    break;
                case "settings":
                    RequireFile($"{projectName}.AppSettingsPanel/{projectName}.AppSettingsPanel.csproj", ProjectValidationErrors.SettingsMissing);
                    break;
                case "single-instance":
                    RequireFile($"{projectName}.UI/App.xaml.cs", ProjectValidationErrors.SingleInstanceMissing);
                    break;
                case "updater":
                    RequireFile($"{projectName}.Common/Updates/AppUpdateLifecycle.cs", ProjectValidationErrors.UpdateLifecycleMissing);
                    RequireFile($"{projectName}.Common/Updates/VerifiedUpdateDownloader.cs", ProjectValidationErrors.UpdateDownloaderMissing);
                    break;
                case "github-release":
                    if (!File.Exists(Path.Combine(context.RootDirectory, "eng", "publishing", "release.json")))
                        RequireFile(".github/release.json", ProjectValidationErrors.ReleaseConfigMissing);
                    RequireFile("eng/publishing/Build-Installer.ps1", ProjectValidationErrors.InstallerScriptMissing);
                    break;
                case "autostart":
                    RequireFile($"{projectName}/Utils/WindowsAutoStart.cs", ProjectValidationErrors.AutostartMissing);
                    break;
                case "background-host":
                    RequireFile($"{projectName}/{projectName}.csproj", ProjectValidationErrors.BackgroundHostMissing);
                    break;
                case "named-pipe-grpc":
                    RequireFile($"{projectName}.Grpc.Client/{projectName}.Grpc.Client.csproj", ProjectValidationErrors.GrpcClientMissing);
                    RequireFile($"{projectName}.Grpc.Service/{projectName}.Grpc.Service.csproj", ProjectValidationErrors.GrpcServiceMissing);
                    break;
                case "system-tray":
                    RequireFile($"{projectName}/MainWindow.xaml", ProjectValidationErrors.TrayWindowMissing);
                    RequireFile($"{projectName}/MainWindow.xaml.cs", ProjectValidationErrors.TrayWindowCodeMissing);
                    break;
                default:
                    issues.Add(new ProjectValidationIssue(
                        ProjectValidationSeverity.Warning,
                        ProjectValidationErrors.ValidatorUnavailable.Code,
                        ProjectValidationErrors.ValidatorUnavailable.GetMessage(module.Id)));
                    break;
            }
        }

        if (context.Manifest.ScaffoldFiles is not null) {
            foreach (XamlNexusManagedFile file in context.Manifest.ScaffoldFiles) {
                if (file.UserEditable) continue;
                string normalizedPath = file.Path.Replace('/', Path.DirectorySeparatorChar);
                string fullPath = Path.Combine(context.RootDirectory, normalizedPath);
                if (!File.Exists(fullPath)) {
                    issues.Add(new ProjectValidationIssue(
                        ProjectValidationSeverity.Error,
                        ProjectValidationErrors.ScaffoldFileMissing.Code,
                        ProjectValidationErrors.ScaffoldFileMissing.GetMessage(),
                        file.Path));
                }
                else if (!ComputeSha256(fullPath).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase)) {
                    issues.Add(new ProjectValidationIssue(
                        ProjectValidationSeverity.Warning,
                        ProjectValidationErrors.ScaffoldFileChanged.Code,
                        ProjectValidationErrors.ScaffoldFileChanged.GetMessage(),
                        file.Path));
                }
            }
        }

        return new XamlNexusProjectValidationReport(context.RootDirectory, issues);

        void ValidateRecipeFiles(XamlNexusManagedModule module) {
            if (module.Files is null) return;
            foreach (XamlNexusManagedFile file in module.Files) {
                string normalizedPath = file.Path.Replace('/', Path.DirectorySeparatorChar);
                string fullPath = Path.Combine(context.RootDirectory, normalizedPath);
                if (!File.Exists(fullPath)) {
                    issues.Add(new ProjectValidationIssue(
                        ProjectValidationSeverity.Error,
                        ProjectValidationErrors.RecipeFileMissing.Code,
                        ProjectValidationErrors.RecipeFileMissing.GetMessage(module.Id),
                        file.Path));
                }
                else if (!ComputeSha256(fullPath).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase)) {
                    issues.Add(new ProjectValidationIssue(
                        ProjectValidationSeverity.Warning,
                        ProjectValidationErrors.RecipeFileChanged.Code,
                        ProjectValidationErrors.RecipeFileChanged.GetMessage(module.Id),
                        file.Path));
                }
            }
        }

        void RequireFile(string relativePath, Resources.ErrorDefinition definition) {
            string normalizedPath = relativePath.Replace('/', Path.DirectorySeparatorChar);
            if (!checkedPaths.Add(normalizedPath)) return;
            if (!File.Exists(Path.Combine(context.RootDirectory, normalizedPath))) {
                issues.Add(new ProjectValidationIssue(
                    ProjectValidationSeverity.Error,
                    definition.Code,
                    definition.GetMessage(),
                    relativePath));
            }
        }
    }

    private static string ComputeSha256(string path) {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)).ToLowerInvariant();
    }
}
