using XamlNexus.Common.Recipes;

namespace XamlNexus.Recipes.BuiltIn;

public sealed class AppUpdateRecipe : IXamlNexusRecipe {
    public XamlNexusRecipeDescriptor Descriptor { get; } = new() {
        Id = "app-update",
        Version = "1.0.0",
        DisplayName = "Application updates",
        Description = "Adds the HTTPS update client, verified downloads and installer lifecycle to pure WinUI.",
        SupportedPresets = ["winui"],
        Dependencies = ["settings"],
    };

    public XamlNexusRecipePlan CreatePlan(XamlNexusRecipeContext context) {
        string name = context.Manifest.Project.Name;
        var files = new Dictionary<string, string> {
            ["JsonAppUpdateSource.cs"] = $"{name}.Common/Updates/JsonAppUpdateSource.cs",
            ["VerifiedUpdateDownloader.cs"] = $"{name}.Common/Updates/VerifiedUpdateDownloader.cs",
            ["AppUpdateLifecycle.cs"] = $"{name}.Common/Updates/AppUpdateLifecycle.cs",
            ["AppUpdaterClient.cs"] = $"{name}.Models/Datas/AppUpdaterClient.cs",
            ["Lifecycle"] = $"{name}.UI/Modules/AppUpdateModule.cs",
        };
        var changes = files.Select(file => {
            using Stream stream = typeof(AppUpdateRecipe).Assembly.GetManifestResourceStream(
                $"XamlNexus.Recipes.BuiltIn.Assets.AppUpdate.{file.Key}.txt")
                ?? throw new InvalidOperationException($"Missing app-update asset: {file.Key}");
            using var reader = new StreamReader(stream);
            return XamlNexusRecipeFileChange.CreateText(file.Value,
                reader.ReadToEnd().Replace("Winui3_XamlNexus", name, StringComparison.Ordinal));
        }).ToList();
        changes.Add(XamlNexusRecipeFileChange.CreateText("app-update.README.md",
            RecipeReadmeResources.Load("AppUpdate/app-update.README.md", new Dictionary<string, string> {
                ["ProjectName"] = name,
            })));
        changes.Add(XamlNexusRecipeFileChange.CreateText("app-update.README.zh-CN.md",
            RecipeReadmeResources.Load("AppUpdate/app-update.README.zh-CN.md", new Dictionary<string, string> {
                ["ProjectName"] = name,
            })));
        return new XamlNexusRecipePlan { Changes = changes };
    }
}
