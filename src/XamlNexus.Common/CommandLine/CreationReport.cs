using Spectre.Console;
using XamlNexus.Common.Generators;
using XamlNexus.Common.Utils;

namespace XamlNexus.Common.CommandLine;

public static class CreationReport {
    public static void Write(ProjectConfig config, string outputRoot, bool hasAddedCapabilities = false) {
        var table = new Table().Border(TableBorder.Rounded);

        table.AddColumn($"[cyan]{LanguageRegistry.GetI18n(LangKeys.Text_Property)}[/]");
        table.AddColumn($"[green]{LanguageRegistry.GetI18n(LangKeys.Text_Value)}[/]");

        table.AddRow(LanguageRegistry.GetI18n(LangKeys.Text_Project), Markup.Escape(config.SlnName));
        table.AddRow(LanguageRegistry.GetI18n(LangKeys.Text_Framework), config.Framework.ToString());
        table.AddRow(LanguageRegistry.GetI18n(LangKeys.Text_Format), config.SlnType.ToString());
        table.AddRow(LanguageRegistry.GetI18n(LangKeys.Text_OutputPath), Markup.Escape(outputRoot));

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine(string.Format(LanguageRegistry.GetText("Creation_Run"), outputRoot));
        AnsiConsole.WriteLine(string.Format(LanguageRegistry.GetText("Creation_Page"), config.SlnName));
        AnsiConsole.WriteLine(string.Format(LanguageRegistry.GetText("Creation_ViewModel"), config.SlnName));
        if (hasAddedCapabilities) AnsiConsole.WriteLine(LanguageRegistry.GetText("Creation_ReviewRecipes"));

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($" [bold green]{LanguageRegistry.GetI18n(LangKeys.Text_Success)}[/]");
    }
}
