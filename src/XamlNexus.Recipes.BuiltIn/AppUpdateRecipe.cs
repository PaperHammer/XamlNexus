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
        changes.Add(XamlNexusRecipeFileChange.CreateText("app-update.README.md", $"""
            # Application updates

            The app-update Recipe registers the updater and enables its settings entry automatically.
            Configure `Consts.Updates.ManifestUrl` in `{name}.Common/Consts.cs` with your HTTPS
            update manifest. The installer and SHA-256 URLs must also use HTTPS.
            Installing this module does not publish an update feed or configure signing.
            This installer flow targets unpackaged WinUI applications; MSIX uses its distribution channel.
            Remove unchanged module files with `xamlnexus remove app-update`.
            Existing projects with a built-in updater must be migrated before adding this Recipe.
            """));
        return new XamlNexusRecipePlan { Changes = changes };
    }
}
