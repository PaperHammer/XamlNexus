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
        private static int AddPage(CliOptions options) {
            try {
                var changed = PageGenerator.Add(XamlNexusProjectLocator.Locate(options.ProjectPath!),
                    options.PageName!, options.DryRun, options.SkipNavigation);
                bool navigationAdded = changed.Any(path => path.EndsWith($".UI/Navigation/{options.PageName}Navigation.cs", StringComparison.Ordinal));
                string? notice = navigationAdded ? null
                    : "Navigation was not changed. Register the generated page in your navigation system manually.";
                if (options.JsonOutput) WriteJson(new { operation = "page-add", status = options.DryRun ? "preview" : "created", files = changed,
                    navigation = navigationAdded ? "automatic" : "manual", message = notice });
                else {
                    AnsiConsole.WriteLine(options.DryRun ? "Page generation preview:" : "Page created:");
                    foreach (string file in changed) AnsiConsole.WriteLine($"  {file}");
                    if (notice is not null) AnsiConsole.WriteLine(notice);
                }
                return SuccessExitCode;
            }
            catch (Exception exception) {
                if (options.JsonOutput) WriteJson(new { operation = "page-add", status = "error", error = exception.Message });
                else AnsiConsole.WriteLine($"Page generation failed: {exception.Message}");
                return GenerationFailureExitCode;
            }
        }

        private static int RunProject(CliOptions options) {
            using var cancellation = new CancellationTokenSource();
            ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
            try {
                var plan = DevelopmentRunner.CreatePlan(XamlNexusProjectLocator.Locate(options.ProjectPath!), options.NoBuild);
                if (options.DryRun) {
                    if (options.JsonOutput) WriteJson(new { operation = "run", status = "preview", plan });
                    else {
                        AnsiConsole.WriteLine($"Startup project: {plan.ProjectPath}");
                        AnsiConsole.WriteLine("Debug / x64 / unpackaged");
                        AnsiConsole.WriteLine(plan.NoBuild ? "Use existing build output." : "Build before starting.");
                        AnsiConsole.WriteLine(plan.Hybrid ? "Start the host; it opens the UI after initialization." : "Start the WinUI application.");
                    }
                    return SuccessExitCode;
                }
                Console.CancelKeyPress += cancel;
                Console.Error.WriteLine("Running Debug / x64 / unpackaged. Press Ctrl+C to stop this run.");
                int exitCode = DevelopmentRunner.RunAsync(plan, new DevelopmentRuntime(Console.Error.WriteLine), cancellation.Token).GetAwaiter().GetResult();
                if (options.JsonOutput) WriteJson(new { operation = "run", status = "exited", exitCode });
                else AnsiConsole.WriteLine($"Application exited with code {exitCode}.");
                return exitCode;
            }
            catch (OperationCanceledException) {
                if (options.JsonOutput) WriteJson(new { operation = "run", status = "cancelled", exitCode = 130 });
                else AnsiConsole.WriteLine("Run stopped.");
                return 130;
            }
            catch (Exception exception) {
                ShowCommandError("run", exception.Message, options.JsonOutput, "XC1201");
                return GenerationFailureExitCode;
            }
            finally { Console.CancelKeyPress -= cancel; }
        }

        private static int RunInteractive(string version) {
            if (Console.IsInputRedirected) {
                AnsiConsole.WriteLine("Interactive creation needs a terminal. Use: xamlnexus new MyApp --profile standard --features sqlite");
                return UsageErrorExitCode;
            }
            BaseConfig.ShowLogo(version);
            var config = BaseConfig.BaseComposeConfig();
            var request = InteractiveCreation.Compose(config, GeneratorFactory.GetGenerator(config.Framework), BuiltInRecipeCatalog.Create());
            return Generate(request.Project, request.Profile, request.Features);
        }

        private static int Generate(ProjectConfig config, string profile = "standard", IReadOnlyList<string>? features = null) {
            config.Profile = profile;
            LanguageRegistry.CurrentLanguage = config.Language == "en-US"
                ? LanguageType.English
                : LanguageType.Chinese;

            var generator = GeneratorFactory.GetGenerator(config.Framework);
            if (features is not { Count: > 0 })
                return generator.Generate(config) ? SuccessExitCode : GenerationFailureExitCode;
            try {
                string output = ProjectComposer.Create(new CompositionRequest(config, profile, features), generator, BuiltInRecipeCatalog.Create());
                CreationReport.Write(config, output, hasAddedCapabilities: true);
                return SuccessExitCode;
            }
            catch (Exception exception) {
                AnsiConsole.WriteLine($"Project creation failed: {exception.Message}");
                if (exception.Data["CleanupError"] is string cleanupError) AnsiConsole.WriteLine(cleanupError);
                return GenerationFailureExitCode;
            }
        }

    }
}
