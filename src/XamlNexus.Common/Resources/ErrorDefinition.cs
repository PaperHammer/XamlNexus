using System.Globalization;
using XamlNexus.Common.Utils;

namespace XamlNexus.Common.Resources;

/// <summary>稳定错误码与场景资源的绑定；业务逻辑只传入动态参数。</summary>
public sealed record ErrorDefinition(string Code, string ResourceKey, int ArgumentCount) {
    public string Format(CultureInfo culture, IReadOnlyList<object?> arguments) =>
        LocalizedMessageFormatter.Format(ResourceKey, ArgumentCount, culture, arguments);

    public string GetMessage(params object?[] arguments) => Format(
        CultureInfo.GetCultureInfo(LanguageRegistry.CurrentLanguage == LanguageType.Chinese ? "zh-CN" : "en"), arguments);
}
