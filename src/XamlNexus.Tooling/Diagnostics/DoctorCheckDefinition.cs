using System.Globalization;
using XamlNexus.Common.Resources;
using XamlNexus.Common.Utils;

namespace XamlNexus.Tooling.Diagnostics;

/// <summary>绑定稳定编号、分类、严重程度和文案资源；检查逻辑只提供格式化参数及路径。</summary>
public sealed record DoctorCheckDefinition(
    string Code, string Category, XamlNexusDoctorSeverity Severity, string ResourceKey, int ArgumentCount) {
    public string Format(CultureInfo culture, IReadOnlyList<object?> arguments) =>
        LocalizedMessageFormatter.Format(ResourceKey, ArgumentCount, culture, arguments);

    public XamlNexusDoctorCheck Create(object?[]? arguments = null, string? path = null) =>
        new(Severity, Code, Category,
            Format(CultureInfo.GetCultureInfo(LanguageRegistry.CurrentLanguage == LanguageType.Chinese ? "zh-CN" : "en"),
                arguments ?? []), path);
}
