using XamlNexus.Common.Projects;
using XamlNexus.Common.Recipes;

namespace XamlNexus.Tooling.Diagnostics;

public sealed record XamlNexusRecipeStatus(
    string Id,
    string InstalledVersion,
    string? AvailableVersion,
    string State);

public sealed record XamlNexusProjectStatusReport(
    string RootDirectory,
    string ToolVersion,
    string GeneratorVersion,
    string ScaffoldState,
    XamlNexusProjectIdentity Project,
    XamlNexusProjectValidationReport Validation,
    XamlNexusDoctorReport Doctor,
    IReadOnlyList<XamlNexusRecipeStatus> Recipes,
    IReadOnlyList<string> SuggestedCommands) {
    public bool IsHealthy => Validation.IsValid && Doctor.IsHealthy;
    public int RecipeUpdateCount => Recipes.Count(recipe => recipe.State == "updateAvailable");
}

public static class XamlNexusProjectStatus {
    public static XamlNexusProjectStatusReport Create(
        XamlNexusProjectContext context,
        IXamlNexusRecipeCatalog catalog,
        string toolVersion,
        bool probeEnvironment = true) {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolVersion);

        XamlNexusProjectValidationReport validation = XamlNexusProjectValidator.Validate(context);
        XamlNexusDoctorReport doctor = XamlNexusDoctor.Diagnose(context, catalog, probeEnvironment);
        string scaffoldState = CompareState(toolVersion, context.Manifest.GeneratorVersion);
        var recipes = context.Manifest.Modules
            .Where(module => module.Source.Equals("recipe", StringComparison.OrdinalIgnoreCase))
            .OrderBy(module => module.Id, StringComparer.OrdinalIgnoreCase)
            .Select(module => {
                IXamlNexusRecipe? available = catalog.Find(module.Id);
                return new XamlNexusRecipeStatus(
                    module.Id,
                    module.Version,
                    available?.Descriptor.Version,
                    available is null ? "unavailable" : CompareState(available.Descriptor.Version, module.Version));
            })
            .ToArray();

        var commands = new List<string>();
        if (!validation.IsValid) commands.Add("xamlnexus validate");
        if (!doctor.IsHealthy) commands.Add("xamlnexus doctor");
        if (recipes.Any(recipe => recipe.State == "updateAvailable"))
            commands.Add("xamlnexus update --all --dry-run");
        if (scaffoldState == "updateAvailable")
            commands.Add("xamlnexus upgrade --dry-run");

        return new(
            context.RootDirectory,
            toolVersion,
            context.Manifest.GeneratorVersion,
            scaffoldState,
            context.Manifest.Project,
            validation,
            doctor,
            recipes,
            commands.AsReadOnly());
    }

    private static string CompareState(string availableVersion, string installedVersion) {
        if (!XamlNexusRecipeVersion.TryCompare(availableVersion, installedVersion, out int comparison))
            return availableVersion.Equals(installedVersion, StringComparison.OrdinalIgnoreCase) ? "current" : "unknown";
        return comparison switch {
            > 0 => "updateAvailable",
            < 0 => "toolOlder",
            _ => "current",
        };
    }
}
