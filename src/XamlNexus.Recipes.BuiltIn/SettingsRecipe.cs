using XamlNexus.Common.Recipes;

namespace XamlNexus.Recipes.BuiltIn;

public sealed class SettingsRecipe : IXamlNexusRecipe, IXamlNexusRecipeRemovalPlanProvider {
    public XamlNexusRecipeDescriptor Descriptor { get; } = new() {
        Id = "settings", Version = "1.0.0", DisplayName = "Settings panel",
        Description = "Adds the standard settings interface and navigation to a basic application.",
        SupportedPresets = ["winui", "hybrid"],
    };

    public XamlNexusRecipePlan CreatePlan(XamlNexusRecipeContext context) {
        string name = context.Manifest.Project.Name;
        string template = context.Manifest.Project.Preset == "hybrid" ? "Winui3_Wpf_XamlNexus" : "Winui3_XamlNexus";
        string prefix = $"Settings.{context.Manifest.Project.Preset}/";
        var assembly = typeof(SettingsRecipe).Assembly;
        var changes = new List<XamlNexusRecipeFileChange>();
        foreach (string resource in assembly.GetManifestResourceNames().Where(resource => resource.StartsWith(prefix, StringComparison.Ordinal)).Order()) {
            string relative = resource[prefix.Length..].Replace('\\', '/');
            string target = relative.StartsWith("panel/", StringComparison.Ordinal)
                ? $"{name}.AppSettingsPanel/{relative[6..]}" : $"{name}.UI/Modules/{relative[7..]}";
            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            changes.Add(XamlNexusRecipeFileChange.CreateText(target.Replace(template, name, StringComparison.Ordinal),
                reader.ReadToEnd().Replace(template, name, StringComparison.Ordinal)
                    .Replace("WInui3_XamlNexus", name, StringComparison.Ordinal)
                    .Replace("{{DEFAULT_LANGUAGE}}", context.Manifest.Project.Language, StringComparison.Ordinal)));
        }
        if (changes.Count == 0) throw new InvalidOperationException("Missing embedded settings templates.");
        string panel = $"{name}.AppSettingsPanel/{name}.AppSettingsPanel.csproj";
        return new() {
            Changes = changes,
            ProjectOperations = [
                new AddProjectReferenceOperation($"{name}.UI/{name}.UI.csproj", panel),
                new AddProjectToSolutionOperation($"{name}.{context.Manifest.Project.SolutionFormat}", panel),
            ],
        };
    }

    public IReadOnlyList<XamlNexusRecipeProjectOperation> CreateRemovalOperations(XamlNexusRecipeContext context) {
        string name = context.Manifest.Project.Name;
        string panel = $"{name}.AppSettingsPanel/{name}.AppSettingsPanel.csproj";
        return [new RemoveProjectReferenceOperation($"{name}.UI/{name}.UI.csproj", panel),
            new RemoveProjectFromSolutionOperation($"{name}.{context.Manifest.Project.SolutionFormat}", panel)];
    }
}
