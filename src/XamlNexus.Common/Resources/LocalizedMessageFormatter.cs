using System.Globalization;
using System.Resources;

namespace XamlNexus.Common.Resources;

/// <summary>共用资源格式化入口，统一校验诊断和错误定义的参数数量。</summary>
public static class LocalizedMessageFormatter {
    private static readonly ResourceManager Resources =
        new("XamlNexus.Common.Resources.Strings", typeof(LocalizedMessageFormatter).Assembly);

    public static string Format(string resourceKey, int argumentCount, CultureInfo culture, IReadOnlyList<object?> arguments) {
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Count != argumentCount)
            throw new ArgumentException($"{resourceKey} requires {argumentCount} arguments.", nameof(arguments));
        string template = Resources.GetString(resourceKey, culture)
            ?? throw new MissingManifestResourceException($"Missing error resource: {resourceKey}");
        return string.Format(culture, template, arguments.ToArray());
    }
}
