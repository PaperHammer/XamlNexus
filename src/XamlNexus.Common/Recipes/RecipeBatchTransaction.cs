using System.Security.Cryptography;
using XamlNexus.Common.Projects;

namespace XamlNexus.Common.Recipes;

/// <summary>批量安装或更新的准备结果：原项目身份和清单哈希、最终文件内容、最终清单及逐组件预览</summary>
public sealed class XamlNexusRecipeBatchPlan {
    internal XamlNexusRecipeBatchPlan(string root, string manifestHash, XamlNexusProjectManifest manifest,
        XamlNexusRecipePlan files, IReadOnlyList<XamlNexusRecipePreview> recipes) {
        Root = root;
        ManifestHash = manifestHash;
        Manifest = manifest;
        Files = files;
        Recipes = recipes;
        ChangedFiles = Array.AsReadOnly(files.Changes.Select(change => change.RelativePath).ToArray());
    }
    /// <summary>计划所属项目的绝对路径，防止应用到另一个项目</summary>
    internal string Root { get; }
    /// <summary>复制快照时的清单哈希，用于检测规划后项目状态变化</summary>
    internal string ManifestHash { get; }
    /// <summary>在临时副本中全部安装完成后得到的清单</summary>
    internal XamlNexusProjectManifest Manifest { get; }
    /// <summary>将多次中间操作归并为从原始状态到最终状态的文件计划</summary>
    internal XamlNexusRecipePlan Files { get; }
    /// <summary>按调用方提供的处理顺序保存每个组件的预览</summary>
    public IReadOnlyList<XamlNexusRecipePreview> Recipes { get; }
    /// <summary>归并后的文件路径列表，用于展示批量提交范围</summary>
    public IReadOnlyList<string> ChangedFiles { get; }
}

public static partial class XamlNexusRecipeTransaction {
    private static readonly HashSet<string> BatchExcludedDirectories = new(StringComparer.OrdinalIgnoreCase) {
        "bin", "obj", ".git", ".vs", ".artifacts",
    };

    /// <summary>复制项目到临时目录，按给定顺序实际演练安装并校验结果；不修改原项目，但会写临时文件</summary>
    public static XamlNexusRecipeBatchPlan PrepareApplyBatch(XamlNexusProjectContext project, IReadOnlyList<IXamlNexusRecipe> recipes) =>
        PrepareBatch(project, recipes, update: false);

    /// <summary>在临时项目副本中依次演练多个 Recipe 更新，再生成一次性提交计划。</summary>
    public static XamlNexusRecipeBatchPlan PrepareUpdateBatch(XamlNexusProjectContext project, IReadOnlyList<IXamlNexusRecipe> recipes) =>
        PrepareBatch(project, recipes, update: true);

    private static XamlNexusRecipeBatchPlan PrepareBatch(
        XamlNexusProjectContext project,
        IReadOnlyList<IXamlNexusRecipe> recipes,
        bool update) {
        if (recipes.Count == 0) throw new ArgumentException("At least one Recipe is required.", nameof(recipes));

        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(project.RootDirectory));
        string staging = Directory.CreateTempSubdirectory("xamlnexus-batch-").FullName;
        Exception? failure = null;

        try {
            ProjectPathSafety.EnsureNoLinks(root);
            CopyBatchSnapshot(root, staging);
            string stagedManifest = Path.Combine(staging, "xamlnexus.json");
            string manifestHash = HashBytes(File.ReadAllBytes(stagedManifest));
            var originals = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
            // 每个文件只记录首次修改前的内容，后续组件可在临时副本中继续修改同一文件
            var previews = new List<XamlNexusRecipePreview>();
            foreach (var recipe in recipes) {
                // 每轮重新加载清单，让后续组件看见前一组件已安装的依赖和文件
                var current = XamlNexusProjectLocator.Locate(staging);
                previews.Add(StageBatchRecipe(root, current, recipe, update, originals));
            }
            var final = XamlNexusProjectLocator.Locate(staging);
            if (!XamlNexusProjectValidator.Validate(final).IsValid)
                throw new XamlNexusRecipeException(XamlNexusRecipeErrors.InvalidComposedBatch, []);

            var files = new List<XamlNexusRecipeFileChange>();
            // 对比最初快照和最终状态，把中间多次修改压缩成一次对原项目的提交
            foreach (var (relative, original) in originals) {
                string path = Path.Combine(staging, relative);
                byte[]? content = File.Exists(path) ? File.ReadAllBytes(path) : null;
                if (original is null && content is null) continue;
                files.Add(new XamlNexusRecipeFileChange {
                    RelativePath = relative,
                    Kind = content is null ? XamlNexusRecipeFileChangeKind.Delete
                        : original is null ? XamlNexusRecipeFileChangeKind.Create : XamlNexusRecipeFileChangeKind.Replace,
                    Content = content,
                    ExpectedSha256 = original is null ? null : HashBytes(original),
                });
            }

            return new(root, manifestHash, final.Manifest, new() { Changes = files.AsReadOnly() }, previews.AsReadOnly());
        }
        catch (Exception exception) {
            failure = exception;
            throw;
        }
        finally {
            try { Directory.Delete(staging, recursive: true); }
            catch (Exception cleanupException) {
                if (failure is not null)
                    failure.Data["CleanupError"] = $"Temporary directory '{staging}' could not be removed: {cleanupException.Message}";
                else System.Diagnostics.Trace.TraceWarning($"Could not remove staging directory '{staging}': {cleanupException.Message}");
            }
        }
    }

    /// <summary>在临时副本中规划并执行一个 Recipe，同时记录最终提交所需的原始文件内容</summary>
    private static XamlNexusRecipePreview StageBatchRecipe(
        string root,
        XamlNexusProjectContext current,
        IXamlNexusRecipe recipe,
        bool update,
        Dictionary<string, byte[]?> originals) {
        string staging = current.RootDirectory;
        XamlNexusRecipePlan plan;
        string? fromVersion = null;
        if (update) {
            var updatePlan = CreateRecipeUpdatePlan(current, recipe);
            plan = updatePlan.Plan;
            fromVersion = updatePlan.Module.Version;
        }
        else {
            XamlNexusRecipeContract.ValidateCompatibility(recipe.Descriptor, current.Manifest);
            plan = recipe.CreatePlan(new XamlNexusRecipeContext(staging, current.Manifest))
                ?? throw new XamlNexusRecipeException(XamlNexusRecipeErrors.MissingInstallationPlan, []);
        }

        IReadOnlyList<ResolvedRecipeFileChange> changes =
            XamlNexusRecipeContract.ResolveAndValidatePlan(staging, plan);
        XamlNexusRecipePreview preview = CreatePreview(
            update ? "update" : "add", recipe.Descriptor.Id, fromVersion, recipe.Descriptor.Version, changes);
        foreach (var change in changes) {
            string relative = NormalizePath(Path.GetRelativePath(staging, change.FullPath));
            ValidateBatchPath(root, relative);
            if (!originals.ContainsKey(relative))
                originals.Add(relative, File.Exists(change.FullPath) ? File.ReadAllBytes(change.FullPath) : null);
        }
        Execute(current, recipe.Descriptor.Id, recipe.Descriptor.Version, changes,
            manifest => update
                ? UpdateRecipeInManifest(manifest, recipe.Descriptor, changes)
                : AddRecipeToManifest(manifest, recipe.Descriptor, changes));
        return preview;
    }

    /// <summary>确认项目路径和清单未改变，重新检查文件计划，再通过统一事务提交归并结果</summary>
    public static IReadOnlyList<string> ApplyBatch(XamlNexusProjectContext project, XamlNexusRecipeBatchPlan plan) {
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(project.RootDirectory));
        if (!root.Equals(plan.Root, StringComparison.OrdinalIgnoreCase))
            throw new XamlNexusRecipeException(XamlNexusRecipeErrors.BatchProjectMismatch, []);

        ValidateBatchPath(root, "xamlnexus.json");
        if (!HashBytes(File.ReadAllBytes(project.ManifestPath)).Equals(plan.ManifestHash, StringComparison.OrdinalIgnoreCase))
            throw new XamlNexusRecipeException(XamlNexusRecipeErrors.StaleBatchManifest, []);

        foreach (var change in plan.Files.Changes) ValidateBatchPath(root, change.RelativePath);
        // 此处检查后，Execute 还会在持有写锁时复核清单和文件，防止检查与提交之间状态变化
        var changes = XamlNexusRecipeContract.ResolveAndValidatePlan(root, plan.Files);

        IReadOnlyList<string> changedFiles = Execute(
            project, "batch", "1.0.0", changes, _ => plan.Manifest, plan.ManifestHash).ChangedFiles;
        RemoveEmptyOwnedDirectories(project.RootDirectory, plan.Files.Changes);
        return changedFiles;
    }

    private static string HashBytes(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    /// <summary>递归复制用于演练的项目快照，跳过构建及版本控制目录，拒绝链接路径</summary>
    private static void CopyBatchSnapshot(string source, string destination) {
        if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Batch installation does not support linked project paths.");

        foreach (string entry in Directory.EnumerateFileSystemEntries(source)) {
            string name = Path.GetFileName(entry);
            if (BatchExcludedDirectories.Contains(name)) continue;

            var attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException($"Batch installation does not support linked files or directories: {entry}");

            string target = Path.Combine(destination, name);
            if ((attributes & FileAttributes.Directory) != 0) {
                Directory.CreateDirectory(target);
                CopyBatchSnapshot(entry, target);
            }
            else File.Copy(entry, target);
        }
    }

    /// <summary>保证批量操作位于项目内部、避开排除目录，并检查目标及祖先是否为链接</summary>
    private static void ValidateBatchPath(string root, string relative) {
        string full = Path.GetFullPath(Path.Combine(root, relative));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || relative.Replace('\\', '/').Split('/').Any(BatchExcludedDirectories.Contains))
            throw new IOException($"Unsupported batch file path: {relative}");

        ProjectPathSafety.EnsureNoLinks(full);
    }
}
