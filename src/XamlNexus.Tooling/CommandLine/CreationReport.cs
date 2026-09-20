using Spectre.Console;
using XamlNexus.Common.Generators;
using XamlNexus.Common.Utils;

namespace XamlNexus.Tooling.CommandLine;

public static class CreationReport {
    public static void Write(ProjectConfig config, string outputRoot, bool hasAddedCapabilities = false) {
        var table = new Table().Border(TableBorder.Rounded);

        table.AddColumn($"[cyan]{ConsoleText.GetI18n(LangKeys.Text_Property)}[/]");
        table.AddColumn($"[green]{ConsoleText.GetI18n(LangKeys.Text_Value)}[/]");

        table.AddRow(ConsoleText.GetI18n(LangKeys.Text_Project), Markup.Escape(config.SlnName));
        table.AddRow(ConsoleText.GetI18n(LangKeys.Text_Framework), config.Framework.ToString());
        table.AddRow(ConsoleText.GetI18n(LangKeys.Text_Format), config.SlnType.ToString());
        table.AddRow(ConsoleText.GetI18n(LangKeys.Text_OutputPath), Markup.Escape(outputRoot));

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine(string.Format(LanguageRegistry.GetText("Creation_Run"), outputRoot));
        if (hasAddedCapabilities) AnsiConsole.WriteLine(LanguageRegistry.GetText("Creation_ReviewRecipes"));

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($" [grey][[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}]][/] [bold green]{ConsoleText.GetI18n(LangKeys.Text_Success)}[/]");
    }
}
