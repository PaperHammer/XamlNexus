using Spectre.Console;
using XamlNexus.Common.Generators;
using XamlNexus.Common.Utils;

namespace XamlNexus.Tooling.CommandLine;

/// <summary>将同步领域进度转换为终端展示；输出重定向时只记录阶段变化。</summary>
public static class GenerationConsole {
    public static T Run<T>(ProjectConfig config, Func<Action<GenerationProgress>, T> action) {
        AnsiConsole.MarkupLine($"\n[bold blue]{ConsoleText.GetI18n(LangKeys.Text_Start)} - {Markup.Escape(config.SlnName)}[/]");
        if (Console.IsOutputRedirected || !AnsiConsole.Profile.Capabilities.Interactive) {
            GenerationStage? previous = null;
            return action(update => {
                if (update.Stage == previous) return;
                previous = update.Stage;
                AnsiConsole.WriteLine(Label(update.Stage));
            });
        }
        return AnsiConsole.Progress().AutoRefresh(true)
            .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new SpinnerColumn())
            .Start(context => {
                var tasks = new Dictionary<GenerationStage, ProgressTask>();
                return action(update => {
                    if (!tasks.TryGetValue(update.Stage, out var task)) {
                        task = context.AddTask(Markup.Escape(Label(update.Stage)), maxValue: Math.Max(1, update.Total));
                        tasks.Add(update.Stage, task);
                    }
                    task.Description = Markup.Escape(Label(update.Stage) + (update.Item is null ? "" : $" — {update.Item}"));
                    task.MaxValue = Math.Max(1, update.Total);
                    task.Value = Math.Clamp(update.Completed, 0, update.Total);
                });
            });
    }

    private static string Label(GenerationStage stage) => LanguageRegistry.GetText(stage switch {
        GenerationStage.CopyModules => LangKeys.Text_Generating_Module,
        GenerationStage.CreateSolution => LangKeys.Text_Generating_Solution,
        GenerationStage.WriteManifest => "Creation_Finishing",
        GenerationStage.ApplyRecipes => "Generation_ApplyRecipes",
        GenerationStage.ValidateProject => "Generation_ValidateProject",
        GenerationStage.PublishProject => "Generation_PublishProject",
        _ => throw new ArgumentOutOfRangeException(nameof(stage)),
    });
}
