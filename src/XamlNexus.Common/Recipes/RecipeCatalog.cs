namespace XamlNexus.Common.Recipes;

/// <summary>Recipe 定义目录：提供可用组件列表和按 ID 查找的入口，不代表项目已安装状态</summary>
public interface IXamlNexusRecipeCatalog {
    IReadOnlyList<IXamlNexusRecipe> Recipes { get; }

    IXamlNexusRecipe? Find(string id);
}

/// <summary>在内存中索引 Recipe；构造时验证描述并拒绝重复 ID，查找忽略大小写</summary>
public sealed class XamlNexusRecipeCatalog : IXamlNexusRecipeCatalog {
    private readonly Dictionary<string, IXamlNexusRecipe> _recipes;

    /// <summary>验证每个 Recipe 并建立索引，同时按 ID 排序生成稳定的展示列表</summary>
    public XamlNexusRecipeCatalog(IEnumerable<IXamlNexusRecipe> recipes) {
        ArgumentNullException.ThrowIfNull(recipes);

        _recipes = new Dictionary<string, IXamlNexusRecipe>(StringComparer.OrdinalIgnoreCase);
        foreach (IXamlNexusRecipe recipe in recipes) {
            ArgumentNullException.ThrowIfNull(recipe);
            XamlNexusRecipeContract.ValidateDescriptor(recipe.Descriptor);
            if (!_recipes.TryAdd(recipe.Descriptor.Id, recipe)) {
                throw new XamlNexusRecipeException(
                    XamlNexusRecipeErrors.DuplicateCatalogId, [recipe.Descriptor.Id]);
            }
        }

        Recipes = _recipes.Values
            .OrderBy(recipe => recipe.Descriptor.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<IXamlNexusRecipe> Recipes { get; }

    /// <summary>按 ID 查找定义；不存在时返回 null，空白 ID 则抛出参数异常</summary>
    public IXamlNexusRecipe? Find(string id) {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _recipes.GetValueOrDefault(id);
    }
}
