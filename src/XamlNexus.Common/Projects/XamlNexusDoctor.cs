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

public sealed record XamlNexusDoctorReport(string RootDirectory, DateTimeOffset GeneratedAtUtc, IReadOnlyList<XamlNexusDoctorCheck> Checks) {
    public bool IsHealthy => Checks.All(check => check.Severity != XamlNexusDoctorSeverity.Error);

    public int ErrorCount => Checks.Count(check => check.Severity == XamlNexusDoctorSeverity.Error);

    public int WarningCount => Checks.Count(check => check.Severity == XamlNexusDoctorSeverity.Warning);
}

public static partial class XamlNexusDoctor {
    public static XamlNexusDoctorReport Diagnose(XamlNexusProjectContext context, IXamlNexusRecipeCatalog recipeCatalog, bool probeEnvironment = true) {
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

    private static void DiagnoseEnvironment(string rootDirectory, List<XamlNexusDoctorCheck> checks) {
        checks.Add(OperatingSystem.IsWindows()
            ? Pass("XD1001", "Environment", string.Format(LanguageRegistry.GetText("DoctorWindows"), Environment.OSVersion.Version))
            : Error("XD1001", "Environment", LanguageRegistry.GetText("DoctorRequiresWindows")));

        try {
            // Resolve from the project directory so global.json and SDK roll-forward apply.
            ShellExecutionResult sdkResult = ShellExecutor.Run("dotnet", "--version", rootDirectory, timeoutMilliseconds: 15000);
            if (!sdkResult.Success) {
                checks.Add(Error("XD1002", "Environment",
                    string.Format(LanguageRegistry.GetText("DoctorSdkResolveFailed"), sdkResult.DiagnosticOutput)));
            }
            else {
                string selected = sdkResult.StandardOutput.Trim();
                checks.Add(Version.TryParse(selected.Split('-')[0], out Version? version) && version.Major >= 8
                    ? Pass("XD1002", "Environment", string.Format(LanguageRegistry.GetText("DoctorSdkSelected"), selected))
                    : Error("XD1002", "Environment", string.Format(LanguageRegistry.GetText("DoctorSdkUnsupported"), selected)));
            }
        }
        catch (Exception exception) when (
            exception is System.ComponentModel.Win32Exception or InvalidOperationException) {
            checks.Add(Error("XD1002", "Environment", string.Format(LanguageRegistry.GetText("DoctorDotnetFailed"), exception.Message)));
        }

        try {
            ShellExecutionResult nugetResult = ShellExecutor.Run(
                "dotnet",
                "nuget list source --format Short",
                rootDirectory, timeoutMilliseconds: 15000);
            if (!nugetResult.Success) {
                checks.Add(Warning(
                    "XD1003",
                    "Environment",
                    string.Format(LanguageRegistry.GetText("DoctorNugetFailed"), nugetResult.DiagnosticOutput)));
            }
            else {
                int enabledSources = nugetResult.StandardOutput
                    .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                    .Count(line => line.TrimStart().StartsWith("E ", StringComparison.OrdinalIgnoreCase));
                checks.Add(enabledSources > 0
                    ? Pass("XD1003", "Environment", string.Format(LanguageRegistry.GetText("DoctorNugetSources"), enabledSources))
                    : Warning("XD1003", "Environment", LanguageRegistry.GetText("DoctorNugetMissing")));
            }
        }
        catch (Exception exception) when (
            exception is System.ComponentModel.Win32Exception or InvalidOperationException) {
            checks.Add(Warning("XD1003", "Environment", string.Format(LanguageRegistry.GetText("DoctorNugetFailed"), exception.Message)));
        }
    }

    /// <summary>
    /// 复用项目校验器检查清单和受管理的项目结构，再将校验问题转换成统一的诊断结果。
    /// 无问题时添加一条 Pass；有问题时逐条保留原编号、说明和文件路径。
    /// </summary>
    /// <param name="context">待校验的项目上下文。</param>
    /// <param name="checks">收集诊断结果，不修改项目文件。</param>
    private static void DiagnoseProject(XamlNexusProjectContext context, List<XamlNexusDoctorCheck> checks) {
        // 具体校验规则由校验器维护，doctor 负责汇总，避免维护两套项目结构检查逻辑。
        XamlNexusProjectValidationReport validation = XamlNexusProjectValidator.Validate(context);
        if (validation.Issues.Count == 0) {
            checks.Add(Pass("XD2001", "Project", "Manifest and managed project structure are consistent."));
            return;
        }

        foreach (ProjectValidationIssue issue in validation.Issues) {
            // 将校验器的严重程度转换为 doctor 的枚举；发现问题后仍继续收集后续项。
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

    /// <summary>
    /// 静态读取项目 XML，检查 Windows 目标框架和 Windows App SDK 版本声明。
    /// 不执行还原或构建，也不验证本机是否安装运行时；不展开 MSBuild 属性、条件和导入文件。
    /// </summary>
    /// <param name="context">提供待检查项目的根目录。</param>
    /// <param name="checks">收集检查结果，不清空已有结果。</param>
    private static void DiagnoseWindowsAppSdk(XamlNexusProjectContext context, List<XamlNexusDoctorCheck> checks) {
        // 对版本声明去重；同一版本被多个项目引用只计为一种版本。
        var versions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int windowsProjects = 0;

        foreach (string projectPath in EnumerateProjectFiles(context.RootDirectory)) {
            try {
                // 枚举辅助方法会过滤 bin/obj 下的项目；LocalName 可兼容带 XML 命名空间的元素。
                XDocument project = XDocument.Load(projectPath);
                // 一个项目即使声明多个 Windows 目标框架，也只累计一次。
                if (project.Descendants().Any(element =>
                        element.Name.LocalName is "TargetFramework" or "TargetFrameworks" &&
                        element.Value.Contains("-windows", StringComparison.OrdinalIgnoreCase))) {
                    windowsProjects++;
                }
                // 只检查当前 csproj 中直接声明的 PackageReference，不解析传递依赖或集中版本管理。
                foreach (XElement reference in project.Descendants().Where(element =>
                             element.Name.LocalName == "PackageReference" &&
                             string.Equals(
                                 (string?)element.Attribute("Include"),
                                 "Microsoft.WindowsAppSDK",
                                 StringComparison.OrdinalIgnoreCase))) {
                    // 同时支持 Version 属性和 Version 子元素；未声明版本的引用不会加入集合。
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

        // 无可读取的版本声明时报错；一种版本通过；多种版本仅提示可能需要统一。
        // 因此集中管理版本等写法可能无法被此静态检查正确识别。
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

    /// <summary>
    /// 将清单中通过 Recipe 安装的模块与当前目录中的定义比较，报告缺失定义或版本差异。
    /// 不查询远程版本，不执行安装或更新，也不在此处验证模块运行行为。
    /// </summary>
    /// <param name="context">提供已安装模块的项目清单。</param>
    /// <param name="recipeCatalog">本次诊断使用的 Recipe 目录。</param>
    /// <param name="checks">收集每个 Recipe 的检查结果。</param>
    private static void DiagnoseRecipes(
        XamlNexusProjectContext context,
        IXamlNexusRecipeCatalog recipeCatalog,
        ICollection<XamlNexusDoctorCheck> checks) {
        // 模板自带模块的 Source 为 template，不参与 Recipe 目录版本比较。
        XamlNexusManagedModule[] installedRecipes = context.Manifest.Modules
            .Where(module => module.Source.Equals("recipe", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (installedRecipes.Length == 0) {
            checks.Add(Pass("XD4001", "Recipes", "No Recipe modules are installed."));
            return;
        }

        foreach (XamlNexusManagedModule module in installedRecipes) {
            // 找不到定义只给 Warning：当前目录未包含该 Recipe，不等于已安装内容一定无效。
            IXamlNexusRecipe? catalogRecipe = recipeCatalog.Find(module.Id);
            if (catalogRecipe is null) {
                checks.Add(Warning(
                    "XD4002",
                    "Recipes",
                    $"Installed Recipe '{module.Id}' is not available in the active Catalog."));
            }
            // 这里只比较版本字符串是否相同，不判断哪个版本更新。
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

    /// <summary>
    /// 在声明了 sqlite 模块时检查数据库所属项目的引用、WAL 相关源码和 hybrid 的协议集成。
    /// 不打开数据库，不执行 SQL，也不验证实际日志模式或 gRPC 通信是否可用。
    /// </summary>
    /// <param name="context">提供架构预设、项目名称、模块清单和根目录。</param>
    /// <param name="checks">收集 SQLite 集成的检查结果。</param>
    private static void DiagnoseSqlite(
        XamlNexusProjectContext context,
        ICollection<XamlNexusDoctorCheck> checks) {
        // SQLite 是可选能力，未安装时跳过，不视为问题。
        if (!context.Manifest.Modules.Any(module => module.Id.Equals("sqlite", StringComparison.OrdinalIgnoreCase)))
            return;

        string name = context.Manifest.Project.Name;
        string dataProject = Path.Combine(context.RootDirectory, $"{name}.Data", $"{name}.Data.csproj");
        // hybrid 由后台宿主拥有数据库；纯 WinUI 则由 UI 进程拥有数据库。
        string hostProject = context.Manifest.Project.Preset == "hybrid"
            ? Path.Combine(context.RootDirectory, name, $"{name}.csproj")
            : Path.Combine(context.RootDirectory, $"{name}.UI", $"{name}.UI.csproj");
        // 辅助方法将 Include 相对路径转为绝对路径后比较，确认引用指向预期 Data 项目。
        bool hasReference = HasProjectItem(hostProject, "ProjectReference", dataProject);
        checks.Add(hasReference
            ? Pass("XD5001", "SQLite", "The database-owning process references the Data project.")
            : Error("XD5001", "SQLite", "The database-owning process does not reference the Data project."));

        string databaseService = Path.Combine(context.RootDirectory, $"{name}.Data", "Persistence", "SqliteDatabase.cs");
        // 当前实现遇到源码缺失会直接结束此方法，包括后面的 hybrid 协议检查。
        if (!File.Exists(databaseService)) return;
        string serviceText = File.ReadAllText(databaseService);
        // 仅匹配两种 WAL 配置文本：匹配成功不代表代码实际执行，未匹配也可能是自定义初始化。
        bool hasWalText = serviceText.Contains("journal_mode=WAL", StringComparison.OrdinalIgnoreCase) ||
            serviceText.Contains("journal_mode = WAL", StringComparison.OrdinalIgnoreCase);
        checks.Add(hasWalText
            ? Pass("XD5002", "SQLite", "WAL-related text was found in the database service source; the runtime journal mode has not been verified.")
            : Warning("XD5002", "SQLite", "WAL initialization could not be confirmed from the database service source. Custom initialization or another journal mode is allowed; verify the runtime configuration if needed."));

        if (context.Manifest.Project.Preset == "hybrid") {
            // 检查服务项目是否把 app_state.proto 声明为 Protobuf 项，不验证协议内容或生成结果。
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

    /// <summary>
    /// 检查 GitHub 发布能力声明和发布配置中的必填字符串字段。
    /// 不执行发布，不验证凭据、远程仓库、字段指向的文件是否存在或 RID 是否有效。
    /// </summary>
    /// <param name="context">提供模块清单和配置文件所在的项目根目录。</param>
    /// <param name="checks">收集发布配置的检查结果。</param>
    private static void DiagnosePublishing(
        XamlNexusProjectContext context,
        ICollection<XamlNexusDoctorCheck> checks) {
        if (!context.Manifest.Modules.Any(module =>
                module.Id.Equals("github-release", StringComparison.OrdinalIgnoreCase))) {
            checks.Add(Warning("XD6001", "Publishing", "The GitHub release module is not declared."));
            return;
        }

        // 优先读取现行配置位置；文件不存在时兼容旧版 .github/release.json。
        string configPath = Path.Combine(context.RootDirectory, "eng", "publishing", "release.json");
        if (!File.Exists(configPath)) configPath = Path.Combine(context.RootDirectory, ".github", "release.json");
        try {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(configPath));
            string[] required = ["solution", "versionFile", "project", "executable", "runtimeIdentifier"];
            // 字段缺失、不是字符串或仅含空白，都按缺少有效值报告。
            string[] missing = required
                .Where(name => !document.RootElement.TryGetProperty(name, out JsonElement value) ||
                    value.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(value.GetString()))
                .ToArray();
            checks.Add(missing.Length == 0
                ? Pass("XD6002", "Publishing", "Release metadata contains all required fields.")
                : Error("XD6002", "Publishing", $"Release metadata is missing: {string.Join(", ", missing)}.", Path.GetRelativePath(context.RootDirectory, configPath)));
        }
        // 将文件读取或 JSON 语法错误记录为诊断项，交由报告统一展示。
        catch (Exception exception) when (exception is IOException or JsonException) {
            checks.Add(Error("XD6002", "Publishing", $"Release metadata is invalid: {exception.Message}", Path.GetRelativePath(context.RootDirectory, configPath)));
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
