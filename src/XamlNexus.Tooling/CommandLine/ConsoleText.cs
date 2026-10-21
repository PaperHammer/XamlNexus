using Spectre.Console;
using XamlNexus.Common.Utils;

namespace XamlNexus.Tooling.CommandLine;

internal static class ConsoleText {
    public static string GetI18n(string key) => Markup.Escape(LanguageRegistry.GetText(key));
}
