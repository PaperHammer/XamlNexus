using Spectre.Console;
using XamlNexus.Common.Projects;
using XamlNexus.Recipes.BuiltIn;
using XamlNexus.Tooling.CommandLine;
using XamlNexus.Tooling.Diagnostics;

namespace XamlNexus;

internal partial class Program {
    private static int ShowProjectStatus(string projectPath, bool jsonOutput, string toolVersion) {
        try {
            XamlNexusProjectContext context = XamlNexusProjectLocator.Locate(projectPath);
            XamlNexusProjectStatusReport report = XamlNexusProjectStatus.Create(
                context,
                BuiltInRecipeCatalog.Create(),
                toolVersion);

            if (jsonOutput) {
                WriteJson(new {
                    operation = "status",
                    status = report.IsHealthy ? "healthy" : "unhealthy",
                    root = report.RootDirectory,
                    toolVersion = report.ToolVersion,
                    generatorVersion = report.GeneratorVersion,
                    scaffoldState = report.ScaffoldState,
                    project = report.Project,
                    validation = new {
                        report.Validation.IsValid,
                        warningCount = report.Validation.Issues.Count(issue => issue.Severity == ProjectValidationSeverity.Warning),
                        errorCount = report.Validation.Issues.Count(issue => issue.Severity == ProjectValidationSeverity.Error),
                        report.Validation.Issues,
                    },
                    doctor = new {
                        report.Doctor.IsHealthy,
                        report.Doctor.WarningCount,
                        report.Doctor.ErrorCount,
                        report.Doctor.Checks,
                    },
                    recipes = report.Recipes.Select(recipe => recipe with { Id = RecipeCommandNames.ToCommandName(recipe.Id) }),
                    suggestedCommands = report.SuggestedCommands,
                });
                return report.IsHealthy ? SuccessExitCode : GenerationFailureExitCode;
            }

            var project = new Table().Border(TableBorder.Rounded);
            project.AddColumn("Property");
            project.AddColumn("Value");
            project.AddRow("Project", Markup.Escape(report.Project.Name));
            project.AddRow("Root", Markup.Escape(report.RootDirectory));
            project.AddRow("Preset", Markup.Escape(report.Project.Preset));
            project.AddRow("Tool", Markup.Escape(report.ToolVersion));
            project.AddRow("Scaffold", $"{Markup.Escape(report.GeneratorVersion)} ({Markup.Escape(report.ScaffoldState)})");
            project.AddRow("Validation", report.Validation.IsValid ? "[green]valid[/]" : "[red]invalid[/]");
            project.AddRow("Doctor", report.Doctor.IsHealthy
                ? $"[green]healthy[/], {report.Doctor.WarningCount} warning(s)"
                : $"[red]unhealthy[/], {report.Doctor.ErrorCount} error(s)");
            AnsiConsole.Write(project);

            if (report.Recipes.Count > 0) {
                AnsiConsole.WriteLine();
                var recipes = new Table().Border(TableBorder.Rounded);
                recipes.AddColumn("Recipe");
                recipes.AddColumn("Installed");
                recipes.AddColumn("Available");
                recipes.AddColumn("State");
                foreach (var recipe in report.Recipes) {
                    recipes.AddRow(
                        Markup.Escape(RecipeCommandNames.ToCommandName(recipe.Id)),
                        Markup.Escape(recipe.InstalledVersion),
                        Markup.Escape(recipe.AvailableVersion ?? "-"),
                        Markup.Escape(recipe.State));
                }
                AnsiConsole.Write(recipes);
            }

            if (report.SuggestedCommands.Count > 0) {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[yellow]Suggested commands:[/]");
                foreach (string command in report.SuggestedCommands)
                    AnsiConsole.WriteLine("  " + command);
            }
            else {
                AnsiConsole.MarkupLine("[green]No maintenance action is currently required.[/]");
            }
            return report.IsHealthy ? SuccessExitCode : GenerationFailureExitCode;
        }
        catch (Exception exception) {
            ShowCommandError("status", CliErrors.ValidationFailed.GetMessage(
                XamlNexus.Common.Utils.LanguageRegistry.GetExceptionMessage(exception)), jsonOutput, CliErrors.ValidationFailed.Code);
            return GenerationFailureExitCode;
        }
    }
}
