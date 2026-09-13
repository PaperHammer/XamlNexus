using System.Text.Json;
using System.Text.Json.Serialization;
using Spectre.Console;
using XamlNexus.Common.CommandLine;
using XamlNexus.Common.Generators;
using XamlNexus.Common.Projects;
using XamlNexus.Common.Recipes;
using XamlNexus.Common.Utils;
using XamlNexus.Recipes.BuiltIn;
using XamlNexus.Utils;

namespace XamlNexus {
    internal partial class Program {
        private static void ShowRecipePreview(XamlNexusRecipePreview preview, bool jsonOutput) {
            if (jsonOutput) {
                WriteJson(new {
                    operation = preview.Operation,
                    status = "planned",
                    dryRun = true,
                    recipeId = preview.RecipeId,
                    fromVersion = preview.FromVersion,
                    toVersion = preview.ToVersion,
                    manifestWillChange = true,
                    changes = preview.Changes,
                });
                return;
            }

            string versions = preview.FromVersion is null
                ? preview.ToVersion
                : $"{preview.FromVersion} -> {preview.ToVersion}";
            AnsiConsole.MarkupLine(
                $"[green]Recipe plan:[/] {Markup.Escape(preview.Operation)} " +
                $"{Markup.Escape(preview.RecipeId)} {Markup.Escape(versions)}");
            foreach (XamlNexusRecipePreviewChange change in preview.Changes) {
                string symbol = change.Kind switch {
                    XamlNexusRecipeFileChangeKind.Create => "+",
                    XamlNexusRecipeFileChangeKind.Delete => "-",
                    _ => "~",
                };
                AnsiConsole.MarkupLine(
                    $"  [grey]{symbol}[/] {Markup.Escape(change.RelativePath)} " +
                    $"[grey]({Markup.Escape(change.Scope)})[/]");
            }
            AnsiConsole.MarkupLine("[yellow]Dry run:[/] no project files were changed.");
        }

        private static void ShowCommandError(
            string operation,
            string message,
            bool jsonOutput,
            string? code = null) {
            if (jsonOutput) {
                WriteJson(new {
                    operation,
                    status = "error",
                    error = new { code, message },
                });
            }
            else {
                ShowOperationalError(message);
            }
        }

        private static string? GetErrorCode(Exception exception) => exception switch {
            XamlNexusRecipeException recipeException => recipeException.Code,
            XamlNexusProjectUpgradeException upgradeException => upgradeException.Code,
            _ => null,
        };

        private static void WriteJson(object value) {
            var options = new JsonSerializerOptions {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };
            options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            Console.WriteLine(JsonSerializer.Serialize(value, options));
        }

        private static void ShowOperationalError(string message) {
            AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(message)}");
        }

    }
}
