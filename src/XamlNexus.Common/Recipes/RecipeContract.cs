using System.Text;
using System.Text.RegularExpressions;
using XamlNexus.Common.Projects;

namespace XamlNexus.Common.Recipes;

/// <summary>Recipe 元数据，描述标识、版本、适用架构、依赖及互斥能力</summary>
public sealed class XamlNexusRecipeDescriptor {
    /// <summary>组件唯一 ID，要求使用小写 kebab-case，例如 system-tray</summary>
    public required string Id { get; init; }

    /// <summary>Recipe 自身的语义版本，与生成器版本分别管理</summary>
    public required string Version { get; init; }

    /// <summary>供命令行展示的组件名称</summary>
    public required string DisplayName { get; init; }

    /// <summary>供用户阅读的功能说明</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>支持的架构预设，只允许 winui、hybrid，至少包含一项</summary>
    public required IReadOnlyList<string> SupportedPresets { get; init; }

    /// <summary>安装前必须具备的模块 ID；依赖排序和自动补齐由上层规划器负责</summary>
    public IReadOnlyList<string> Dependencies { get; init; } = [];

    /// <summary>不能与当前 Recipe 同时存在的模块 ID</summary>
    public IReadOnlyList<string> Conflicts { get; init; } = [];
}

/// <summary>组件扩展契约：提供元数据和声明式安装计划，实际文件提交交给事务执行器</summary>
public interface IXamlNexusRecipe {
    XamlNexusRecipeDescriptor Descriptor { get; }

    XamlNexusRecipePlan CreatePlan(XamlNexusRecipeContext context);
}

/// <summary>可选卸载扩展，补充共享项目文件中的引用移除操作；自有文件由清单生成删除计划</summary>
public interface IXamlNexusRecipeRemovalPlanProvider {
    IReadOnlyList<XamlNexusRecipeProjectOperation> CreateRemovalOperations(
        XamlNexusRecipeContext context);
}

/// <summary>可选更新扩展，根据已安装版本生成共享项目文件的更新操作，避免重放安装时的新增操作</summary>
public interface IXamlNexusRecipeUpdatePlanProvider {
    IReadOnlyList<XamlNexusRecipeProjectOperation> CreateUpdateOperations(
        XamlNexusRecipeContext context,
        string installedVersion);
}

/// <summary>传给 Recipe 的项目根目录和清单；批量规划时根目录可能指向临时副本</summary>
public sealed record XamlNexusRecipeContext(
    string RootDirectory,
    XamlNexusProjectManifest Manifest);

/// <summary>安装或更新的声明式计划，分别描述文件内容变化和共享项目文件的结构修改</summary>
public sealed class XamlNexusRecipePlan {
    /// <summary>创建、替换或删除的文件列表；计划校验会拒绝同一路径出现多次</summary>
    public required IReadOnlyList<XamlNexusRecipeFileChange> Changes { get; init; }

    /// <summary>对 csproj、sln、slnx 的结构操作，由编辑器归并为带哈希前置条件的文件替换</summary>
    public IReadOnlyList<XamlNexusRecipeProjectOperation> ProjectOperations { get; init; } = [];
}

/// <summary>共享项目文件的结构操作基类；各派生记录只描述意图，不直接写文件</summary>
public abstract record XamlNexusRecipeProjectOperation;

/// <summary>新增 NuGet 包引用；不负责执行 dotnet restore，已有引用的冲突由编辑器检查</summary>
public sealed record AddPackageReferenceOperation(
    string ProjectPath,
    string PackageId,
    string Version) : XamlNexusRecipeProjectOperation;

/// <summary>确保包引用至少达到指定数字版本，保留已有更高版本；不自动处理条件引用和复杂版本表达式</summary>
public sealed record EnsurePackageReferenceOperation(
    string ProjectPath,
    string PackageId,
    string MinimumVersion) : XamlNexusRecipeProjectOperation;

/// <summary>在指定项目中添加对另一个项目的引用，路径相对于项目根目录提供</summary>
public sealed record AddProjectReferenceOperation(
    string ProjectPath,
    string ReferencedProjectPath) : XamlNexusRecipeProjectOperation;

/// <summary>从指定项目中移除匹配的项目引用，不删除被引用的项目文件</summary>
public sealed record RemoveProjectReferenceOperation(
    string ProjectPath,
    string ReferencedProjectPath) : XamlNexusRecipeProjectOperation;

/// <summary>把项目加入解决方案；支持引用同一计划中尚未落盘的新项目文件</summary>
public sealed record AddProjectToSolutionOperation(
    string SolutionPath,
    string ProjectPath,
    string? SolutionFolder = null) : XamlNexusRecipeProjectOperation;

/// <summary>从解决方案移除项目声明；传统 SLN 同时清理相关配置及嵌套关系</summary>
public sealed record RemoveProjectFromSolutionOperation(
    string SolutionPath,
    string ProjectPath) : XamlNexusRecipeProjectOperation;

/// <summary>向项目添加 Protobuf 编译项，指向指定 proto 文件</summary>
public sealed record AddProtobufOperation(
    string ProjectPath,
    string ProtoPath) : XamlNexusRecipeProjectOperation;

/// <summary>移除匹配的 Protobuf 编译项，文件删除由独立文件操作描述</summary>
public sealed record RemoveProtobufOperation(
    string ProjectPath,
    string ProtoPath) : XamlNexusRecipeProjectOperation;

/// <summary>文件级操作：新增、替换完整内容或删除</summary>
public enum XamlNexusRecipeFileChangeKind {
    Create,
    Replace,
    Delete,
}

/// <summary>一项待执行的文件变化，替换与删除必须携带当前内容的预期哈希</summary>
public sealed class XamlNexusRecipeFileChange {
    /// <summary>决定文件操作类型</summary>
    public required XamlNexusRecipeFileChangeKind Kind { get; init; }

    /// <summary>相对于项目根目录的文件路径，不允许越出根目录或直接修改清单</summary>
    public required string RelativePath { get; init; }

    /// <summary>创建或替换时写入的完整字节；删除时可以为空</summary>
    public byte[]? Content { get; init; }

    /// <summary>执行前文件必须匹配的 SHA-256，避免覆盖用户已修改的内容</summary>
    public string? ExpectedSha256 { get; init; }

    /// <summary>将文本编码为 UTF-8 并生成创建计划，不立即创建文件</summary>
    public static XamlNexusRecipeFileChange CreateText(string relativePath, string content) => new() {
        Kind = XamlNexusRecipeFileChangeKind.Create,
        RelativePath = relativePath,
        Content = Encoding.UTF8.GetBytes(content),
    };

    /// <summary>生成 UTF-8 文本替换计划，调用方提供预期的修改前哈希</summary>
    public static XamlNexusRecipeFileChange ReplaceText(
        string relativePath,
        string content,
        string expectedSha256) => new() {
        Kind = XamlNexusRecipeFileChangeKind.Replace,
        RelativePath = relativePath,
        Content = Encoding.UTF8.GetBytes(content),
        ExpectedSha256 = expectedSha256,
    };

    /// <summary>生成带哈希前置条件的删除计划</summary>
    public static XamlNexusRecipeFileChange Delete(
        string relativePath,
        string expectedSha256) => new() {
        Kind = XamlNexusRecipeFileChangeKind.Delete,
        RelativePath = relativePath,
        ExpectedSha256 = expectedSha256,
    };
}

/// <summary>携带稳定错误码的 Recipe 异常，可保留底层异常供诊断使用</summary>
public sealed class XamlNexusRecipeException : Exception {
    public XamlNexusRecipeException(RecipeErrorDefinition definition, object?[] arguments, Exception? innerException = null)
        : base($"{definition.Code}: {definition.Format(System.Globalization.CultureInfo.InvariantCulture, arguments)}", innerException) {
        Code = definition.Code;
        Definition = definition;
        Arguments = Array.AsReadOnly((object?[])arguments.Clone());
    }

    /// <summary>旧构造函数继续接受调用方自定义消息；其内容不进行自动翻译。</summary>
    public XamlNexusRecipeException(string code, string message)
        : base($"{code}: {message}") {
        Code = code;
    }

    public XamlNexusRecipeException(string code, string message, Exception innerException)
        : base($"{code}: {message}", innerException) {
        Code = code;
    }

    public string Code { get; }
    public RecipeErrorDefinition? Definition { get; }
    public string? ResourceKey => Definition?.ResourceKey;
    public IReadOnlyList<object?> Arguments { get; } = Array.Empty<object?>();

    /// <summary>Message 保持稳定英文诊断；仅在展示时根据显式语言解析资源。</summary>
    public string GetLocalizedMessage(System.Globalization.CultureInfo culture) => Definition is null
        ? Message : $"{Code}: {Definition.Format(culture, Arguments)}";
}

/// <summary>集中校验 Recipe 定义、安装兼容性和文件计划，防止各组件自行实现不一致的规则</summary>
public static partial class XamlNexusRecipeContract {
    /// <summary>校验元数据格式、支持架构，以及依赖和冲突列表的重复、自引用与交叉矛盾</summary>
    public static void ValidateDescriptor(XamlNexusRecipeDescriptor descriptor) {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (!IsValidId(descriptor.Id))
            throw new XamlNexusRecipeException(XamlNexusRecipeErrors.InvalidRecipeId, []);
        if (!SemanticVersionRegex().IsMatch(descriptor.Version))
            throw new XamlNexusRecipeException(XamlNexusRecipeErrors.InvalidRecipeVersion, []);
        if (string.IsNullOrWhiteSpace(descriptor.DisplayName))
            throw new XamlNexusRecipeException(XamlNexusRecipeErrors.MissingDisplayName, []);
        if (descriptor.SupportedPresets.Count == 0 ||
            descriptor.SupportedPresets.Any(preset => preset is not ("winui" or "hybrid"))) {
            throw new XamlNexusRecipeException(
                XamlNexusRecipeErrors.InvalidSupportedPresets, []);
        }

        ValidateIds(descriptor.Dependencies, "dependency");
        ValidateIds(descriptor.Conflicts, "conflict");

        var dependencies = descriptor.Dependencies.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var conflicts = descriptor.Conflicts.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (dependencies.Count != descriptor.Dependencies.Count)
            throw new XamlNexusRecipeException(XamlNexusRecipeErrors.DuplicateDependencies, []);
        if (conflicts.Count != descriptor.Conflicts.Count)
            throw new XamlNexusRecipeException(XamlNexusRecipeErrors.DuplicateConflicts, []);
        if (dependencies.Contains(descriptor.Id) || conflicts.Contains(descriptor.Id))
            throw new XamlNexusRecipeException(XamlNexusRecipeErrors.SelfDependencyOrConflict, []);
        if (dependencies.Overlaps(conflicts))
            throw new XamlNexusRecipeException(XamlNexusRecipeErrors.DependencyConflictOverlap, []);
    }

    /// <summary>检查架构、重复安装、缺失依赖和当前组件声明的冲突；不自动安装依赖</summary>
    public static void ValidateCompatibility(
        XamlNexusRecipeDescriptor descriptor,
        XamlNexusProjectManifest manifest) {
        ValidateDescriptor(descriptor);
        ArgumentNullException.ThrowIfNull(manifest);

        var installed = manifest.Modules
            .Select(module => module.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!descriptor.SupportedPresets.Contains(
                manifest.Project.Preset,
                StringComparer.OrdinalIgnoreCase)) {
            throw new XamlNexusRecipeException(
                XamlNexusRecipeErrors.UnsupportedPreset, [descriptor.Id, manifest.Project.Preset]);
        }
        if (installed.Contains(descriptor.Id))
            throw new XamlNexusRecipeException(XamlNexusRecipeErrors.AlreadyInstalled, [descriptor.Id]);

        string[] missingDependencies = descriptor.Dependencies
            .Where(dependency => !installed.Contains(dependency))
            .ToArray();
        if (missingDependencies.Length > 0) {
            throw new XamlNexusRecipeException(
                XamlNexusRecipeErrors.MissingDependencies, [string.Join(", ", missingDependencies)]);
        }

        string[] activeConflicts = descriptor.Conflicts
            .Where(installed.Contains)
            .ToArray();
        if (activeConflicts.Length > 0) {
            throw new XamlNexusRecipeException(
                XamlNexusRecipeErrors.ConflictingModules, [string.Join(", ", activeConflicts)]);
        }
    }

    /// <summary>解析绝对路径并检查文件存在性、链接、越界和哈希前置条件，再展开结构操作；不会提交写入</summary>
    internal static IReadOnlyList<ResolvedRecipeFileChange> ResolveAndValidatePlan(
        string rootDirectory,
        XamlNexusRecipePlan plan) {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(plan.Changes);

        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootDirectory));
        string rootPrefix = root + Path.DirectorySeparatorChar;
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var resolved = new List<ResolvedRecipeFileChange>();

        foreach (XamlNexusRecipeFileChange change in plan.Changes) {
            if (change is null)
                throw new XamlNexusRecipeException(XamlNexusRecipeErrors.EmptyFileChange, []);
            if (string.IsNullOrWhiteSpace(change.RelativePath) || Path.IsPathRooted(change.RelativePath))
                throw new XamlNexusRecipeException(XamlNexusRecipeErrors.FilePathMustBeRelative, []);

            string fullPath = Path.GetFullPath(Path.Combine(root, change.RelativePath));
            if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                throw new XamlNexusRecipeException(XamlNexusRecipeErrors.FilePathEscapesProject, [change.RelativePath]);
            if (Path.GetFileName(fullPath).Equals("xamlnexus.json", StringComparison.OrdinalIgnoreCase))
                throw new XamlNexusRecipeException(XamlNexusRecipeErrors.DirectManifestEditForbidden, []);
            if (!paths.Add(fullPath))
                throw new XamlNexusRecipeException(XamlNexusRecipeErrors.DuplicateFileChange, [change.RelativePath]);

            ProjectPathSafety.EnsureNoLinks(fullPath);
            if (Directory.Exists(fullPath))
                throw new XamlNexusRecipeException(XamlNexusRecipeErrors.FileTargetIsDirectory, [change.RelativePath]);
            string? parentPath = Path.GetDirectoryName(fullPath);
            while (!string.IsNullOrEmpty(parentPath) &&
                   !parentPath.Equals(root, StringComparison.OrdinalIgnoreCase)) {
                if (File.Exists(parentPath)) {
                    throw new XamlNexusRecipeException(
                        XamlNexusRecipeErrors.ParentPathIsFile, [change.RelativePath]);
                }
                parentPath = Path.GetDirectoryName(parentPath);
            }

            bool exists = File.Exists(fullPath);
            switch (change.Kind) {
                case XamlNexusRecipeFileChangeKind.Create when exists:
                    throw new XamlNexusRecipeException(XamlNexusRecipeErrors.FileAlreadyExists, [change.RelativePath]);
                case XamlNexusRecipeFileChangeKind.Create when change.Content is null:
                    throw new XamlNexusRecipeException(XamlNexusRecipeErrors.MissingCreateContent, [change.RelativePath]);
                case XamlNexusRecipeFileChangeKind.Replace or XamlNexusRecipeFileChangeKind.Delete when !exists:
                    throw new XamlNexusRecipeException(XamlNexusRecipeErrors.ExpectedFileMissing, [change.RelativePath]);
                case XamlNexusRecipeFileChangeKind.Replace when change.Content is null:
                    throw new XamlNexusRecipeException(XamlNexusRecipeErrors.MissingReplaceContent, [change.RelativePath]);
            }

            if (change.Kind is XamlNexusRecipeFileChangeKind.Replace or XamlNexusRecipeFileChangeKind.Delete) {
                // 检查磁盘实际内容而不只检查清单；文件已被用户修改时拒绝覆盖或删除
                if (!IsSha256(change.ExpectedSha256))
                    throw new XamlNexusRecipeException(XamlNexusRecipeErrors.InvalidExpectedHash, [change.RelativePath]);

                string actualHash = XamlNexusRecipeHash.ComputeFile(fullPath);
                if (!actualHash.Equals(change.ExpectedSha256, StringComparison.OrdinalIgnoreCase)) {
                    throw new XamlNexusRecipeException(
                        XamlNexusRecipeErrors.FileHashMismatch, [change.RelativePath]);
                }
            }

            resolved.Add(new ResolvedRecipeFileChange(change, fullPath));
        }

        IReadOnlyList<ResolvedRecipeFileChange> projectChanges =
            // 结构操作同样转为完整文件替换，以便共享事务快照、前置条件和回滚机制
            XamlNexusRecipeProjectEditor.ResolveAndValidate(
                root,
                plan.ProjectOperations,
                resolved);
        return resolved.Concat(projectChanges).ToArray();
    }

    private static bool IsValidId(string id) =>
        !string.IsNullOrWhiteSpace(id) && RecipeIdRegex().IsMatch(id);

    private static bool IsSha256(string? value) =>
        value is not null && Sha256Regex().IsMatch(value);

    /// <summary>逐个检查依赖或冲突 ID 的格式；重复和集合交叉检查由调用方完成</summary>
    private static void ValidateIds(IReadOnlyList<string> ids, string kind) {
        ArgumentNullException.ThrowIfNull(ids);
        string? invalid = ids.FirstOrDefault(id => !IsValidId(id));
        if (invalid is not null)
            throw new XamlNexusRecipeException(XamlNexusRecipeErrors.InvalidRelatedModuleId, [kind, invalid]);
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex RecipeIdRegex();

    [GeneratedRegex("^[0-9]+\\.[0-9]+\\.[0-9]+(?:-[0-9A-Za-z.-]+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex SemanticVersionRegex();

    [GeneratedRegex("^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Regex();
}

/// <summary>经过解析的文件操作，包含绝对路径；共享项目文件标记用于避免把公共文件登记为组件独占</summary>
internal sealed record ResolvedRecipeFileChange(
    XamlNexusRecipeFileChange Change,
    string FullPath,
    bool IsSharedProjectFile = false);
