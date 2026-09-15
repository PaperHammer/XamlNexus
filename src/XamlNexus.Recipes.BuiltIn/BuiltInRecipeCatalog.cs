using XamlNexus.Common.Recipes;

namespace XamlNexus.Recipes.BuiltIn;

public static class BuiltInRecipeCatalog {
    public static IXamlNexusRecipeCatalog Create() => new XamlNexusRecipeCatalog([
        new EditorConfigRecipe(),
        new SettingsRecipe(),
        new SqliteRecipe(),
        new SystemTrayRecipe(),
        new AppUpdateRecipe(),
    ]);
}
