using System.Globalization;
using XamlNexus.Common.Resources;

namespace XamlNexus.Common.Recipes;

/// <summary>单个错误场景的定义；参数数量明确登记，避免资源占位符与调用方不一致。</summary>
public sealed record RecipeErrorDefinition(string Code, string ResourceKey, int ArgumentCount) {
    public string Format(CultureInfo culture, IReadOnlyList<object?> arguments) =>
        LocalizedMessageFormatter.Format(ResourceKey, ArgumentCount, culture, arguments);
}
