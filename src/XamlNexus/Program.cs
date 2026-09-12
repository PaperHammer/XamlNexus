using System.Reflection;
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
    internal class Program {
        private const int SuccessExitCode = 0;
        private const int GenerationFailureExitCode = 1;
        private const int UsageErrorExitCode = 2;

        static int Main(string[] args) {
            try {
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
                            options.ConflictOutputPath);
                    default:
                        throw new InvalidOperationException($"Unsupported command: {options.Command}");
                }
            }
            catch (Exception ex) {
                AnsiConsole.WriteException(ex);
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

        private static int ListProject(string projectPath, bool jsonOutput) {
            try {
                XamlNexusProjectContext context = XamlNexusProjectLocator.Locate(projectPath);
                XamlNexusProjectManifest manifest = context.Manifest;
                if (jsonOutput) {
                    WriteJson(new {
                        operation = "list",
                        status = "success",
                        root = context.RootDirectory,
                        generatorVersion = manifest.GeneratorVersion,
                        project = manifest.Project,
                        modules = manifest.Modules,
                    });
                    return SuccessExitCode;
                }

                var projectTable = new Table().Border(TableBorder.Rounded);
                projectTable.AddColumn("Property");
                projectTable.AddColumn("Value");
                projectTable.AddRow("Root", Markup.Escape(context.RootDirectory));
                projectTable.AddRow("Project", Markup.Escape(manifest.Project.Name));
                projectTable.AddRow("Preset", Markup.Escape(manifest.Project.Preset));
                projectTable.AddRow("Starting profile", Markup.Escape(manifest.Project.Profile ?? "standard"));
                projectTable.AddRow("Language", Markup.Escape(manifest.Project.Language));
                projectTable.AddRow("Generator", Markup.Escape(manifest.GeneratorVersion));
                AnsiConsole.Write(projectTable);

                AnsiConsole.WriteLine();
                var moduleTable = new Table().Border(TableBorder.Rounded);
                moduleTable.AddColumn("Module");
                moduleTable.AddColumn("Version");
                moduleTable.AddColumn("Source");
                foreach (XamlNexusManagedModule module in manifest.Modules) {
                    moduleTable.AddRow(
                        Markup.Escape(module.Id),
                        Markup.Escape(module.Version),
                        Markup.Escape(module.Source));
                }
                AnsiConsole.Write(moduleTable);
                return SuccessExitCode;
            }
            catch (Exception exception) {
                ShowCommandError("list", exception.Message, jsonOutput, "XL1001");
                return GenerationFailureExitCode;
            }
        }

        private static int ValidateProject(string projectPath, bool jsonOutput) {
            try {
                XamlNexusProjectContext context = XamlNexusProjectLocator.Locate(projectPath);
                XamlNexusProjectValidationReport report = XamlNexusProjectValidator.Validate(context);
                if (jsonOutput) {
                    WriteJson(new {
                        operation = "validate",
                        status = report.IsValid ? "valid" : "invalid",
                        root = context.RootDirectory,
                        isValid = report.IsValid,
                        issues = report.Issues,
                    });
                    return report.IsValid ? SuccessExitCode : GenerationFailureExitCode;
                }

                AnsiConsole.MarkupLine($"Project: [cyan]{Markup.Escape(context.RootDirectory)}[/]");
                if (report.Issues.Count == 0) {
                    AnsiConsole.MarkupLine("[green]Valid:[/] manifest and managed project structure are consistent.");
                    return SuccessExitCode;
                }

                foreach (ProjectValidationIssue issue in report.Issues) {
                    string color = issue.Severity == ProjectValidationSeverity.Error ? "red" : "yellow";
                    string path = issue.RelativePath is null
                        ? string.Empty
                        : $" ({Markup.Escape(issue.RelativePath)})";
                    AnsiConsole.MarkupLine(
                        $"[{color}]{issue.Severity} {Markup.Escape(issue.Code)}:[/] " +
                        $"{Markup.Escape(issue.Message)}{path}");
                }

                if (report.IsValid) {
                    AnsiConsole.MarkupLine("[green]Valid with warnings.[/]");
                    return SuccessExitCode;
                }

                AnsiConsole.MarkupLine("[red]Invalid:[/] one or more managed files are missing.");
                return GenerationFailureExitCode;
            }
            catch (Exception exception) {
                ShowCommandError("validate", exception.Message, jsonOutput, "XV1001");
                return GenerationFailureExitCode;
            }
        }

        private static int ShowRecipes(bool jsonOutput) {
            try {
                IXamlNexusRecipeCatalog catalog = BuiltInRecipeCatalog.Create();
                if (jsonOutput) {
                    WriteJson(new {
                        operation = "recipes",
                        status = "success",
                        recipes = catalog.Recipes.Select(recipe => recipe.Descriptor),
                    });
                    return SuccessExitCode;
                }
                var table = new Table().Border(TableBorder.Rounded);
                table.AddColumn("Recipe");
                table.AddColumn("Version");
                table.AddColumn("Presets");
                table.AddColumn("Description");
                foreach (IXamlNexusRecipe recipe in catalog.Recipes) {
                    XamlNexusRecipeDescriptor descriptor = recipe.Descriptor;
                    table.AddRow(
                        Markup.Escape(descriptor.Id),
                        Markup.Escape(descriptor.Version),
                        Markup.Escape(string.Join(", ", descriptor.SupportedPresets)),
                        Markup.Escape(descriptor.Description));
                }
                AnsiConsole.Write(table);
                return SuccessExitCode;
            }
            catch (Exception exception) {
                ShowCommandError("recipes", exception.Message, jsonOutput, "XC1103");
                return GenerationFailureExitCode;
            }
        }

        private static int AddRecipe(string recipeId, string projectPath, bool dryRun, bool jsonOutput) {
            try {
                IXamlNexusRecipeCatalog catalog = BuiltInRecipeCatalog.Create();
                string[] requested = recipeId.Split(',', StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                foreach (string id in requested) {
                    if (catalog.Find(id) is null) {
                        ShowCommandError("add", $"Unknown Recipe '{id}'. Available Recipes: {string.Join(", ", catalog.Recipes.Select(item => item.Descriptor.Id))}.", jsonOutput);
                        return UsageErrorExitCode;
                    }
                }
                var context = XamlNexusProjectLocator.Locate(projectPath);
                if (!XamlNexusProjectValidator.Validate(context).IsValid) {
                    ShowCommandError("add", "The project is not structurally valid. Run 'xamlnexus validate' and fix errors before adding a Recipe.", jsonOutput);
                    return GenerationFailureExitCode;
                }
                var recipes = CompositionPlanner.Resolve(context.Manifest.Project.Preset, context.Manifest.Project.Profile ?? "standard",
                    requested, catalog, context.Manifest.Modules);
                if (recipes.Count > 1) {
                    var plan = XamlNexusRecipeTransaction.PrepareApplyBatch(context, recipes);
                    IReadOnlyList<string> files = dryRun ? plan.ChangedFiles : XamlNexusRecipeTransaction.ApplyBatch(context, plan);
                    if (jsonOutput) WriteJson(new {
                        operation = "add", status = dryRun ? "preview" : "applied",
                        recipes = plan.Recipes, changedFiles = files,
                    });
                    else {
                        AnsiConsole.WriteLine(dryRun ? "Batch installation preview:" : "Installed Recipes:");
                        foreach (var recipe in plan.Recipes) AnsiConsole.WriteLine($"  {recipe.RecipeId} {recipe.ToVersion}");
                        foreach (string file in files) AnsiConsole.WriteLine($"  {file}");
                    }
                    return SuccessExitCode;
                }
                var single = recipes[0];
                if (dryRun) {
                    ShowRecipePreview(XamlNexusRecipeTransaction.PreviewApply(context, single), jsonOutput);
                    return SuccessExitCode;
                }
                var result = XamlNexusRecipeTransaction.Apply(context, single);
                if (jsonOutput) WriteJson(new {
                    operation = "add", status = "applied", recipeId = result.RecipeId,
                    version = result.RecipeVersion, changedFiles = result.ChangedFiles,
                });
                else {
                    AnsiConsole.MarkupLine($"[green]Installed Recipe:[/] {Markup.Escape(result.RecipeId)} {Markup.Escape(result.RecipeVersion)}");
                    foreach (string file in result.ChangedFiles) AnsiConsole.WriteLine($"  {file}");
                }
                return SuccessExitCode;
            }
            catch (Exception exception) {
                ShowCommandError("add", exception.Message, jsonOutput, GetErrorCode(exception));
                return GenerationFailureExitCode;
            }
        }

        private static int RemoveRecipe(string recipeId, string projectPath, bool dryRun, bool jsonOutput) {
            try {
                IXamlNexusRecipeCatalog catalog = BuiltInRecipeCatalog.Create();
                IXamlNexusRecipe? recipe = catalog.Find(recipeId);
                if (recipe is null) {
                    string available = string.Join(", ", catalog.Recipes.Select(item => item.Descriptor.Id));
                    ShowCommandError("remove", $"Unknown Recipe '{recipeId}'. Available Recipes: {available}.", jsonOutput);
                    return UsageErrorExitCode;
                }

                XamlNexusProjectContext context = XamlNexusProjectLocator.Locate(projectPath);
                var installed = context.Manifest.Modules
                    .Select(module => module.Id)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                string[] dependents = catalog.Recipes
                    .Where(candidate =>
                        installed.Contains(candidate.Descriptor.Id) &&
                        candidate.Descriptor.Dependencies.Contains(
                            recipe.Descriptor.Id,
                            StringComparer.OrdinalIgnoreCase))
                    .Select(candidate => candidate.Descriptor.Id)
                    .ToArray();
                if (dependents.Length > 0) {
                    ShowCommandError("remove", $"Recipe '{recipe.Descriptor.Id}' is required by: {string.Join(", ", dependents)}.", jsonOutput);
                    return GenerationFailureExitCode;
                }

                if (dryRun) {
                    ShowRecipePreview(XamlNexusRecipeTransaction.PreviewRemove(context, recipe), jsonOutput);
                    return SuccessExitCode;
                }

                XamlNexusRecipeApplyResult result = XamlNexusRecipeTransaction.Remove(context, recipe);
                if (jsonOutput) {
                    WriteJson(new {
                        operation = "remove",
                        status = "applied",
                        recipeId = result.RecipeId,
                        version = result.RecipeVersion,
                        changedFiles = result.ChangedFiles,
                    });
                    return SuccessExitCode;
                }
                AnsiConsole.MarkupLine(
                    $"[green]Removed Recipe:[/] {Markup.Escape(result.RecipeId)} {Markup.Escape(result.RecipeVersion)}");
                foreach (string file in result.ChangedFiles)
                    AnsiConsole.MarkupLine($"  [grey]-[/] {Markup.Escape(file)}");
                return SuccessExitCode;
            }
            catch (Exception exception) {
                ShowCommandError("remove", exception.Message, jsonOutput, GetErrorCode(exception));
                return GenerationFailureExitCode;
            }
        }

        private static int UpdateRecipe(string recipeId, string projectPath, bool dryRun, bool jsonOutput) {
            try {
                IXamlNexusRecipeCatalog catalog = BuiltInRecipeCatalog.Create();
                IXamlNexusRecipe? recipe = catalog.Find(recipeId);
                if (recipe is null) {
                    string available = string.Join(", ", catalog.Recipes.Select(item => item.Descriptor.Id));
                    ShowCommandError("update", $"Unknown Recipe '{recipeId}'. Available Recipes: {available}.", jsonOutput);
                    return UsageErrorExitCode;
                }

                XamlNexusProjectContext context = XamlNexusProjectLocator.Locate(projectPath);
                XamlNexusManagedModule? installed = context.Manifest.Modules.SingleOrDefault(module =>
                    module.Id.Equals(recipe.Descriptor.Id, StringComparison.OrdinalIgnoreCase));
                if (installed is not null &&
                    installed.Source.Equals("recipe", StringComparison.OrdinalIgnoreCase) &&
                    installed.Version.Equals(recipe.Descriptor.Version, StringComparison.OrdinalIgnoreCase)) {
                    if (jsonOutput) {
                        WriteJson(new {
                            operation = "update",
                            status = "upToDate",
                            recipeId = recipe.Descriptor.Id,
                            version = recipe.Descriptor.Version,
                            dryRun,
                        });
                        return SuccessExitCode;
                    }
                    AnsiConsole.MarkupLine(
                        $"[green]Recipe is up to date:[/] {Markup.Escape(recipe.Descriptor.Id)} " +
                        Markup.Escape(recipe.Descriptor.Version));
                    return SuccessExitCode;
                }

                if (dryRun) {
                    ShowRecipePreview(XamlNexusRecipeTransaction.PreviewUpdate(context, recipe), jsonOutput);
                    return SuccessExitCode;
                }

                XamlNexusRecipeApplyResult result = XamlNexusRecipeTransaction.Update(context, recipe);
                if (jsonOutput) {
                    WriteJson(new {
                        operation = "update",
                        status = "applied",
                        recipeId = result.RecipeId,
                        version = result.RecipeVersion,
                        changedFiles = result.ChangedFiles,
                    });
                    return SuccessExitCode;
                }
                AnsiConsole.MarkupLine(
                    $"[green]Updated Recipe:[/] {Markup.Escape(result.RecipeId)} {Markup.Escape(result.RecipeVersion)}");
                foreach (string file in result.ChangedFiles)
                    AnsiConsole.MarkupLine($"  [grey]~[/] {Markup.Escape(file)}");
                return SuccessExitCode;
            }
            catch (Exception exception) {
                ShowCommandError("update", exception.Message, jsonOutput, GetErrorCode(exception));
                return GenerationFailureExitCode;
            }
        }

        private static int Doctor(string projectPath, bool jsonOutput) {
            try {
                XamlNexusProjectContext context = XamlNexusProjectLocator.Locate(projectPath);
                XamlNexusDoctorReport report = XamlNexusDoctor.Diagnose(
                    context,
                    BuiltInRecipeCatalog.Create());
                if (jsonOutput) {
                    var jsonOptions = new JsonSerializerOptions {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    };
                    jsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
                    Console.WriteLine(JsonSerializer.Serialize(report, jsonOptions));
                }
                else {
                    AnsiConsole.MarkupLine($"Project: [cyan]{Markup.Escape(report.RootDirectory)}[/]");
                    var table = new Table().Border(TableBorder.Rounded);
                    table.AddColumn("Status");
                    table.AddColumn("Code");
                    table.AddColumn("Category");
                    table.AddColumn("Diagnostic");
                    foreach (XamlNexusDoctorCheck check in report.Checks) {
                        (string label, string color) = check.Severity switch {
                            XamlNexusDoctorSeverity.Pass => ("PASS", "green"),
                            XamlNexusDoctorSeverity.Warning => ("WARN", "yellow"),
                            _ => ("ERROR", "red"),
                        };
                        string path = check.RelativePath is null
                            ? string.Empty
                            : $" ({check.RelativePath})";
                        table.AddRow(
                            $"[{color}]{label}[/]",
                            Markup.Escape(check.Code),
                            Markup.Escape(check.Category),
                            Markup.Escape(check.Message + path));
                    }
                    AnsiConsole.Write(table);
                    AnsiConsole.MarkupLine(
                        report.IsHealthy
                            ? $"[green]Healthy:[/] {report.WarningCount} warning(s), 0 errors."
                            : $"[red]Unhealthy:[/] {report.WarningCount} warning(s), {report.ErrorCount} error(s).");
                }
                return report.IsHealthy ? SuccessExitCode : GenerationFailureExitCode;
            }
            catch (Exception exception) {
                if (jsonOutput) {
                    Console.WriteLine(JsonSerializer.Serialize(new { error = exception.Message }));
                }
                else {
                    ShowOperationalError(exception.Message);
                }
                return GenerationFailureExitCode;
            }
        }

        private static int UpgradeProject(
            string projectPath,
            bool dryRun,
            bool jsonOutput,
            string? conflictOutputPath) {
            string? temporaryParent = null;
            try {
                XamlNexusProjectContext current = XamlNexusProjectLocator.Locate(projectPath);
                string targetVersion = GetVersion();
                int versionComparison = CompareToolVersions(
                    targetVersion,
                    current.Manifest.GeneratorVersion);
                if (versionComparison < 0) {
                    ShowCommandError(
                        "upgrade",
                        $"Project downgrade from {current.Manifest.GeneratorVersion} to {targetVersion} is not supported.",
                        jsonOutput,
                        "XU1008");
                    return GenerationFailureExitCode;
                }
                if (versionComparison == 0 && current.Manifest.ScaffoldFiles is not null && !dryRun) {
                    if (jsonOutput) {
                        WriteJson(new {
                            operation = "upgrade",
                            status = "upToDate",
                            fromVersion = current.Manifest.GeneratorVersion,
                            toVersion = targetVersion,
                            dryRun,
                        });
                        return SuccessExitCode;
                    }
                    AnsiConsole.MarkupLine(
                        $"[green]Project is up to date:[/] {Markup.Escape(targetVersion)}");
                    return SuccessExitCode;
                }
                if (versionComparison > 0 && current.Manifest.ScaffoldFiles is null) {
                    ShowCommandError(
                        "upgrade",
                        "This older project has no scaffold baseline. Upgrade it first with the matching XamlNexus version.",
                        jsonOutput,
                        "XU1001");
                    return GenerationFailureExitCode;
                }

                temporaryParent = Path.Combine(
                    Path.GetTempPath(),
                    "xamlnexus-upgrade",
                    Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(temporaryParent);
                FrameworkType framework = current.Manifest.Project.Preset == "hybrid"
                    ? FrameworkType.Winui3_Wpf
                    : FrameworkType.Winui3;
                var config = new ProjectConfig {
                    Profile = current.Manifest.Project.Profile ?? "standard",
                    SlnName = current.Manifest.Project.Name,
                    OutputPath = temporaryParent,
                    Language = current.Manifest.Project.Language,
                    Framework = framework,
                    SlnType = current.Manifest.Project.SolutionFormat == "slnx" ? SolutionType.Slnx : SolutionType.Sln,
                };
                IGenerator generator = GeneratorFactory.GetGenerator(framework);
                if (!GenerateUpgradeTarget(generator, config)) {
                    ShowCommandError(
                        "upgrade",
                        "Failed to generate the target scaffold for upgrade planning.",
                        jsonOutput);
                    return GenerationFailureExitCode;
                }

                XamlNexusProjectContext target = XamlNexusProjectLocator.Locate(
                    Path.Combine(temporaryParent, current.Manifest.Project.Name));
                bool baselineAdoption = current.Manifest.ScaffoldFiles is null;
                XamlNexusProjectUpgradePlan plan = baselineAdoption
                    ? XamlNexusProjectUpgrade.CreateBaselineAdoptionPlan(current, target)
                    : XamlNexusProjectUpgrade.CreatePlan(current, target);
                bool manifestWillChange = baselineAdoption || versionComparison != 0;
                if (!plan.CanApply) {
                    IReadOnlyList<string> conflictArtifacts = conflictOutputPath is null
                        ? []
                        : XamlNexusProjectUpgrade.WriteConflictArtifacts(plan, conflictOutputPath);
                    ShowUpgradePlan(
                        plan,
                        dryRun,
                        jsonOutput,
                        manifestWillChange,
                        conflictOutputPath,
                        conflictArtifacts);
                    return GenerationFailureExitCode;
                }

                if (dryRun) {
                    ShowUpgradePlan(plan, dryRun: true, jsonOutput, manifestWillChange, null, []);
                    return SuccessExitCode;
                }

                XamlNexusProjectUpgradeResult result = XamlNexusProjectUpgrade.Apply(
                    current,
                    target,
                    plan);
                if (jsonOutput) {
                    WriteJson(new {
                        operation = "upgrade",
                        status = versionComparison == 0 ? "baselineAdopted" : "applied",
                        fromVersion = result.FromVersion,
                        toVersion = result.ToVersion,
                        changedFiles = result.ChangedFiles,
                    });
                    return SuccessExitCode;
                }
                if (versionComparison == 0) {
                    AnsiConsole.MarkupLine(
                        $"[green]Adopted scaffold baseline:[/] {Markup.Escape(result.ToVersion)}");
                }
                else {
                    AnsiConsole.MarkupLine(
                        $"[green]Upgraded project:[/] {Markup.Escape(result.FromVersion)} -> " +
                        Markup.Escape(result.ToVersion));
                }
                foreach (string file in result.ChangedFiles)
                    AnsiConsole.MarkupLine($"  [grey]~[/] {Markup.Escape(file)}");
                return SuccessExitCode;
            }
            catch (Exception exception) {
                ShowCommandError("upgrade", exception.Message, jsonOutput, GetErrorCode(exception));
                return GenerationFailureExitCode;
            }
            finally {
                if (temporaryParent is not null && Directory.Exists(temporaryParent)) {
                    try {
                        Directory.Delete(temporaryParent, recursive: true);
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
                        // Temporary scaffold cleanup must not hide the upgrade result.
                    }
                }
            }
        }

        private static bool GenerateUpgradeTarget(IGenerator generator, ProjectConfig config) {
            TextWriter originalOutput = Console.Out;
            using var suppressedOutput = new StringWriter();
            try {
                Console.SetOut(suppressedOutput);
                return generator.Generate(config);
            }
            finally {
                Console.SetOut(originalOutput);
            }
        }

        private static void ShowUpgradePlan(
            XamlNexusProjectUpgradePlan plan,
            bool dryRun,
            bool jsonOutput,
            bool manifestWillChange,
            string? conflictOutputPath,
            IReadOnlyList<string> conflictArtifacts) {
            if (jsonOutput) {
                WriteJson(new {
                    operation = "upgrade",
                    status = plan.CanApply ? "planned" : "conflict",
                    dryRun,
                    fromVersion = plan.FromVersion,
                    toVersion = plan.ToVersion,
                    canApply = plan.CanApply,
                    manifestWillChange = plan.CanApply && manifestWillChange,
                    changes = plan.Changes.Select(change => new {
                        kind = change.Kind,
                        path = change.RelativePath,
                        strategy = change.Strategy,
                    }),
                    conflicts = plan.Conflicts,
                    conflictOutputPath,
                    conflictArtifacts,
                });
                return;
            }

            foreach (XamlNexusUpgradeConflict conflict in plan.Conflicts) {
                AnsiConsole.MarkupLine(
                    $"[red]{Markup.Escape(conflict.Code)}:[/] " +
                    $"{Markup.Escape(conflict.Message)} ({Markup.Escape(conflict.RelativePath)})");
            }
            if (!plan.CanApply) {
                ShowOperationalError(
                    $"Upgrade stopped with {plan.Conflicts.Count} conflict(s); no project files were changed.");
                if (conflictOutputPath is not null) {
                    AnsiConsole.MarkupLine(
                        $"[yellow]Conflict artifacts:[/] {Markup.Escape(conflictOutputPath)}");
                    foreach (string artifact in conflictArtifacts)
                        AnsiConsole.MarkupLine($"  [grey]+[/] {Markup.Escape(artifact)}");
                    if (conflictArtifacts.Count == 0)
                        AnsiConsole.MarkupLine("  [grey]No merge document was available for these conflicts.[/]");
                }
                return;
            }

            AnsiConsole.MarkupLine(
                $"[green]Upgrade plan:[/] {Markup.Escape(plan.FromVersion)} -> {Markup.Escape(plan.ToVersion)}");
            foreach (XamlNexusUpgradeChange change in plan.Changes) {
                string symbol = change.Kind switch {
                    XamlNexusUpgradeChangeKind.Create => "+",
                    XamlNexusUpgradeChangeKind.Delete => "-",
                    _ => "~",
                };
                string strategy = change.Strategy switch {
                    XamlNexusUpgradeChangeStrategy.TextMerge => " [cyan](text merge)[/]",
                    XamlNexusUpgradeChangeStrategy.XmlMerge => " [cyan](XML semantic merge)[/]",
                    XamlNexusUpgradeChangeStrategy.SolutionMerge => " [cyan](solution semantic merge)[/]",
                    _ => string.Empty,
                };
                AnsiConsole.MarkupLine(
                    $"  [grey]{symbol}[/] {Markup.Escape(change.RelativePath)}{strategy}");
            }
            if (plan.Changes.Count == 0) {
                string message = manifestWillChange
                    ? "No scaffold file changes; only the manifest baseline will be updated."
                    : "No changes are required.";
                AnsiConsole.MarkupLine($"  [grey]{message}[/]");
            }
            AnsiConsole.MarkupLine("[yellow]Dry run:[/] no project files were changed.");
        }

        private static int CompareToolVersions(string left, string right) {
            Version leftVersion = Version.Parse(left.Split('-', 2)[0]);
            Version rightVersion = Version.Parse(right.Split('-', 2)[0]);
            int core = leftVersion.CompareTo(rightVersion);
            if (core != 0) return core;
            bool leftPreview = left.Contains('-', StringComparison.Ordinal);
            bool rightPreview = right.Contains('-', StringComparison.Ordinal);
            if (leftPreview == rightPreview)
                return string.CompareOrdinal(left, right);
            return leftPreview ? -1 : 1;
        }

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

        private static string GetVersion() {
            var rawVersion = Assembly
                .GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            return rawVersion?.Split('+')[0] ?? "unknown";
        }

        private static void ShowHelp() {
            AnsiConsole.WriteLine("XamlNexus - Windows desktop prototype scaffolding");
            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine("Quick start:");
            AnsiConsole.WriteLine("  xamlnexus new MyApp");
            AnsiConsole.WriteLine("  cd MyApp");
            AnsiConsole.WriteLine("  xamlnexus run");
            AnsiConsole.WriteLine("  Stop with Ctrl+C before editing or running again.");
            AnsiConsole.WriteLine("  Edit MyApp.MainPanel/MainPage.xaml and ViewModels/MainViewModel.cs.");
            AnsiConsole.WriteLine("  xamlnexus page add Workspace");
            AnsiConsole.WriteLine("  xamlnexus run");
            AnsiConsole.WriteLine("  After stopping: xamlnexus add sqlite, then xamlnexus run.");
            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine("Usage:");
            AnsiConsole.WriteLine("  xamlnexus                         Start interactive mode");
            AnsiConsole.WriteLine("  xamlnexus new <name> [options]    Create a project");
            AnsiConsole.WriteLine("  xamlnexus page add <name> [--project <directory>] [--no-navigation] [--dry-run] [--json]");
            AnsiConsole.WriteLine("  xamlnexus run [path] [--project <directory>] [--no-build] [--dry-run] [--json]");
            AnsiConsole.WriteLine("    Build and run Debug/x64 unpackaged; Ctrl+C stops this run.");
            AnsiConsole.WriteLine("    --no-build uses existing output; no watch or hot reload.");
            AnsiConsole.WriteLine("  xamlnexus list [path] [--json]    List project capabilities");
            AnsiConsole.WriteLine("  xamlnexus validate [path] [--json] Validate the manifest and managed files");
            AnsiConsole.WriteLine("  xamlnexus doctor [path] [--json] Diagnose environment and project health");
            AnsiConsole.WriteLine("  xamlnexus upgrade [path] [options] Upgrade scaffold-managed infrastructure");
            AnsiConsole.WriteLine("  xamlnexus recipes [--json]        List built-in Recipes");
            AnsiConsole.WriteLine("  xamlnexus add <recipe[,recipe...]> [options]  Install Recipes transactionally");
            AnsiConsole.WriteLine("  xamlnexus remove <recipe> [options] Remove a Recipe transactionally");
            AnsiConsole.WriteLine("  xamlnexus update <recipe> [options] Update a Recipe transactionally");
            AnsiConsole.WriteLine("  xamlnexus --help                  Show help");
            AnsiConsole.WriteLine("  xamlnexus --version               Show version");
            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine("New options:");
            AnsiConsole.WriteLine("  -n, --name <name>                 Project name (or use the positional name)");
            AnsiConsole.WriteLine("  -p, --preset <preset>             winui (default) or hybrid");
            AnsiConsole.WriteLine("  -o, --output <directory>          Parent output directory (default: current directory)");
            AnsiConsole.WriteLine("      --profile <standard|basic>    Starting capability profile (default: standard)");
            AnsiConsole.WriteLine("                                    basic omits the full settings panel; core services remain.");
            AnsiConsole.WriteLine("      --features <ids>               Install comma-separated Recipes during creation");
            AnsiConsole.WriteLine("  -l, --language <language>         zh-CN (default) or en-US");
            AnsiConsole.WriteLine("  -f, --solution-format <format>    sln (default) or slnx");
            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine("Project command options:");
            AnsiConsole.WriteLine("  -p, --project <path>              Project directory or xamlnexus.json path");
            AnsiConsole.WriteLine("  --dry-run                         Validate and show changes without writing files");
            AnsiConsole.WriteLine("  --json                            Emit machine-readable query or change output");
            AnsiConsole.WriteLine("  --conflict-output <directory>     Export upgrade merge conflicts (writes files)");
            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine("Examples:");
            AnsiConsole.WriteLine("  xamlnexus new MyApp");
            AnsiConsole.WriteLine("  xamlnexus new MinimalApp --profile basic");
            AnsiConsole.WriteLine("  xamlnexus new DataApp --profile basic --features settings,sqlite");
            AnsiConsole.WriteLine("  xamlnexus run --project MyApp");
            AnsiConsole.WriteLine("  xamlnexus page add Workspace --project MyApp --dry-run");
            AnsiConsole.WriteLine("  xamlnexus new MyApp --preset hybrid --output D:\\Projects --language en-US");
            AnsiConsole.WriteLine("  xamlnexus list D:\\Projects\\MyApp");
            AnsiConsole.WriteLine("  xamlnexus validate --project D:\\Projects\\MyApp --json");
            AnsiConsole.WriteLine("  xamlnexus doctor --project D:\\Projects\\MyApp --json");
            AnsiConsole.WriteLine("  xamlnexus upgrade --project D:\\Projects\\MyApp --dry-run --json");
            AnsiConsole.WriteLine("  xamlnexus upgrade --project D:\\Projects\\MyApp --conflict-output .\\upgrade-conflicts");
            AnsiConsole.WriteLine("  xamlnexus recipes");
            AnsiConsole.WriteLine("  xamlnexus add editorconfig --project D:\\Projects\\MyApp --dry-run");
            AnsiConsole.WriteLine("  xamlnexus remove editorconfig --project D:\\Projects\\MyApp");
            AnsiConsole.WriteLine("  xamlnexus update sqlite --project D:\\Projects\\MyApp");
            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine("Recipes: settings, sqlite, system-tray, app-update, editorconfig.");
            AnsiConsole.WriteLine("  standard includes settings; hybrid includes tray and updater.");
            AnsiConsole.WriteLine("  app-update depends on settings. Do not add already installed Recipes.");
            AnsiConsole.WriteLine("Guide: https://github.com/PaperHammer/XamlNexus/blob/main/docs/user-guide/quickstart.zh-CN.md");
        }
    }
}
