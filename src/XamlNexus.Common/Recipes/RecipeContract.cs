using System.Text;
using System.Text.RegularExpressions;
using XamlNexus.Common.Projects;

namespace XamlNexus.Common.Recipes;

public sealed class XamlNexusRecipeDescriptor {
    public required string Id { get; init; }

    public required string Version { get; init; }

    public required string DisplayName { get; init; }

    public string Description { get; init; } = string.Empty;

    public required IReadOnlyList<string> SupportedPresets { get; init; }

    public IReadOnlyList<string> Dependencies { get; init; } = [];

    public IReadOnlyList<string> Conflicts { get; init; } = [];
}

public interface IXamlNexusRecipe {
    XamlNexusRecipeDescriptor Descriptor { get; }

    XamlNexusRecipePlan CreatePlan(XamlNexusRecipeContext context);
}

public interface IXamlNexusRecipeRemovalPlanProvider {
    IReadOnlyList<XamlNexusRecipeProjectOperation> CreateRemovalOperations(
        XamlNexusRecipeContext context);
}

public interface IXamlNexusRecipeUpdatePlanProvider {
    IReadOnlyList<XamlNexusRecipeProjectOperation> CreateUpdateOperations(
        XamlNexusRecipeContext context,
        string installedVersion);
}

public sealed record XamlNexusRecipeContext(
    string RootDirectory,
    XamlNexusProjectManifest Manifest);

public sealed class XamlNexusRecipePlan {
    public required IReadOnlyList<XamlNexusRecipeFileChange> Changes { get; init; }

    public IReadOnlyList<XamlNexusRecipeProjectOperation> ProjectOperations { get; init; } = [];
}

public abstract record XamlNexusRecipeProjectOperation;

public sealed record AddPackageReferenceOperation(
    string ProjectPath,
    string PackageId,
    string Version) : XamlNexusRecipeProjectOperation;

public sealed record EnsurePackageReferenceOperation(
    string ProjectPath,
    string PackageId,
    string MinimumVersion) : XamlNexusRecipeProjectOperation;

public sealed record AddProjectReferenceOperation(
    string ProjectPath,
    string ReferencedProjectPath) : XamlNexusRecipeProjectOperation;

public sealed record RemoveProjectReferenceOperation(
    string ProjectPath,
    string ReferencedProjectPath) : XamlNexusRecipeProjectOperation;

public sealed record AddProjectToSolutionOperation(
    string SolutionPath,
    string ProjectPath) : XamlNexusRecipeProjectOperation;

public sealed record RemoveProjectFromSolutionOperation(
    string SolutionPath,
    string ProjectPath) : XamlNexusRecipeProjectOperation;

public sealed record AddProtobufOperation(
    string ProjectPath,
    string ProtoPath) : XamlNexusRecipeProjectOperation;

public sealed record RemoveProtobufOperation(
    string ProjectPath,
    string ProtoPath) : XamlNexusRecipeProjectOperation;

public enum XamlNexusRecipeFileChangeKind {
    Create,
    Replace,
    Delete,
}

public sealed class XamlNexusRecipeFileChange {
    public required XamlNexusRecipeFileChangeKind Kind { get; init; }

    public required string RelativePath { get; init; }

    public byte[]? Content { get; init; }

    public string? ExpectedSha256 { get; init; }

    public static XamlNexusRecipeFileChange CreateText(string relativePath, string content) => new() {
        Kind = XamlNexusRecipeFileChangeKind.Create,
        RelativePath = relativePath,
        Content = Encoding.UTF8.GetBytes(content),
    };

    public static XamlNexusRecipeFileChange ReplaceText(
        string relativePath,
        string content,
        string expectedSha256) => new() {
        Kind = XamlNexusRecipeFileChangeKind.Replace,
        RelativePath = relativePath,
        Content = Encoding.UTF8.GetBytes(content),
        ExpectedSha256 = expectedSha256,
    };

    public static XamlNexusRecipeFileChange Delete(
        string relativePath,
        string expectedSha256) => new() {
        Kind = XamlNexusRecipeFileChangeKind.Delete,
        RelativePath = relativePath,
        ExpectedSha256 = expectedSha256,
    };
}

public sealed class XamlNexusRecipeException : Exception {
    public XamlNexusRecipeException(string code, string message)
        : base($"{code}: {message}") {
        Code = code;
    }

    public XamlNexusRecipeException(string code, string message, Exception innerException)
        : base($"{code}: {message}", innerException) {
        Code = code;
    }

    public string Code { get; }
}

public static partial class XamlNexusRecipeContract {
    public static void ValidateDescriptor(XamlNexusRecipeDescriptor descriptor) {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (!IsValidId(descriptor.Id))
            throw new XamlNexusRecipeException("XR1001", "Recipe id must use lowercase kebab-case.");
        if (!SemanticVersionRegex().IsMatch(descriptor.Version))
            throw new XamlNexusRecipeException("XR1002", "Recipe version must be a semantic version such as 1.0.0.");
        if (string.IsNullOrWhiteSpace(descriptor.DisplayName))
            throw new XamlNexusRecipeException("XR1003", "Recipe display name is required.");
        if (descriptor.SupportedPresets.Count == 0 ||
            descriptor.SupportedPresets.Any(preset => preset is not ("winui" or "hybrid"))) {
            throw new XamlNexusRecipeException(
                "XR1004",
                "Supported presets must contain one or more of: winui, hybrid.");
        }

        ValidateIds(descriptor.Dependencies, "dependency");
        ValidateIds(descriptor.Conflicts, "conflict");

        var dependencies = descriptor.Dependencies.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var conflicts = descriptor.Conflicts.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (dependencies.Count != descriptor.Dependencies.Count)
            throw new XamlNexusRecipeException("XR1005", "Recipe dependencies must be unique.");
        if (conflicts.Count != descriptor.Conflicts.Count)
            throw new XamlNexusRecipeException("XR1006", "Recipe conflicts must be unique.");
        if (dependencies.Contains(descriptor.Id) || conflicts.Contains(descriptor.Id))
            throw new XamlNexusRecipeException("XR1007", "A Recipe cannot depend on or conflict with itself.");
        if (dependencies.Overlaps(conflicts))
            throw new XamlNexusRecipeException("XR1008", "A module cannot be both a dependency and a conflict.");
    }

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
                "XR1101",
                $"Recipe '{descriptor.Id}' does not support preset '{manifest.Project.Preset}'.");
        }
        if (installed.Contains(descriptor.Id))
            throw new XamlNexusRecipeException("XR1102", $"Module '{descriptor.Id}' is already installed.");

        string[] missingDependencies = descriptor.Dependencies
            .Where(dependency => !installed.Contains(dependency))
            .ToArray();
        if (missingDependencies.Length > 0) {
            throw new XamlNexusRecipeException(
                "XR1103",
                $"Missing dependencies: {string.Join(", ", missingDependencies)}.");
        }

        string[] activeConflicts = descriptor.Conflicts
            .Where(installed.Contains)
            .ToArray();
        if (activeConflicts.Length > 0) {
            throw new XamlNexusRecipeException(
                "XR1104",
                $"Conflicting modules are installed: {string.Join(", ", activeConflicts)}.");
        }
    }

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
                throw new XamlNexusRecipeException("XR1201", "Recipe plan contains an empty file change.");
            if (string.IsNullOrWhiteSpace(change.RelativePath) || Path.IsPathRooted(change.RelativePath))
                throw new XamlNexusRecipeException("XR1202", "Recipe file paths must be relative to the project root.");

            string fullPath = Path.GetFullPath(Path.Combine(root, change.RelativePath));
            if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                throw new XamlNexusRecipeException("XR1203", $"Recipe path escapes the project: {change.RelativePath}");
            if (Path.GetFileName(fullPath).Equals("xamlnexus.json", StringComparison.OrdinalIgnoreCase))
                throw new XamlNexusRecipeException("XR1204", "Recipes cannot edit xamlnexus.json directly.");
            if (!paths.Add(fullPath))
                throw new XamlNexusRecipeException("XR1205", $"Recipe changes the same path more than once: {change.RelativePath}");

            ProjectPathSafety.EnsureNoLinks(fullPath);
            if (Directory.Exists(fullPath))
                throw new XamlNexusRecipeException("XR1212", $"Recipe file target is an existing directory: {change.RelativePath}");
            string? parentPath = Path.GetDirectoryName(fullPath);
            while (!string.IsNullOrEmpty(parentPath) &&
                   !parentPath.Equals(root, StringComparison.OrdinalIgnoreCase)) {
                if (File.Exists(parentPath)) {
                    throw new XamlNexusRecipeException(
                        "XR1213",
                        $"A parent path is an existing file: {change.RelativePath}");
                }
                parentPath = Path.GetDirectoryName(parentPath);
            }

            bool exists = File.Exists(fullPath);
            switch (change.Kind) {
                case XamlNexusRecipeFileChangeKind.Create when exists:
                    throw new XamlNexusRecipeException("XR1206", $"Recipe would overwrite an existing file: {change.RelativePath}");
                case XamlNexusRecipeFileChangeKind.Create when change.Content is null:
                    throw new XamlNexusRecipeException("XR1207", $"Create operation has no content: {change.RelativePath}");
                case XamlNexusRecipeFileChangeKind.Replace or XamlNexusRecipeFileChangeKind.Delete when !exists:
                    throw new XamlNexusRecipeException("XR1208", $"Recipe expects a file that does not exist: {change.RelativePath}");
                case XamlNexusRecipeFileChangeKind.Replace when change.Content is null:
                    throw new XamlNexusRecipeException("XR1209", $"Replace operation has no content: {change.RelativePath}");
            }

            if (change.Kind is XamlNexusRecipeFileChangeKind.Replace or XamlNexusRecipeFileChangeKind.Delete) {
                if (!IsSha256(change.ExpectedSha256))
                    throw new XamlNexusRecipeException("XR1210", $"A valid expected SHA-256 is required for: {change.RelativePath}");

                string actualHash = XamlNexusRecipeHash.ComputeFile(fullPath);
                if (!actualHash.Equals(change.ExpectedSha256, StringComparison.OrdinalIgnoreCase)) {
                    throw new XamlNexusRecipeException(
                        "XR1211",
                        $"File was modified and does not match the Recipe precondition: {change.RelativePath}");
                }
            }

            resolved.Add(new ResolvedRecipeFileChange(change, fullPath));
        }

        IReadOnlyList<ResolvedRecipeFileChange> projectChanges =
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

    private static void ValidateIds(IReadOnlyList<string> ids, string kind) {
        ArgumentNullException.ThrowIfNull(ids);
        string? invalid = ids.FirstOrDefault(id => !IsValidId(id));
        if (invalid is not null)
            throw new XamlNexusRecipeException("XR1009", $"Invalid {kind} module id '{invalid}'.");
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex RecipeIdRegex();

    [GeneratedRegex("^[0-9]+\\.[0-9]+\\.[0-9]+(?:-[0-9A-Za-z.-]+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex SemanticVersionRegex();

    [GeneratedRegex("^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Regex();
}

internal sealed record ResolvedRecipeFileChange(
    XamlNexusRecipeFileChange Change,
    string FullPath,
    bool IsSharedProjectFile = false);
