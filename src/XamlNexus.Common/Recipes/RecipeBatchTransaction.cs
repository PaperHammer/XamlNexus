using System.Security.Cryptography;
using XamlNexus.Common.Projects;

namespace XamlNexus.Common.Recipes;

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
    internal string Root { get; }
    internal string ManifestHash { get; }
    internal XamlNexusProjectManifest Manifest { get; }
    internal XamlNexusRecipePlan Files { get; }
    public IReadOnlyList<XamlNexusRecipePreview> Recipes { get; }
    public IReadOnlyList<string> ChangedFiles { get; }
}

public static partial class XamlNexusRecipeTransaction {
    private static readonly HashSet<string> BatchExcludedDirectories = new(StringComparer.OrdinalIgnoreCase) {
        "bin", "obj", ".git", ".vs", ".artifacts",
    };

    public static XamlNexusRecipeBatchPlan PrepareApplyBatch(
        XamlNexusProjectContext project, IReadOnlyList<IXamlNexusRecipe> recipes) {
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
            var previews = new List<XamlNexusRecipePreview>();
            foreach (var recipe in recipes) {
                var current = XamlNexusProjectLocator.Locate(staging);
                XamlNexusRecipeContract.ValidateCompatibility(recipe.Descriptor, current.Manifest);
                var plan = recipe.CreatePlan(new XamlNexusRecipeContext(staging, current.Manifest))
                    ?? throw new XamlNexusRecipeException("XR1301", "Recipe returned no installation plan.");
                var changes = XamlNexusRecipeContract.ResolveAndValidatePlan(staging, plan);
                foreach (var change in changes) {
                    string relative = NormalizePath(Path.GetRelativePath(staging, change.FullPath));
                    ValidateBatchPath(root, relative);
                    if (!originals.ContainsKey(relative))
                        originals.Add(relative, File.Exists(change.FullPath) ? File.ReadAllBytes(change.FullPath) : null);
                }
                previews.Add(CreatePreview("add", recipe.Descriptor.Id, null, recipe.Descriptor.Version, changes));
                Execute(current, recipe.Descriptor.Id, recipe.Descriptor.Version, changes,
                    manifest => AddRecipeToManifest(manifest, recipe.Descriptor, changes));
            }
            var final = XamlNexusProjectLocator.Locate(staging);
            if (!XamlNexusProjectValidator.Validate(final).IsValid)
                throw new XamlNexusRecipeException("XR1801", "The composed batch failed project validation.");
            var files = new List<XamlNexusRecipeFileChange>();
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

    public static IReadOnlyList<string> ApplyBatch(XamlNexusProjectContext project, XamlNexusRecipeBatchPlan plan) {
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(project.RootDirectory));
        if (!root.Equals(plan.Root, StringComparison.OrdinalIgnoreCase))
            throw new XamlNexusRecipeException("XR1802", "The batch belongs to another project.");
        ValidateBatchPath(root, "xamlnexus.json");
        if (!HashBytes(File.ReadAllBytes(project.ManifestPath)).Equals(plan.ManifestHash, StringComparison.OrdinalIgnoreCase))
            throw new XamlNexusRecipeException("XR1803", "The project manifest changed after batch planning. Preview the operation again.");
        foreach (var change in plan.Files.Changes) ValidateBatchPath(root, change.RelativePath);
        var changes = XamlNexusRecipeContract.ResolveAndValidatePlan(root, plan.Files);
        return Execute(project, "batch", "1.0.0", changes, _ => plan.Manifest, plan.ManifestHash).ChangedFiles;
    }

    private static string HashBytes(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

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

    private static void ValidateBatchPath(string root, string relative) {
        string full = Path.GetFullPath(Path.Combine(root, relative));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || relative.Replace('\\', '/').Split('/').Any(BatchExcludedDirectories.Contains))
            throw new IOException($"Unsupported batch file path: {relative}");
        ProjectPathSafety.EnsureNoLinks(full);
    }
}
