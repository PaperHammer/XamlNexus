using XamlNexus.Common.Generators;
using XamlNexus.Common.Recipes;
using XamlNexus.Common.Utils;

namespace XamlNexus.Common.Projects;

public sealed record CompositionRequest(ProjectConfig Project, string Profile, IReadOnlyList<string> Features);

public static class CompositionPlanner {
    public static IReadOnlyList<IXamlNexusRecipe> Resolve(
        string preset, string profile, IEnumerable<string> features, IXamlNexusRecipeCatalog catalog,
        IEnumerable<XamlNexusManagedModule>? installedModules = null) =>
        ResolveCore(preset, profile, features, catalog, (installedModules ?? []).Select(module => module.Id), false);

    public static IReadOnlyList<IXamlNexusRecipe> ResolveForCreation(
        string preset, string profile, IEnumerable<string> features, IXamlNexusRecipeCatalog catalog,
        IEnumerable<string> includedModuleIds) =>
        ResolveCore(preset, profile, features, catalog, includedModuleIds, true);

    private static IReadOnlyList<IXamlNexusRecipe> ResolveCore(
        string preset, string profile, IEnumerable<string> features, IXamlNexusRecipeCatalog catalog,
        IEnumerable<string> existingIds, bool reuseRequestedModules) {
        if (profile is not ("standard" or "basic")) throw new ArgumentException("Profile must be standard or basic.");
        if (preset is not ("winui" or "hybrid")) throw new ArgumentException("Unsupported architecture.");
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var installed = existingIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var completed = new HashSet<string>(installed, StringComparer.OrdinalIgnoreCase);
        var result = new List<IXamlNexusRecipe>();
        void Visit(string id) {
            if (completed.Contains(id)) return;
            if (!visiting.Add(id)) throw new InvalidOperationException($"Cyclic Recipe dependency at '{id}'.");
            var recipe = catalog.Find(id) ?? throw new InvalidOperationException($"Unknown Recipe '{id}'.");
            if (!recipe.Descriptor.SupportedPresets.Contains(preset, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Recipe '{id}' does not support preset '{preset}'.");
            foreach (string dependency in recipe.Descriptor.Dependencies.Order(StringComparer.OrdinalIgnoreCase)) Visit(dependency);
            visiting.Remove(id);
            completed.Add(id);
            result.Add(recipe);
        }
        foreach (string id in features.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)) {
            if (installed.Contains(id) && !reuseRequestedModules)
                throw new XamlNexusRecipeException("XR1102", $"Module '{id}' is already installed.");
            Visit(id);
        }
        foreach (var recipe in catalog.Recipes.Where(recipe => completed.Contains(recipe.Descriptor.Id))) {
            foreach (string conflict in recipe.Descriptor.Conflicts)
                if (completed.Contains(conflict))
                    throw new InvalidOperationException($"Recipe '{recipe.Descriptor.Id}' conflicts with '{conflict}'.");
        }
        return result.AsReadOnly();
    }
}

public static class ProjectComposer {
    public static string Create(CompositionRequest request, IGenerator generator, IXamlNexusRecipeCatalog catalog) {
        var config = request.Project;
        string preset = config.Framework switch {
            FrameworkType.Winui3 => "winui",
            FrameworkType.Winui3_Wpf => "hybrid",
            _ => throw new ArgumentException("Unsupported architecture."),
        };
        var recipes = CompositionPlanner.ResolveForCreation(preset, request.Profile, request.Features, catalog,
            generator.GetIncludedModuleIds(request.Profile));
        if (string.IsNullOrWhiteSpace(config.SlnName) || config.SlnName is "." or ".."
            || Path.GetFileName(config.SlnName) != config.SlnName)
            throw new ArgumentException("The project name must be a single directory name.");

        string parent = Path.GetFullPath(config.OutputPath);
        Directory.CreateDirectory(parent);
        string destination = Path.Combine(parent, config.SlnName);
        string staging = Directory.CreateTempSubdirectory("xamlnexus-create-").FullName;
        bool ownsDestination = false;
        Exception? failure = null;
        try {
            var stagedConfig = new ProjectConfig {
                Profile = request.Profile,
                SlnName = config.SlnName, OutputPath = staging, Language = config.Language,
                Framework = config.Framework, SlnType = config.SlnType,
            };
            if (!generator.Generate(stagedConfig, reportSuccess: false))
                throw new InvalidOperationException("Application scaffold generation failed.");
            CommandLine.CreationReport.RunFinishing(() => {
                string projectRoot = Path.Combine(stagedConfig.OutputPath, config.SlnName);
                foreach (var recipe in recipes) {
                    // Reload after each installation so dependencies and expected hashes reflect prior changes.
                    var project = XamlNexusProjectLocator.Locate(projectRoot);
                    XamlNexusRecipeTransaction.Apply(project, recipe);
                }
                var report = XamlNexusProjectValidator.Validate(XamlNexusProjectLocator.Locate(projectRoot));
                if (!report.IsValid) throw new InvalidOperationException("The composed project failed validation.");
                // Reserve a new destination atomically; never copy into a directory
                // another process created after the initial existence check.
                destination = ProjectOutputReservation.Create(parent, config.SlnName);
                ownsDestination = true;
                // Staging may live on another volume. Copy source files first and solution
                // discovery files last so IDEs only load the completed composition.
                var files = Directory.EnumerateFiles(projectRoot, "*", SearchOption.AllDirectories)
                    .Select(path => (Source: path, Relative: Path.GetRelativePath(projectRoot, path)))
                    .Where(file => !file.Relative.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj" or ".vs"))
                    .OrderBy(file => Path.GetExtension(file.Source) is ".sln" or ".slnx" ? 2 : Path.GetExtension(file.Source) == ".csproj" ? 1 : 0)
                    .ThenBy(file => file.Relative, StringComparer.Ordinal).ToArray();
                foreach (var file in files) {
                    string target = Path.Combine(destination, file.Relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(file.Source, target, overwrite: false);
                }
                if (!XamlNexusProjectValidator.Validate(XamlNexusProjectLocator.Locate(destination)).IsValid)
                    throw new InvalidOperationException("The published project failed validation.");
            });
            return destination;
        }
        catch (Exception exception) {
            failure = exception;
            if (ownsDestination) {
                try { Directory.Delete(destination, recursive: true); }
                catch (Exception cleanupException) {
                    failure.Data["CleanupError"] = $"Incomplete project '{destination}' could not be removed: {cleanupException.Message}";
                }
            }
            throw;
        }
        finally {
            // Only remove the unique temporary directory created by this invocation.
            try {
                if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
            }
            catch (Exception cleanupException) {
                if (failure is not null) failure.Data["CleanupError"] = $"{failure.Data["CleanupError"]} Temporary directory '{staging}' could not be removed: {cleanupException.Message}";
                else System.Diagnostics.Trace.TraceWarning($"Could not remove staging directory '{staging}': {cleanupException.Message}");
            }
        }
    }
}
