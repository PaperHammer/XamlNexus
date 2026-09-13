using System.Reflection;
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
        private const int SuccessExitCode = 0;
        private const int GenerationFailureExitCode = 1;
        private const int UsageErrorExitCode = 2;

        static int Main(string[] args) {
            try {
                // Set encoding before Spectre captures the console writer/profile.
                Console.OutputEncoding = new System.Text.UTF8Encoding(false);
                string version = GetVersion();
                CliParseResult parseResult = CliParser.Parse(args, Environment.CurrentDirectory);
                if (!parseResult.Success) {
                    if (args.Any(argument => argument.Equals("--json", StringComparison.OrdinalIgnoreCase))) {
                        WriteJson(new {
                            operation = args.FirstOrDefault()?.ToLowerInvariant() ?? "cli",
                            status = "error",
                            error = new {
                                code = "XC1001",
                                message = parseResult.Error,
                            },
                        });
                        return UsageErrorExitCode;
                    }
                    AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(parseResult.Error!)}");
                    AnsiConsole.WriteLine();
                    ShowHelp();
                    return UsageErrorExitCode;
                }

                CliOptions options = parseResult.Options!;
                switch (options.Command) {
                    case CliCommand.Help:
                        ShowHelp();
                        return SuccessExitCode;
                    case CliCommand.Version:
                        AnsiConsole.WriteLine(version);
                        return SuccessExitCode;
                    case CliCommand.Interactive:
                        return RunInteractive(version);
                    case CliCommand.PageAdd:
                        return AddPage(options);
                    case CliCommand.New:
                        return Generate(options.Project!, options.Profile, options.Features);
                    case CliCommand.Run:
                        return RunProject(options);
                    case CliCommand.List:
                        return ListProject(options.ProjectPath!, options.JsonOutput);
                    case CliCommand.Validate:
                        return ValidateProject(options.ProjectPath!, options.JsonOutput);
                    case CliCommand.Recipes:
                        return ShowRecipes(options.JsonOutput);
                    case CliCommand.Add:
                        return AddRecipe(options.RecipeId!, options.ProjectPath!, options.DryRun, options.JsonOutput);
                    case CliCommand.Remove:
                        return RemoveRecipe(options.RecipeId!, options.ProjectPath!, options.DryRun, options.JsonOutput);
                    case CliCommand.Update:
                        return UpdateRecipe(options.RecipeId!, options.ProjectPath!, options.DryRun, options.JsonOutput);
                    case CliCommand.Doctor:
                        return Doctor(options.ProjectPath!, options.JsonOutput);
                    case CliCommand.Upgrade:
                        return UpgradeProject(
                            options.ProjectPath!,
                            options.DryRun,
                            options.JsonOutput,
                            options.ConflictOutputPath,
                            options.ResolveFromPath);
                    default:
                        throw new InvalidOperationException($"Unsupported command: {options.Command}");
                }
            }
            catch (Exception ex) {
                AnsiConsole.WriteException(ex);
                return GenerationFailureExitCode;
            }
        }

        private static string GetVersion() {
            var rawVersion = Assembly
                .GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            return rawVersion?.Split('+')[0] ?? "unknown";
        }

        private static void ShowHelp() => AnsiConsole.WriteLine(LanguageRegistry.GetText("Cli_Help"));
    }
}
