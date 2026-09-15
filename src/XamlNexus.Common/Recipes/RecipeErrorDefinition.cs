using System.Globalization;
using System.Resources;

namespace XamlNexus.Common.Recipes;

/// <summary>单个错误场景的定义；参数数量明确登记，避免资源占位符与调用方不一致。</summary>
public sealed record RecipeErrorDefinition(string Code, string ResourceKey, int ArgumentCount) {
    private static readonly ResourceManager Resources =
        new("XamlNexus.Common.Resources.Strings", typeof(RecipeErrorDefinition).Assembly);

    public string Format(CultureInfo culture, IReadOnlyList<object?> arguments) {
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Count != ArgumentCount)
            throw new ArgumentException($"{ResourceKey} requires {ArgumentCount} arguments.", nameof(arguments));
        string template = Resources.GetString(ResourceKey, culture)
            ?? throw new MissingManifestResourceException($"Missing error resource: {ResourceKey}");
        return string.Format(culture, template, arguments.ToArray());
    }
}
