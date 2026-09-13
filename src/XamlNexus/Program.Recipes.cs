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

    }
}
