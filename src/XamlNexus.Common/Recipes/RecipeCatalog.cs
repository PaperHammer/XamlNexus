namespace XamlNexus.Common.Recipes;

public interface IXamlNexusRecipeCatalog {
    IReadOnlyList<IXamlNexusRecipe> Recipes { get; }

    IXamlNexusRecipe? Find(string id);
}

public sealed class XamlNexusRecipeCatalog : IXamlNexusRecipeCatalog {
    private readonly Dictionary<string, IXamlNexusRecipe> _recipes;

    public XamlNexusRecipeCatalog(IEnumerable<IXamlNexusRecipe> recipes) {
        ArgumentNullException.ThrowIfNull(recipes);

        _recipes = new Dictionary<string, IXamlNexusRecipe>(StringComparer.OrdinalIgnoreCase);
        foreach (IXamlNexusRecipe recipe in recipes) {
            ArgumentNullException.ThrowIfNull(recipe);
            XamlNexusRecipeContract.ValidateDescriptor(recipe.Descriptor);
            if (!_recipes.TryAdd(recipe.Descriptor.Id, recipe)) {
                throw new XamlNexusRecipeException(
                    "XR1401",
                    $"Recipe catalog contains duplicate id '{recipe.Descriptor.Id}'.");
            }
        }

        Recipes = _recipes.Values
            .OrderBy(recipe => recipe.Descriptor.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<IXamlNexusRecipe> Recipes { get; }

    public IXamlNexusRecipe? Find(string id) {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _recipes.GetValueOrDefault(id);
    }
}
