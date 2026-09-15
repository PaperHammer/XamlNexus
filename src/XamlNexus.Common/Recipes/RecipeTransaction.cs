using System.Security.Cryptography;
using XamlNexus.Common.Projects;

namespace XamlNexus.Common.Recipes;

/// <summary>一次已完成操作的结果，包含组件 ID、版本和发生变化的文件路径</summary>
public sealed record XamlNexusRecipeApplyResult(
    string RecipeId,
    string RecipeVersion,
    IReadOnlyList<string> ChangedFiles);

/// <summary>单个文件的预览项；Scope 区分组件自有文件 owned 与共享项目文件 project</summary>
public sealed record XamlNexusRecipePreviewChange(
    XamlNexusRecipeFileChangeKind Kind,
    string RelativePath,
    string Scope);

/// <summary>安装、移除或更新的预览，描述版本变化及文件操作，不表示已经提交</summary>
public sealed record XamlNexusRecipePreview(
    string Operation,
    string RecipeId,
    string? FromVersion,
    string ToVersion,
    IReadOnlyList<XamlNexusRecipePreviewChange> Changes);

/// <summary>统一管理 Recipe 的预览、提交和异常回滚；批量安装入口在另一个 partial 文件中</summary>
public static partial class XamlNexusRecipeTransaction {
    /// <summary>复用文件事务创建业务页面，但不把页面登记为可卸载的 Recipe；dryRun 仅检查并列出路径</summary>
    internal static IReadOnlyList<string> ApplyPageChanges(XamlNexusProjectContext project,
        XamlNexusRecipePlan plan, bool dryRun) {
        var changes = XamlNexusRecipeContract.ResolveAndValidatePlan(project.RootDirectory, plan);
        if (dryRun) return changes.Select(change => change.Change.RelativePath).ToArray();
        // Business pages belong to the user, not to a removable Recipe.
        return Execute(project, "page", "1.0.0", changes, manifest => manifest).ChangedFiles;
    }
    /// <summary>生成并校验安装计划，返回预览；本执行器不写项目文件</summary>
    public static XamlNexusRecipePreview PreviewApply(
        XamlNexusProjectContext project,
        IXamlNexusRecipe recipe) {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(recipe);
        XamlNexusRecipeDescriptor descriptor = recipe.Descriptor;
        XamlNexusRecipeContract.ValidateCompatibility(descriptor, project.Manifest);
        var context = new XamlNexusRecipeContext(project.RootDirectory, project.Manifest);
        XamlNexusRecipePlan plan = recipe.CreatePlan(context)
            ?? throw new XamlNexusRecipeException(XamlNexusRecipeErrors.MissingInstallationPlan, []);
        IReadOnlyList<ResolvedRecipeFileChange> changes =
            XamlNexusRecipeContract.ResolveAndValidatePlan(project.RootDirectory, plan);
        return CreatePreview("add", descriptor.Id, null, descriptor.Version, changes);
    }

    /// <summary>依据已安装组件的文件清单和卸载扩展生成删除及引用调整预览</summary>
    public static XamlNexusRecipePreview PreviewRemove(
        XamlNexusProjectContext project,
        IXamlNexusRecipe recipe) {
        (XamlNexusRecipeDescriptor descriptor, XamlNexusManagedModule module, XamlNexusRecipePlan plan) =
            CreateRemovalPlan(project, recipe);
        IReadOnlyList<ResolvedRecipeFileChange> changes =
            XamlNexusRecipeContract.ResolveAndValidatePlan(project.RootDirectory, plan);
        return CreatePreview("remove", descriptor.Id, module.Version, module.Version, changes);
    }

    /// <summary>检查目标版本和兼容性，预览自有文件及共享引用的更新</summary>
    public static XamlNexusRecipePreview PreviewUpdate(
        XamlNexusProjectContext project,
        IXamlNexusRecipe recipe) {
        (XamlNexusRecipeDescriptor descriptor, XamlNexusManagedModule module, XamlNexusRecipePlan plan) =
            CreateRecipeUpdatePlan(project, recipe);
        IReadOnlyList<ResolvedRecipeFileChange> changes =
            XamlNexusRecipeContract.ResolveAndValidatePlan(project.RootDirectory, plan);
        return CreatePreview("update", descriptor.Id, module.Version, descriptor.Version, changes);
    }

    /// <summary>检查安装兼容性并解析计划，在写锁保护下提交文件和新增模块清单</summary>
    public static XamlNexusRecipeApplyResult Apply(
        XamlNexusProjectContext project,
        IXamlNexusRecipe recipe) {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(recipe);

        XamlNexusRecipeDescriptor descriptor = recipe.Descriptor;
        XamlNexusRecipeContract.ValidateCompatibility(descriptor, project.Manifest);

        var recipeContext = new XamlNexusRecipeContext(project.RootDirectory, project.Manifest);
        XamlNexusRecipePlan plan = recipe.CreatePlan(recipeContext)
            ?? throw new XamlNexusRecipeException(XamlNexusRecipeErrors.MissingInstallationPlan, []);
        IReadOnlyList<ResolvedRecipeFileChange> changes =
            XamlNexusRecipeContract.ResolveAndValidatePlan(project.RootDirectory, plan);

        return Execute(
            project,
            descriptor.Id,
            descriptor.Version,
            changes,
            manifest => AddRecipeToManifest(manifest, descriptor, changes));
    }

    /// <summary>删除清单登记且哈希仍匹配的组件文件，更新模块清单，完成后清理空目录</summary>
    public static XamlNexusRecipeApplyResult Remove(
        XamlNexusProjectContext project,
        IXamlNexusRecipe recipe) {
        (XamlNexusRecipeDescriptor descriptor, XamlNexusManagedModule installedModule, XamlNexusRecipePlan plan) =
            CreateRemovalPlan(project, recipe);
        IReadOnlyList<ResolvedRecipeFileChange> changes =
            XamlNexusRecipeContract.ResolveAndValidatePlan(project.RootDirectory, plan);

        XamlNexusRecipeApplyResult result = Execute(
            project,
            descriptor.Id,
            installedModule.Version,
            changes,
            manifest => RemoveRecipeFromManifest(manifest, descriptor.Id));
        RemoveEmptyOwnedDirectories(project.RootDirectory, plan.Changes);
        return result;
    }

    /// <summary>提交目标版本文件及清单，拒绝用默认更新流程覆盖已被用户修改的自有文件</summary>
    public static XamlNexusRecipeApplyResult Update(
        XamlNexusProjectContext project,
        IXamlNexusRecipe recipe) {
        (XamlNexusRecipeDescriptor descriptor, XamlNexusManagedModule installedModule, XamlNexusRecipePlan updatePlan) =
            CreateRecipeUpdatePlan(project, recipe);
        IReadOnlyList<ResolvedRecipeFileChange> changes =
            XamlNexusRecipeContract.ResolveAndValidatePlan(project.RootDirectory, updatePlan);

        XamlNexusRecipeApplyResult result = Execute(
            project,
            descriptor.Id,
            descriptor.Version,
            changes,
            manifest => UpdateRecipeInManifest(manifest, descriptor, changes));
        RemoveEmptyOwnedDirectories(project.RootDirectory, updatePlan.Changes);
        return result;
    }

    /// <summary>只允许卸载来源为 recipe 的模块，按登记的文件哈希生成删除计划，并附加引用清理操作</summary>
    private static (XamlNexusRecipeDescriptor Descriptor, XamlNexusManagedModule Module, XamlNexusRecipePlan Plan)
        CreateRemovalPlan(XamlNexusProjectContext project, IXamlNexusRecipe recipe) {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(recipe);
        XamlNexusRecipeDescriptor descriptor = recipe.Descriptor;
        XamlNexusRecipeContract.ValidateDescriptor(descriptor);
        XamlNexusManagedModule? module = project.Manifest.Modules.SingleOrDefault(value =>
            value.Id.Equals(descriptor.Id, StringComparison.OrdinalIgnoreCase));
        if (module is null)
            throw new XamlNexusRecipeException(XamlNexusRecipeErrors.RemovalModuleNotInstalled, [descriptor.Id]);
        if (!module.Source.Equals("recipe", StringComparison.OrdinalIgnoreCase))
            throw new XamlNexusRecipeException(XamlNexusRecipeErrors.RemovalRequiresRecipeModule, [descriptor.Id]);
        var context = new XamlNexusRecipeContext(project.RootDirectory, project.Manifest);
        return (descriptor, module, new XamlNexusRecipePlan {
            Changes = (module.Files ?? [])
                .Select(file => XamlNexusRecipeFileChange.Delete(file.Path, file.Sha256))
                .ToArray(),
            ProjectOperations = recipe is IXamlNexusRecipeRemovalPlanProvider provider
                ? provider.CreateRemovalOperations(context)
                : [],
        });
    }

    /// <summary>检查已安装来源及目标版本，拒绝同版本更新和降级，再将目标安装内容转换为更新计划</summary>
    private static (XamlNexusRecipeDescriptor Descriptor, XamlNexusManagedModule Module, XamlNexusRecipePlan Plan)
        CreateRecipeUpdatePlan(XamlNexusProjectContext project, IXamlNexusRecipe recipe) {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(recipe);
        XamlNexusRecipeDescriptor descriptor = recipe.Descriptor;
        XamlNexusRecipeContract.ValidateDescriptor(descriptor);
        XamlNexusManagedModule? module = project.Manifest.Modules.SingleOrDefault(value =>
            value.Id.Equals(descriptor.Id, StringComparison.OrdinalIgnoreCase));
        if (module is null)
            throw new XamlNexusRecipeException(XamlNexusRecipeErrors.UpdateModuleNotInstalled, [descriptor.Id]);
        if (!module.Source.Equals("recipe", StringComparison.OrdinalIgnoreCase))
            throw new XamlNexusRecipeException(XamlNexusRecipeErrors.UpdateRequiresRecipeModule, [descriptor.Id]);
        int comparison = CompareSemanticVersions(descriptor.Version, module.Version);
        if (comparison == 0)
            throw new XamlNexusRecipeException(XamlNexusRecipeErrors.AlreadyAtTargetVersion, [descriptor.Id, descriptor.Version]);
        if (comparison < 0)
            throw new XamlNexusRecipeException(XamlNexusRecipeErrors.DowngradeNotSupported, [module.Version, descriptor.Version]);
        ValidateUpdateCompatibility(descriptor, project.Manifest);
        var context = new XamlNexusRecipeContext(project.RootDirectory, project.Manifest);
        XamlNexusRecipePlan desired = recipe.CreatePlan(context)
            ?? throw new XamlNexusRecipeException(XamlNexusRecipeErrors.MissingInstallationPlan, []);
        return (descriptor, module, CreateUpdatePlan(context, recipe, module, desired));
    }

    /// <summary>统一预览路径分隔符，并标明文件是否由组件独占</summary>
    private static XamlNexusRecipePreview CreatePreview(
        string operation,
        string recipeId,
        string? fromVersion,
        string toVersion,
        IReadOnlyList<ResolvedRecipeFileChange> changes) => new(
            operation,
            recipeId,
            fromVersion,
            toVersion,
            changes.Select(change => new XamlNexusRecipePreviewChange(
                change.Change.Kind,
                change.Change.RelativePath.Replace('\\', '/'),
                change.IsSharedProjectFile ? "project" : "owned")).ToArray());

    /// <summary>把已拥有文件的 Create 转为带旧哈希的 Replace，旧有而目标不再需要的文件转为 Delete</summary>
    private static XamlNexusRecipePlan CreateUpdatePlan(
        XamlNexusRecipeContext context,
        IXamlNexusRecipe recipe,
        XamlNexusManagedModule installedModule,
        XamlNexusRecipePlan desiredPlan) {
        ArgumentNullException.ThrowIfNull(desiredPlan.Changes);
        var ownedFiles = (installedModule.Files ?? []).ToDictionary(
            file => NormalizePath(file.Path),
            StringComparer.OrdinalIgnoreCase);
        var desiredPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var changes = new List<XamlNexusRecipeFileChange>();

        foreach (XamlNexusRecipeFileChange desired in desiredPlan.Changes) {
            string normalizedPath = NormalizePath(desired.RelativePath);
            desiredPaths.Add(normalizedPath);
            if (desired.Kind == XamlNexusRecipeFileChangeKind.Create &&
                ownedFiles.TryGetValue(normalizedPath, out XamlNexusManagedFile? owned)) {
                changes.Add(new XamlNexusRecipeFileChange {
                    Kind = XamlNexusRecipeFileChangeKind.Replace,
                    RelativePath = desired.RelativePath,
                    Content = desired.Content,
                    ExpectedSha256 = owned.Sha256,
                });
            }
            else {
                changes.Add(desired);
            }
        }

        changes.AddRange(ownedFiles
            .Where(pair => !desiredPaths.Contains(pair.Key))
            .Select(pair => XamlNexusRecipeFileChange.Delete(
                pair.Value.Path,
                pair.Value.Sha256)));

        IReadOnlyList<XamlNexusRecipeProjectOperation> operations =
            recipe is IXamlNexusRecipeUpdatePlanProvider provider
                ? provider.CreateUpdateOperations(context, installedModule.Version)
                : [];
        return new XamlNexusRecipePlan {
            Changes = changes,
            ProjectOperations = operations,
        };
    }

    /// <summary>更新时允许当前组件已存在，但仍检查架构、依赖和冲突</summary>
    private static void ValidateUpdateCompatibility(
        XamlNexusRecipeDescriptor descriptor,
        XamlNexusProjectManifest manifest) {
        if (!descriptor.SupportedPresets.Contains(
                manifest.Project.Preset,
                StringComparer.OrdinalIgnoreCase)) {
            throw new XamlNexusRecipeException(
                XamlNexusRecipeErrors.UpdateUnsupportedPreset, [descriptor.Id, manifest.Project.Preset]);
        }
        var installed = manifest.Modules
            .Select(module => module.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        string[] missingDependencies = descriptor.Dependencies
            .Where(dependency => !installed.Contains(dependency))
            .ToArray();
        if (missingDependencies.Length > 0) {
            throw new XamlNexusRecipeException(
                XamlNexusRecipeErrors.UpdateMissingDependencies, [string.Join(", ", missingDependencies)]);
        }
        string[] conflicts = descriptor.Conflicts.Where(installed.Contains).ToArray();
        if (conflicts.Length > 0) {
            throw new XamlNexusRecipeException(
                XamlNexusRecipeErrors.UpdateConflictingModules, [string.Join(", ", conflicts)]);
        }
    }

    /// <summary>比较主版本及预发布标识；正式版本高于同版本预发布，数字标识按数值比较</summary>
    private static int CompareSemanticVersions(string left, string right) {
        (Version leftVersion, string? leftPrerelease) = ParseSemanticVersion(left);
        (Version rightVersion, string? rightPrerelease) = ParseSemanticVersion(right);
        int coreComparison = leftVersion.CompareTo(rightVersion);
        if (coreComparison != 0) return coreComparison;
        if (leftPrerelease is null) return rightPrerelease is null ? 0 : 1;
        if (rightPrerelease is null) return -1;

        string[] leftParts = leftPrerelease.Split('.');
        string[] rightParts = rightPrerelease.Split('.');
        for (int index = 0; index < Math.Max(leftParts.Length, rightParts.Length); index++) {
            if (index >= leftParts.Length) return -1;
            if (index >= rightParts.Length) return 1;
            bool leftNumeric = leftParts[index].All(char.IsDigit);
            bool rightNumeric = rightParts[index].All(char.IsDigit);
            int comparison = leftNumeric && rightNumeric
                ? CompareNumericIdentifiers(leftParts[index], rightParts[index])
                : leftNumeric
                    ? -1
                    : rightNumeric
                        ? 1
                        : string.CompareOrdinal(leftParts[index], rightParts[index]);
            if (comparison != 0) return comparison;
        }
        return 0;
    }

    /// <summary>通过数字字符串长度和字典序比较数值，避免转换成整数时溢出</summary>
    private static int CompareNumericIdentifiers(string left, string right) {
        string normalizedLeft = left.TrimStart('0');
        string normalizedRight = right.TrimStart('0');
        if (normalizedLeft.Length == 0) normalizedLeft = "0";
        if (normalizedRight.Length == 0) normalizedRight = "0";
        int lengthComparison = normalizedLeft.Length.CompareTo(normalizedRight.Length);
        return lengthComparison != 0
            ? lengthComparison
            : string.CompareOrdinal(normalizedLeft, normalizedRight);
    }

    /// <summary>拆分数字版本和预发布部分，供版本比较使用</summary>
    private static (Version Version, string? Prerelease) ParseSemanticVersion(string value) {
        string[] parts = value.Split('-', 2);
        return (Version.Parse(parts[0]), parts.Length == 2 ? parts[1] : null);
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/');

    /// <summary>获取项目写锁、复核计划、保存内存快照并提交；捕获失败后逆序恢复，非崩溃恢复型持久事务</summary>
    private static XamlNexusRecipeApplyResult Execute(
        XamlNexusProjectContext project,
        string recipeId,
        string recipeVersion,
        IReadOnlyList<ResolvedRecipeFileChange> changes,
        Func<XamlNexusProjectManifest, XamlNexusProjectManifest> updateManifest,
        string? expectedManifestHash = null) {

        using var lease = ProjectWriteLease.Acquire(project.RootDirectory);
        ProjectPathSafety.EnsureNoLinks(project.ManifestPath);
        byte[] originalManifest = File.ReadAllBytes(project.ManifestPath);
        if (expectedManifestHash is not null && !HashBytes(originalManifest).Equals(expectedManifestHash, StringComparison.OrdinalIgnoreCase))
            throw new XamlNexusRecipeException(XamlNexusRecipeErrors.StaleBatchManifest, []);
        var freshManifest = XamlNexusProjectManifestStore.Load(project.ManifestPath);
        if (System.Text.Json.JsonSerializer.Serialize(freshManifest) != System.Text.Json.JsonSerializer.Serialize(project.Manifest))
            throw new XamlNexusRecipeException(XamlNexusRecipeErrors.StaleProjectContext, []);

        // 计划可能在其他进程写入前已生成；持有跨进程写锁后再次确认文件存在性及预期哈希
        XamlNexusRecipeContract.ResolveAndValidatePlan(project.RootDirectory, new XamlNexusRecipePlan {
            Changes = changes.Select(change => change.Change).ToArray(),
        });

        var snapshots = changes.ToDictionary(
            change => change.FullPath,
            change => File.Exists(change.FullPath) ? File.ReadAllBytes(change.FullPath) : null,
            StringComparer.OrdinalIgnoreCase);
        var createdDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var appliedChanges = new List<ResolvedRecipeFileChange>();

        try {
            foreach (ResolvedRecipeFileChange resolved in changes) {
                EnsureParentDirectory(resolved.FullPath, project.RootDirectory, createdDirectories);
                switch (resolved.Change.Kind) {
                    case XamlNexusRecipeFileChangeKind.Create:
                    case XamlNexusRecipeFileChangeKind.Replace:
                        AtomicWrite(resolved.FullPath, resolved.Change.Content!);
                        break;
                    case XamlNexusRecipeFileChangeKind.Delete:
                        File.Delete(resolved.FullPath);
                        break;
                    default:
                        throw new XamlNexusRecipeException(
                            XamlNexusRecipeErrors.UnsupportedFileChangeKind, [resolved.Change.Kind]);
                }
                appliedChanges.Add(resolved);
            }

            XamlNexusProjectManifest updatedManifest = updateManifest(project.Manifest);
            // 文件全部写入成功后再保存清单；清单保存失败也进入回滚路径
            XamlNexusProjectManifestStore.Save(project.ManifestPath, updatedManifest);

            return new XamlNexusRecipeApplyResult(
                recipeId,
                recipeVersion,
                changes.Select(change => change.Change.RelativePath).ToArray());
        }
        catch (Exception applyException) {
            var rollbackErrors = new List<Exception>();
            foreach (ResolvedRecipeFileChange change in appliedChanges.AsEnumerable().Reverse()) {
                // 逆序撤销已完成操作：原本不存在就删除新文件，原本存在则恢复字节快照
                try {
                    byte[]? original = snapshots[change.FullPath];
                    if (original is null) {
                        if (File.Exists(change.FullPath)) File.Delete(change.FullPath);
                    }
                    else {
                        AtomicWrite(change.FullPath, original);
                    }
                }
                catch (Exception rollbackException) {
                    rollbackErrors.Add(rollbackException);
                }
            }

            try {
                if (!File.ReadAllBytes(project.ManifestPath).AsSpan().SequenceEqual(originalManifest))
                    AtomicWrite(project.ManifestPath, originalManifest);
            }
            catch (Exception rollbackException) {
                rollbackErrors.Add(rollbackException);
            }

            foreach (string directory in createdDirectories.OrderByDescending(path => path.Length)) {
                // 只删除本次新建且仍为空的目录，从最深层向外清理，保留用户原有目录
                try {
                    if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                        Directory.Delete(directory);
                }
                catch (Exception rollbackException) {
                    rollbackErrors.Add(rollbackException);
                }
            }

            throw rollbackErrors.Count == 0
                ? new XamlNexusRecipeException(XamlNexusRecipeErrors.TransactionRolledBack, [recipeId], applyException)
                : new XamlNexusRecipeException(XamlNexusRecipeErrors.TransactionRollbackFailed, [recipeId, rollbackErrors.Count], applyException);
        }
    }

    /// <summary>构造移除指定模块后的新清单，保留项目身份和脚手架基线</summary>
    private static XamlNexusProjectManifest RemoveRecipeFromManifest(
        XamlNexusProjectManifest manifest,
        string recipeId) => new() {
            SchemaVersion = manifest.SchemaVersion,
            GeneratorVersion = manifest.GeneratorVersion,
            Project = manifest.Project,
            ScaffoldFiles = manifest.ScaffoldFiles,
            Modules = manifest.Modules
                .Where(module => !module.Id.Equals(recipeId, StringComparison.OrdinalIgnoreCase))
                .ToArray(),
        };

    /// <summary>替换目标模块的版本及自有文件记录，其余模块保持不变</summary>
    private static XamlNexusProjectManifest UpdateRecipeInManifest(
        XamlNexusProjectManifest manifest,
        XamlNexusRecipeDescriptor descriptor,
        IReadOnlyList<ResolvedRecipeFileChange> appliedChanges) => new() {
            SchemaVersion = manifest.SchemaVersion,
            GeneratorVersion = manifest.GeneratorVersion,
            Project = manifest.Project,
            ScaffoldFiles = manifest.ScaffoldFiles,
            Modules = manifest.Modules
                .Select(module => module.Id.Equals(descriptor.Id, StringComparison.OrdinalIgnoreCase)
                    ? CreateManagedModule(descriptor, appliedChanges)
                    : module)
                .ToArray(),
        };

    /// <summary>从删除文件的父目录向上清理空目录，到项目根目录停止；清理失败不撤销已完成的文件事务</summary>
    private static void RemoveEmptyOwnedDirectories(
        string rootDirectory,
        IReadOnlyList<XamlNexusRecipeFileChange> changes) {
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootDirectory));
        foreach (string? initialDirectory in changes
                     .Where(change => change.Kind == XamlNexusRecipeFileChangeKind.Delete)
                     .Select(change => Path.GetDirectoryName(Path.Combine(root, change.RelativePath)))
                     .Where(directory => directory is not null)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderByDescending(directory => directory!.Length)) {
            try {
                string? directory = initialDirectory;
                while (directory is not null &&
                       !directory.Equals(root, StringComparison.OrdinalIgnoreCase) &&
                       Directory.Exists(directory) &&
                       !Directory.EnumerateFileSystemEntries(directory).Any()) {
                    Directory.Delete(directory);
                    directory = Path.GetDirectoryName(directory);
                }
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException) {
                // Empty-directory cleanup is cosmetic; the transactional file removal succeeded.
            }
        }
    }

    /// <summary>在现有清单末尾追加安装模块，复用实际写入后的自有文件信息</summary>
    private static XamlNexusProjectManifest AddRecipeToManifest(
        XamlNexusProjectManifest manifest,
        XamlNexusRecipeDescriptor descriptor,
        IReadOnlyList<ResolvedRecipeFileChange> appliedChanges) {
        return new XamlNexusProjectManifest {
            SchemaVersion = manifest.SchemaVersion,
            GeneratorVersion = manifest.GeneratorVersion,
            Project = manifest.Project,
            ScaffoldFiles = manifest.ScaffoldFiles,
            Modules = manifest.Modules.Concat([
                CreateManagedModule(descriptor, appliedChanges)
            ]).ToArray(),
        };
    }

    /// <summary>只登记未删除的组件自有文件；共享 csproj 和解决方案不能被组件整体拥有或卸载删除</summary>
    private static XamlNexusManagedModule CreateManagedModule(
        XamlNexusRecipeDescriptor descriptor,
        IReadOnlyList<ResolvedRecipeFileChange> appliedChanges) => new() {
            Id = descriptor.Id,
            Version = descriptor.Version,
            Source = "recipe",
            Files = appliedChanges
                .Where(change =>
                    !change.IsSharedProjectFile &&
                    change.Change.Kind != XamlNexusRecipeFileChangeKind.Delete)
                .Select(change => new XamlNexusManagedFile {
                    Path = NormalizePath(change.Change.RelativePath),
                    Sha256 = XamlNexusRecipeHash.ComputeFile(change.FullPath),
                })
                .ToArray(),
        };

    /// <summary>由外向内创建缺失的父目录，并记录本次新建目录，供回滚时清理</summary>
    private static void EnsureParentDirectory(
        string filePath,
        string rootDirectory,
        ISet<string> createdDirectories) {
        string? directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(directory) || Directory.Exists(directory)) return;

        var missing = new Stack<string>();
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootDirectory));
        while (!string.IsNullOrEmpty(directory) &&
               !Directory.Exists(directory) &&
               !directory.Equals(root, StringComparison.OrdinalIgnoreCase)) {
            missing.Push(directory);
            directory = Path.GetDirectoryName(directory);
        }

        while (missing.Count > 0) {
            string path = missing.Pop();
            Directory.CreateDirectory(path);
            createdDirectories.Add(path);
        }
    }

    /// <summary>先写同目录唯一临时文件并刷盘，再移动替换目标；这是单文件写入步骤，不是多文件整体原子提交</summary>
    private static void AtomicWrite(string path, byte[] content) {
        string temporaryPath = path + $".{Guid.NewGuid():N}.tmp";
        try {
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None)) {
                stream.Write(content);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}

/// <summary>统一计算小写十六进制 SHA-256，用于文件前置条件和模块文件清单</summary>
public static class XamlNexusRecipeHash {
    /// <summary>对完整字节内容计算哈希，不进行文本或换行规范化</summary>
    public static string Compute(byte[] content) {
        ArgumentNullException.ThrowIfNull(content);
        return Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
    }

    /// <summary>读取整个文件后计算内容哈希</summary>
    public static string ComputeFile(string path) => Compute(File.ReadAllBytes(path));
}
