using System.Text.Json;
using System.Xml.Linq;
using XamlNexus.Common.Recipes;
using XamlNexus.Common.Utils;

namespace XamlNexus.Common.Projects;

public enum XamlNexusDoctorSeverity {
    Pass,
    Warning,
    Error,
}

public sealed record XamlNexusDoctorCheck(
    XamlNexusDoctorSeverity Severity,
    string Code,
    string Category,
    string Message,
    string? RelativePath = null);

public sealed record XamlNexusDoctorReport(
    string RootDirectory,
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<XamlNexusDoctorCheck> Checks) {
    public bool IsHealthy => Checks.All(check => check.Severity != XamlNexusDoctorSeverity.Error);

    public int ErrorCount => Checks.Count(check => check.Severity == XamlNexusDoctorSeverity.Error);

    public int WarningCount => Checks.Count(check => check.Severity == XamlNexusDoctorSeverity.Warning);
}

public static class XamlNexusDoctor {
    public static XamlNexusDoctorReport Diagnose(
        XamlNexusProjectContext context,
        IXamlNexusRecipeCatalog recipeCatalog,
        bool probeEnvironment = true) {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(recipeCatalog);

        var checks = new List<XamlNexusDoctorCheck>();
        if (probeEnvironment) DiagnoseEnvironment(context.RootDirectory, checks);
        DiagnoseProject(context, checks);
        DiagnoseWindowsAppSdk(context, checks);
        DiagnoseRecipes(context, recipeCatalog, checks);
        DiagnoseSqlite(context, checks);
        DiagnosePublishing(context, checks);
        return new XamlNexusDoctorReport(context.RootDirectory, DateTimeOffset.UtcNow, checks);
    }

    private static void DiagnoseEnvironment(
        string rootDirectory,
        ICollection<XamlNexusDoctorCheck> checks) {
        checks.Add(OperatingSystem.IsWindows()
            ? Pass("XD1001", "Environment", $"Windows {Environment.OSVersion.Version} detected.")
            : Error("XD1001", "Environment", "XamlNexus generated applications require Windows."));

        try {
            // Resolve from the project directory so global.json and SDK roll-forward apply.
            ShellExecutionResult sdkResult = ShellExecutor.Run("dotnet", "--version", rootDirectory);
            if (!sdkResult.Success) {
                checks.Add(Error("XD1002", "Environment",
                    $"Unable to resolve the .NET SDK for this project. Check global.json and the installed SDKs: {sdkResult.DiagnosticOutput}"));
            }
            else {
                string selected = sdkResult.StandardOutput.Trim();
                checks.Add(Version.TryParse(selected.Split('-')[0], out Version? version) && version.Major >= 8
                    ? Pass("XD1002", "Environment", $"Project-selected .NET SDK: {selected}.")
                    : Error("XD1002", "Environment", $"Project-selected SDK '{selected}' is unsupported. .NET SDK 8.0 or newer is required; check global.json."));
            }
        }
        catch (Exception exception) when (
            exception is System.ComponentModel.Win32Exception or InvalidOperationException) {
            checks.Add(Error("XD1002", "Environment", $"The dotnet SDK command could not start: {exception.Message}"));
        }

        try {
            ShellExecutionResult nugetResult = ShellExecutor.Run(
                "dotnet",
                "nuget list source --format Short",
                rootDirectory);
            if (!nugetResult.Success) {
                checks.Add(Warning(
                    "XD1003",
                    "Environment",
                    $"Unable to inspect NuGet sources: {nugetResult.DiagnosticOutput}"));
            }
            else {
                int enabledSources = nugetResult.StandardOutput
                    .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                    .Count(line => line.TrimStart().StartsWith("E ", StringComparison.OrdinalIgnoreCase));
                checks.Add(enabledSources > 0
                    ? Pass("XD1003", "Environment", $"{enabledSources} NuGet source(s) enabled.")
                    : Warning("XD1003", "Environment", "No enabled NuGet source was detected."));
            }
        }
        catch (Exception exception) when (
            exception is System.ComponentModel.Win32Exception or InvalidOperationException) {
            checks.Add(Warning("XD1003", "Environment", $"NuGet sources could not be inspected: {exception.Message}"));
        }
    }

    private static void DiagnoseProject(
        XamlNexusProjectContext context,
        ICollection<XamlNexusDoctorCheck> checks) {
        XamlNexusProjectValidationReport validation = XamlNexusProjectValidator.Validate(context);
        if (validation.Issues.Count == 0) {
            checks.Add(Pass("XD2001", "Project", "Manifest and managed project structure are consistent."));
            return;
        }

        foreach (ProjectValidationIssue issue in validation.Issues) {
            checks.Add(new XamlNexusDoctorCheck(
                issue.Severity == ProjectValidationSeverity.Error
                    ? XamlNexusDoctorSeverity.Error
                    : XamlNexusDoctorSeverity.Warning,
                issue.Code,
                "Project",
                issue.Message,
                issue.RelativePath));
        }
    }

    private static void DiagnoseWindowsAppSdk(
        XamlNexusProjectContext context,
        ICollection<XamlNexusDoctorCheck> checks) {
        var versions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int windowsProjects = 0;
        foreach (string projectPath in EnumerateProjectFiles(context.RootDirectory)) {
            try {
                XDocument project = XDocument.Load(projectPath);
                if (project.Descendants().Any(element =>
                        element.Name.LocalName is "TargetFramework" or "TargetFrameworks" &&
                        element.Value.Contains("-windows", StringComparison.OrdinalIgnoreCase))) {
                    windowsProjects++;
                }
                foreach (XElement reference in project.Descendants().Where(element =>
                             element.Name.LocalName == "PackageReference" &&
                             string.Equals(
                                 (string?)element.Attribute("Include"),
                                 "Microsoft.WindowsAppSDK",
                                 StringComparison.OrdinalIgnoreCase))) {
                    string? version = (string?)reference.Attribute("Version") ??
                        reference.Elements().FirstOrDefault(element => element.Name.LocalName == "Version")?.Value;
                    if (!string.IsNullOrWhiteSpace(version)) versions.Add(version);
                }
            }
            catch (Exception exception) when (exception is IOException or System.Xml.XmlException) {
                checks.Add(Warning(
                    "XD3001",
                    "Windows App SDK",
                    $"Could not inspect project XML: {exception.Message}",
                    Path.GetRelativePath(context.RootDirectory, projectPath)));
            }
        }

        checks.Add(windowsProjects > 0
            ? Pass("XD3002", "Windows App SDK", $"{windowsProjects} Windows-targeted project(s) detected.")
            : Error("XD3002", "Windows App SDK", "No Windows-targeted project was detected."));
        if (versions.Count == 0) {
            checks.Add(Error("XD3003", "Windows App SDK", "Microsoft.WindowsAppSDK is not referenced."));
        }
        else if (versions.Count == 1) {
            checks.Add(Pass("XD3003", "Windows App SDK", $"Microsoft.WindowsAppSDK {versions.Single()} is referenced."));
        }
        else {
            checks.Add(Warning(
                "XD3003",
                "Windows App SDK",
                $"Multiple Microsoft.WindowsAppSDK versions are referenced: {string.Join(", ", versions.Order())}."));
        }
    }

    private static void DiagnoseRecipes(
        XamlNexusProjectContext context,
        IXamlNexusRecipeCatalog recipeCatalog,
        ICollection<XamlNexusDoctorCheck> checks) {
        XamlNexusManagedModule[] installedRecipes = context.Manifest.Modules
            .Where(module => module.Source.Equals("recipe", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (installedRecipes.Length == 0) {
            checks.Add(Pass("XD4001", "Recipes", "No Recipe modules are installed."));
            return;
        }

        foreach (XamlNexusManagedModule module in installedRecipes) {
            IXamlNexusRecipe? catalogRecipe = recipeCatalog.Find(module.Id);
            if (catalogRecipe is null) {
                checks.Add(Warning(
                    "XD4002",
                    "Recipes",
                    $"Installed Recipe '{module.Id}' is not available in the active Catalog."));
            }
            else if (module.Version.Equals(catalogRecipe.Descriptor.Version, StringComparison.OrdinalIgnoreCase)) {
                checks.Add(Pass("XD4003", "Recipes", $"Recipe '{module.Id}' is at Catalog version {module.Version}."));
            }
            else {
                checks.Add(Warning(
                    "XD4004",
                    "Recipes",
                    $"Recipe '{module.Id}' is installed at {module.Version}; Catalog provides {catalogRecipe.Descriptor.Version}."));
            }
        }
    }

    private static void DiagnoseSqlite(
        XamlNexusProjectContext context,
        ICollection<XamlNexusDoctorCheck> checks) {
        if (!context.Manifest.Modules.Any(module => module.Id.Equals("sqlite", StringComparison.OrdinalIgnoreCase)))
            return;

        string name = context.Manifest.Project.Name;
        string dataProject = Path.Combine(context.RootDirectory, $"{name}.Data", $"{name}.Data.csproj");
        string hostProject = context.Manifest.Project.Preset == "hybrid"
            ? Path.Combine(context.RootDirectory, name, $"{name}.csproj")
            : Path.Combine(context.RootDirectory, $"{name}.UI", $"{name}.UI.csproj");
        bool hasReference = HasProjectItem(hostProject, "ProjectReference", dataProject);
        checks.Add(hasReference
            ? Pass("XD5001", "SQLite", "The database-owning process references the Data project.")
            : Error("XD5001", "SQLite", "The database-owning process does not reference the Data project."));

        string databaseService = Path.Combine(context.RootDirectory, $"{name}.Data", "Persistence", "SqliteDatabase.cs");
        if (!File.Exists(databaseService)) return;
        string serviceText = File.ReadAllText(databaseService);
        bool hasWalText = serviceText.Contains("journal_mode=WAL", StringComparison.OrdinalIgnoreCase) ||
            serviceText.Contains("journal_mode = WAL", StringComparison.OrdinalIgnoreCase);
        checks.Add(hasWalText
            ? Pass("XD5002", "SQLite", "WAL-related text was found in the database service source; the runtime journal mode has not been verified.")
            : Warning("XD5002", "SQLite", "WAL initialization could not be confirmed from the database service source. Custom initialization or another journal mode is allowed; verify the runtime configuration if needed."));

        if (context.Manifest.Project.Preset == "hybrid") {
            string grpcProject = Path.Combine(
                context.RootDirectory,
                $"{name}.Grpc.Service",
                $"{name}.Grpc.Service.csproj");
            string proto = Path.Combine(
                context.RootDirectory,
                $"{name}.Grpc.Service",
                "Protos",
                "app_state.proto");
            checks.Add(HasProjectItem(grpcProject, "Protobuf", proto)
                ? Pass("XD5003", "SQLite", "Hybrid app-state Protobuf integration is present.")
                : Error("XD5003", "SQLite", "Hybrid app-state Protobuf integration is missing."));
        }
    }

    private static void DiagnosePublishing(
        XamlNexusProjectContext context,
        ICollection<XamlNexusDoctorCheck> checks) {
        if (!context.Manifest.Modules.Any(module =>
                module.Id.Equals("github-release", StringComparison.OrdinalIgnoreCase))) {
            checks.Add(Warning("XD6001", "Publishing", "The GitHub release module is not declared."));
            return;
        }

        string configPath = Path.Combine(context.RootDirectory, ".github", "release.json");
        try {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(configPath));
            string[] required = ["solution", "versionFile", "project", "executable", "runtimeIdentifier"];
            string[] missing = required
                .Where(name => !document.RootElement.TryGetProperty(name, out JsonElement value) ||
                    value.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(value.GetString()))
                .ToArray();
            checks.Add(missing.Length == 0
                ? Pass("XD6002", "Publishing", "Release metadata contains all required fields.")
                : Error("XD6002", "Publishing", $"Release metadata is missing: {string.Join(", ", missing)}.", ".github/release.json"));
        }
        catch (Exception exception) when (exception is IOException or JsonException) {
            checks.Add(Error("XD6002", "Publishing", $"Release metadata is invalid: {exception.Message}", ".github/release.json"));
        }
    }

    private static IEnumerable<string> EnumerateProjectFiles(string rootDirectory) =>
        Directory.EnumerateFiles(rootDirectory, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(rootDirectory, path)
                .Split(Path.DirectorySeparatorChar)
                .Any(segment =>
                segment.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("obj", StringComparison.OrdinalIgnoreCase)));

    private static bool HasProjectItem(string projectPath, string itemName, string expectedPath) {
        if (!File.Exists(projectPath)) return false;
        try {
            XDocument project = XDocument.Load(projectPath);
            return project.Descendants().Any(element => {
                if (element.Name.LocalName != itemName) return false;
                string? include = (string?)element.Attribute("Include");
                return !string.IsNullOrWhiteSpace(include) &&
                    Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectPath)!, include))
                        .Equals(expectedPath, StringComparison.OrdinalIgnoreCase);
            });
        }
        catch (Exception exception) when (exception is IOException or System.Xml.XmlException) {
            return false;
        }
    }

    private static XamlNexusDoctorCheck Pass(string code, string category, string message) =>
        new(XamlNexusDoctorSeverity.Pass, code, category, message);

    private static XamlNexusDoctorCheck Warning(
        string code,
        string category,
        string message,
        string? path = null) =>
        new(XamlNexusDoctorSeverity.Warning, code, category, message, path);

    private static XamlNexusDoctorCheck Error(
        string code,
        string category,
        string message,
        string? path = null) =>
        new(XamlNexusDoctorSeverity.Error, code, category, message, path);
}
