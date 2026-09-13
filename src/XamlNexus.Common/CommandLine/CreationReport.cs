using Spectre.Console;
using XamlNexus.Common.Generators;
using XamlNexus.Common.Utils;

namespace XamlNexus.Common.CommandLine;

public static class CreationReport {
    internal static void RunFinishing(Action action) {
        string message = LanguageRegistry.GetText("Creation_Finishing");
        if (Console.IsOutputRedirected || !AnsiConsole.Profile.Capabilities.Interactive) {
            AnsiConsole.WriteLine(message);
            action();
            return;
        }
        AnsiConsole.Progress().AutoRefresh(true).AutoClear(true)
            .Columns(new TaskDescriptionColumn(), new SpinnerColumn())
            .Start(context => {
                var task = context.AddTask(message);
                task.IsIndeterminate = true;
                context.Refresh();
                action();
                task.IsIndeterminate = false;
                task.Value = task.MaxValue;
                task.StopTask();
            });
    }

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
        if (hasAddedCapabilities) AnsiConsole.WriteLine(LanguageRegistry.GetText("Creation_ReviewRecipes"));

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($" [bold green]{LanguageRegistry.GetI18n(LangKeys.Text_Success)}[/]");
    }
}
