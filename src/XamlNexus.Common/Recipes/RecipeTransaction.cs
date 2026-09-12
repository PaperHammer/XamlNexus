using System.Security.Cryptography;
using XamlNexus.Common.Projects;

namespace XamlNexus.Common.Recipes;

public sealed record XamlNexusRecipeApplyResult(
    string RecipeId,
    string RecipeVersion,
    IReadOnlyList<string> ChangedFiles);

public sealed record XamlNexusRecipePreviewChange(
    XamlNexusRecipeFileChangeKind Kind,
    string RelativePath,
    string Scope);

public sealed record XamlNexusRecipePreview(
    string Operation,
    string RecipeId,
    string? FromVersion,
    string ToVersion,
    IReadOnlyList<XamlNexusRecipePreviewChange> Changes);

public static partial class XamlNexusRecipeTransaction {
    internal static IReadOnlyList<string> ApplyPageChanges(XamlNexusProjectContext project,
        XamlNexusRecipePlan plan, bool dryRun) {
        var changes = XamlNexusRecipeContract.ResolveAndValidatePlan(project.RootDirectory, plan);
        if (dryRun) return changes.Select(change => change.Change.RelativePath).ToArray();
        // Business pages belong to the user, not to a removable Recipe.
        return Execute(project, "page", "1.0.0", changes, manifest => manifest).ChangedFiles;
    }
    public static XamlNexusRecipePreview PreviewApply(
        XamlNexusProjectContext project,
        IXamlNexusRecipe recipe) {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(recipe);
        XamlNexusRecipeDescriptor descriptor = recipe.Descriptor;
        XamlNexusRecipeContract.ValidateCompatibility(descriptor, project.Manifest);
        var context = new XamlNexusRecipeContext(project.RootDirectory, project.Manifest);
        XamlNexusRecipePlan plan = recipe.CreatePlan(context)
            ?? throw new XamlNexusRecipeException("XR1301", "Recipe returned no installation plan.");
        IReadOnlyList<ResolvedRecipeFileChange> changes =
            XamlNexusRecipeContract.ResolveAndValidatePlan(project.RootDirectory, plan);
        return CreatePreview("add", descriptor.Id, null, descriptor.Version, changes);
    }

    public static XamlNexusRecipePreview PreviewRemove(
        XamlNexusProjectContext project,
        IXamlNexusRecipe recipe) {
        (XamlNexusRecipeDescriptor descriptor, XamlNexusManagedModule module, XamlNexusRecipePlan plan) =
            CreateRemovalPlan(project, recipe);
        IReadOnlyList<ResolvedRecipeFileChange> changes =
            XamlNexusRecipeContract.ResolveAndValidatePlan(project.RootDirectory, plan);
        return CreatePreview("remove", descriptor.Id, module.Version, module.Version, changes);
    }

    public static XamlNexusRecipePreview PreviewUpdate(
        XamlNexusProjectContext project,
        IXamlNexusRecipe recipe) {
        (XamlNexusRecipeDescriptor descriptor, XamlNexusManagedModule module, XamlNexusRecipePlan plan) =
            CreateRecipeUpdatePlan(project, recipe);
        IReadOnlyList<ResolvedRecipeFileChange> changes =
            XamlNexusRecipeContract.ResolveAndValidatePlan(project.RootDirectory, plan);
        return CreatePreview("update", descriptor.Id, module.Version, descriptor.Version, changes);
    }

    public static XamlNexusRecipeApplyResult Apply(
        XamlNexusProjectContext project,
        IXamlNexusRecipe recipe) {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(recipe);

        XamlNexusRecipeDescriptor descriptor = recipe.Descriptor;
        XamlNexusRecipeContract.ValidateCompatibility(descriptor, project.Manifest);

        var recipeContext = new XamlNexusRecipeContext(project.RootDirectory, project.Manifest);
        XamlNexusRecipePlan plan = recipe.CreatePlan(recipeContext)
            ?? throw new XamlNexusRecipeException("XR1301", "Recipe returned no installation plan.");
        IReadOnlyList<ResolvedRecipeFileChange> changes =
            XamlNexusRecipeContract.ResolveAndValidatePlan(project.RootDirectory, plan);

        return Execute(
            project,
            descriptor.Id,
            descriptor.Version,
            changes,
            manifest => AddRecipeToManifest(manifest, descriptor, changes));
    }

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

    private static (XamlNexusRecipeDescriptor Descriptor, XamlNexusManagedModule Module, XamlNexusRecipePlan Plan)
        CreateRemovalPlan(XamlNexusProjectContext project, IXamlNexusRecipe recipe) {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(recipe);
        XamlNexusRecipeDescriptor descriptor = recipe.Descriptor;
        XamlNexusRecipeContract.ValidateDescriptor(descriptor);
        XamlNexusManagedModule? module = project.Manifest.Modules.SingleOrDefault(value =>
            value.Id.Equals(descriptor.Id, StringComparison.OrdinalIgnoreCase));
        if (module is null)
            throw new XamlNexusRecipeException("XR1401", $"Module '{descriptor.Id}' is not installed.");
        if (!module.Source.Equals("recipe", StringComparison.OrdinalIgnoreCase))
            throw new XamlNexusRecipeException("XR1402", $"Module '{descriptor.Id}' was not installed by a Recipe and cannot be removed this way.");
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

    private static (XamlNexusRecipeDescriptor Descriptor, XamlNexusManagedModule Module, XamlNexusRecipePlan Plan)
        CreateRecipeUpdatePlan(XamlNexusProjectContext project, IXamlNexusRecipe recipe) {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(recipe);
        XamlNexusRecipeDescriptor descriptor = recipe.Descriptor;
        XamlNexusRecipeContract.ValidateDescriptor(descriptor);
        XamlNexusManagedModule? module = project.Manifest.Modules.SingleOrDefault(value =>
            value.Id.Equals(descriptor.Id, StringComparison.OrdinalIgnoreCase));
        if (module is null)
            throw new XamlNexusRecipeException("XR1501", $"Module '{descriptor.Id}' is not installed.");
        if (!module.Source.Equals("recipe", StringComparison.OrdinalIgnoreCase))
            throw new XamlNexusRecipeException("XR1502", $"Module '{descriptor.Id}' was not installed by a Recipe and cannot be updated this way.");
        int comparison = CompareSemanticVersions(descriptor.Version, module.Version);
        if (comparison == 0)
            throw new XamlNexusRecipeException("XR1503", $"Recipe '{descriptor.Id}' is already at version {descriptor.Version}.");
        if (comparison < 0)
            throw new XamlNexusRecipeException("XR1504", $"Recipe downgrade from {module.Version} to {descriptor.Version} is not supported.");
        ValidateUpdateCompatibility(descriptor, project.Manifest);
        var context = new XamlNexusRecipeContext(project.RootDirectory, project.Manifest);
        XamlNexusRecipePlan desired = recipe.CreatePlan(context)
            ?? throw new XamlNexusRecipeException("XR1301", "Recipe returned no installation plan.");
        return (descriptor, module, CreateUpdatePlan(context, recipe, module, desired));
    }

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

    private static void ValidateUpdateCompatibility(
        XamlNexusRecipeDescriptor descriptor,
        XamlNexusProjectManifest manifest) {
        if (!descriptor.SupportedPresets.Contains(
                manifest.Project.Preset,
                StringComparer.OrdinalIgnoreCase)) {
            throw new XamlNexusRecipeException(
                "XR1505",
                $"Recipe '{descriptor.Id}' does not support preset '{manifest.Project.Preset}'.");
        }
        var installed = manifest.Modules
            .Select(module => module.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        string[] missingDependencies = descriptor.Dependencies
            .Where(dependency => !installed.Contains(dependency))
            .ToArray();
        if (missingDependencies.Length > 0) {
            throw new XamlNexusRecipeException(
                "XR1506",
                $"Missing dependencies: {string.Join(", ", missingDependencies)}.");
        }
        string[] conflicts = descriptor.Conflicts.Where(installed.Contains).ToArray();
        if (conflicts.Length > 0) {
            throw new XamlNexusRecipeException(
                "XR1507",
                $"Conflicting modules are installed: {string.Join(", ", conflicts)}.");
        }
    }

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

    private static (Version Version, string? Prerelease) ParseSemanticVersion(string value) {
        string[] parts = value.Split('-', 2);
        return (Version.Parse(parts[0]), parts.Length == 2 ? parts[1] : null);
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/');

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
            throw new XamlNexusRecipeException("XR1803", "The project manifest changed after batch planning. Preview the operation again.");
        var freshManifest = XamlNexusProjectManifestStore.Load(project.ManifestPath);
        if (System.Text.Json.JsonSerializer.Serialize(freshManifest) != System.Text.Json.JsonSerializer.Serialize(project.Manifest))
            throw new XamlNexusRecipeException("XR1304", "The project manifest changed after the context was loaded. Reload the project and preview the operation again.");
        // Plans may have been resolved before another writer completed. Check again under the lease.
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
                            "XR1302",
                            $"Unsupported file change kind: {resolved.Change.Kind}");
                }
                appliedChanges.Add(resolved);
            }

            XamlNexusProjectManifest updatedManifest = updateManifest(project.Manifest);
            XamlNexusProjectManifestStore.Save(project.ManifestPath, updatedManifest);

            return new XamlNexusRecipeApplyResult(
                recipeId,
                recipeVersion,
                changes.Select(change => change.Change.RelativePath).ToArray());
        }
        catch (Exception applyException) {
            var rollbackErrors = new List<Exception>();
            foreach (ResolvedRecipeFileChange change in appliedChanges.AsEnumerable().Reverse()) {
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
                try {
                    if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                        Directory.Delete(directory);
                }
                catch (Exception rollbackException) {
                    rollbackErrors.Add(rollbackException);
                }
            }

            string rollbackMessage = rollbackErrors.Count == 0
                ? "All file changes were rolled back."
                : $"Rollback encountered {rollbackErrors.Count} additional error(s).";
            throw new XamlNexusRecipeException(
                "XR1303",
                $"Recipe '{recipeId}' failed. {rollbackMessage}",
                applyException);
        }
    }

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

public static class XamlNexusRecipeHash {
    public static string Compute(byte[] content) {
        ArgumentNullException.ThrowIfNull(content);
        return Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
    }

    public static string ComputeFile(string path) => Compute(File.ReadAllBytes(path));
}
