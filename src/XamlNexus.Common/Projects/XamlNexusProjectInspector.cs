namespace XamlNexus.Common.Projects;

public sealed record XamlNexusProjectContext(
    string RootDirectory,
    string ManifestPath,
    XamlNexusProjectManifest Manifest);

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

        RequireFile($"{projectName}.{context.Manifest.Project.SolutionFormat}", "XN1001", "Solution file is missing.");
        RequireFile("Directory.Build.props", "XN1002", "Shared version properties are missing.");
        RequireFile($"{projectName}.UI/{projectName}.UI.csproj", "XN1003", "WinUI application project is missing.");
        RequireFile($"{projectName}.Common/{projectName}.Common.csproj", "XN1004", "Common project is missing.");

        if (context.Manifest.Project.Preset == "hybrid")
            RequireFile($"{projectName}/{projectName}.csproj", "XN1005", "Hybrid background host project is missing.");

        foreach (XamlNexusManagedModule module in context.Manifest.Modules) {
            if (module.Source == "recipe") {
                ValidateRecipeFiles(module);
                continue;
            }

            switch (module.Id.ToLowerInvariant()) {
                case "app-shell":
                    RequireFile($"{projectName}.UI/MainWindow.xaml", "XN1101", "Application shell is missing.");
                    break;
                case "arcxaml":
                    RequireFile($"{projectName}.UIComponent/{projectName}.UIComponent.csproj", "XN1102", "ArcXaml UI component project is missing.");
                    break;
                case "localization":
                    RequireFile($"{projectName}.UIComponent/Strings/en-US/Resources.resw", "XN1103", "English localization resources are missing.");
                    RequireFile($"{projectName}.UIComponent/Strings/zh-CN/Resources.resw", "XN1104", "Chinese localization resources are missing.");
                    break;
                case "logging":
                    RequireFile($"{projectName}.Common/Logging/ArcLog.cs", "XN1105", "Logging infrastructure is missing.");
                    break;
                case "settings":
                    RequireFile($"{projectName}.AppSettingsPanel/{projectName}.AppSettingsPanel.csproj", "XN1106", "Settings panel project is missing.");
                    break;
                case "single-instance":
                    RequireFile($"{projectName}.UI/App.xaml.cs", "XN1107", "Single-instance application entry point is missing.");
                    break;
                case "updater":
                    RequireFile($"{projectName}.Common/Updates/AppUpdateLifecycle.cs", "XN1108", "Update lifecycle is missing.");
                    RequireFile($"{projectName}.Common/Updates/VerifiedUpdateDownloader.cs", "XN1109", "Verified update downloader is missing.");
                    break;
                case "github-release":
                    RequireFile(".github/release.json", "XN1110", "Release configuration is missing.");
                    RequireFile(".github/workflows/release-merged-pull-request.yml", "XN1111", "Release workflow is missing.");
                    RequireFile("eng/publishing/Build-Installer.ps1", "XN1112", "Installer build script is missing.");
                    break;
                case "autostart":
                    RequireFile($"{projectName}/Utils/WindowsAutoStart.cs", "XN1113", "Autostart integration is missing.");
                    break;
                case "background-host":
                    RequireFile($"{projectName}/{projectName}.csproj", "XN1114", "Background host project is missing.");
                    break;
                case "named-pipe-grpc":
                    RequireFile($"{projectName}.Grpc.Client/{projectName}.Grpc.Client.csproj", "XN1115", "Named Pipe gRPC client project is missing.");
                    RequireFile($"{projectName}.Grpc.Service/{projectName}.Grpc.Service.csproj", "XN1116", "Named Pipe gRPC service project is missing.");
                    break;
                case "system-tray":
                    RequireFile($"{projectName}/MainWindow.xaml", "XN1117", "System tray host window is missing.");
                    RequireFile($"{projectName}/MainWindow.xaml.cs", "XN1117", "System tray host window code is missing.");
                    break;
                default:
                    issues.Add(new ProjectValidationIssue(
                        ProjectValidationSeverity.Warning,
                        "XN2001",
                        $"No structural validator is registered for module '{module.Id}'."));
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
                        "XN1301",
                        "A scaffold-managed file is missing.",
                        file.Path));
                }
                else if (!ComputeSha256(fullPath).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase)) {
                    issues.Add(new ProjectValidationIssue(
                        ProjectValidationSeverity.Warning,
                        "XN1302",
                        "Template file differs from its generated baseline. Customization is allowed; automatic upgrade may require resolving these changes.",
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
                        "XN1201",
                        $"Recipe module '{module.Id}' owns a file that is missing.",
                        file.Path));
                }
                else if (!ComputeSha256(fullPath).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase)) {
                    issues.Add(new ProjectValidationIssue(
                        ProjectValidationSeverity.Warning,
                        "XN1202",
                        $"Recipe '{module.Id}' file differs from its generated baseline. Customization is allowed; automatic update or removal may require resolving these changes.",
                        file.Path));
                }
            }
        }

        void RequireFile(string relativePath, string code, string message) {
            string normalizedPath = relativePath.Replace('/', Path.DirectorySeparatorChar);
            if (!checkedPaths.Add(normalizedPath)) return;
            if (!File.Exists(Path.Combine(context.RootDirectory, normalizedPath))) {
                issues.Add(new ProjectValidationIssue(
                    ProjectValidationSeverity.Error,
                    code,
                    message,
                    relativePath));
            }
        }
    }

    private static string ComputeSha256(string path) {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)).ToLowerInvariant();
    }
}
