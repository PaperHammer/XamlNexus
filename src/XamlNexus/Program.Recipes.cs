using XamlNexus.Common.CommandLine;
using Spectre.Console;
using XamlNexus.Common.Projects;
using XamlNexus.Common.Recipes;
using XamlNexus.Recipes.BuiltIn;

namespace XamlNexus {
    internal partial class Program {
        /// <summary>
        /// 以 JSON 或表格列出内置 Recipe 的版本、支持的预设和描述
        /// </summary>
        private static int ShowRecipes(bool jsonOutput) {
            try {
                IXamlNexusRecipeCatalog catalog = BuiltInRecipeCatalog.Create();
                if (jsonOutput) {
                    WriteJson(new {
                        operation = "recipes",
                        status = "success",
                        recipes = catalog.Recipes.Select(recipe => new {
                            id = RecipeCommandNames.ToCommandName(recipe.Descriptor.Id),
                            recipe.Descriptor.Version, recipe.Descriptor.DisplayName, recipe.Descriptor.Description,
                            recipe.Descriptor.SupportedPresets,
                            dependencies = recipe.Descriptor.Dependencies.Select(RecipeCommandNames.ToCommandName),
                            conflicts = recipe.Descriptor.Conflicts.Select(RecipeCommandNames.ToCommandName),
                        }),
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
                        Markup.Escape(RecipeCommandNames.ToCommandName(descriptor.Id)),
                        Markup.Escape(descriptor.Version),
                        Markup.Escape(string.Join(", ", descriptor.SupportedPresets)),
                        Markup.Escape(descriptor.Description));
                }
                AnsiConsole.Write(table);
                return SuccessExitCode;
            }
            catch (Exception exception) {
                ShowCommandError("recipes", XamlNexus.Common.Utils.LanguageRegistry.GetExceptionMessage(exception), jsonOutput, "XC1103");
                return GenerationFailureExitCode;
            }
        }

        /// <summary>
        /// 解析逗号分隔的 Recipe 标识及其依赖，校验项目后预览或执行单项、批量安装
        /// </summary>
        private static int AddRecipe(string recipeId, string projectPath, bool dryRun, bool jsonOutput) {
            try {
                IXamlNexusRecipeCatalog catalog = BuiltInRecipeCatalog.Create();
                string[] requested = recipeId.Split(',', StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                foreach (string id in requested) {
                    if (catalog.Find(id) is null) {
                        ShowCommandError("add", $"Unknown Recipe '{id}'. Available Recipes: {string.Join(", ", catalog.Recipes.Select(item => RecipeCommandNames.ToCommandName(item.Descriptor.Id)))}.", jsonOutput);
                        return UsageErrorExitCode;
                    }
                }
                var context = XamlNexusProjectLocator.Locate(projectPath);
                if (!XamlNexusProjectValidator.Validate(context).IsValid) {
                    ShowCommandError("add", "The project is not structurally valid. Run 'xamlnexus validate' and fix errors before adding a Recipe.", jsonOutput);
                    return GenerationFailureExitCode;
                }
                var installed = context.Manifest.Modules.Select(module => module.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
                string[] skipped = requested.Where(installed.Contains).ToArray();
                var recipes = CompositionPlanner.Resolve(context.Manifest.Project.Preset, context.Manifest.Project.Profile ?? "standard",
                    requested.Where(id => !installed.Contains(id)), catalog, context.Manifest.Modules);
                if (!jsonOutput)
                    foreach (string id in skipped) AnsiConsole.WriteLine($"Skipped already installed Recipe: {RecipeCommandNames.ToCommandName(id)}");
                if (recipes.Count == 0) {
                    if (jsonOutput) WriteJson(new {
                        operation = "add",
                        status = "upToDate",
                        dryRun,
                        skippedRecipes = skipped.Select(RecipeCommandNames.ToCommandName),
                        changedFiles = Array.Empty<string>(),
                    });
                    else AnsiConsole.WriteLine("All requested Recipes are already installed. No changes were made.");
                    return SuccessExitCode;
                }
                if (recipes.Count > 1) {
                    var plan = XamlNexusRecipeTransaction.PrepareApplyBatch(context, recipes);
                    IReadOnlyList<string> files = dryRun ? plan.ChangedFiles : XamlNexusRecipeTransaction.ApplyBatch(context, plan);
                    if (jsonOutput) WriteJson(new {
                        operation = "add",
                        status = dryRun ? "preview" : "applied",
                        recipes = plan.Recipes.Select(item => item with { RecipeId = RecipeCommandNames.ToCommandName(item.RecipeId) }),
                        skippedRecipes = skipped.Select(RecipeCommandNames.ToCommandName),
                        changedFiles = files,
                    });
                    else {
                        AnsiConsole.WriteLine(dryRun ? "Batch installation preview:" : "Installed Recipes:");
                        foreach (var recipe in plan.Recipes) AnsiConsole.WriteLine($"  {RecipeCommandNames.ToCommandName(recipe.RecipeId)} {recipe.ToVersion}");
                        foreach (string file in files) AnsiConsole.WriteLine($"  {file}");
                    }
                    return SuccessExitCode;
                }
                var single = recipes[0];
                if (dryRun) {
                    ShowRecipePreview(XamlNexusRecipeTransaction.PreviewApply(context, single), jsonOutput, skipped);
                    return SuccessExitCode;
                }
                var result = XamlNexusRecipeTransaction.Apply(context, single);
                if (jsonOutput) WriteJson(new {
                    operation = "add",
                    status = "applied",
                    recipeId = RecipeCommandNames.ToCommandName(result.RecipeId),
                    version = result.RecipeVersion,
                    skippedRecipes = skipped.Select(RecipeCommandNames.ToCommandName),
                    changedFiles = result.ChangedFiles,
                });
                else {
                    AnsiConsole.MarkupLine($"[green]Installed Recipe:[/] {Markup.Escape(RecipeCommandNames.ToCommandName(result.RecipeId))} {Markup.Escape(result.RecipeVersion)}");
                    foreach (string file in result.ChangedFiles) AnsiConsole.WriteLine($"  {file}");
                }
                return SuccessExitCode;
            }
            catch (Exception exception) {
                ShowCommandError("add", XamlNexus.Common.Utils.LanguageRegistry.GetExceptionMessage(exception), jsonOutput, GetErrorCode(exception));
                return GenerationFailureExitCode;
            }
        }

        /// <summary>
        /// 检查已安装 Recipe 的直接依赖关系，确认未被依赖后预览或执行移除
        /// </summary>
        private static int RemoveRecipe(string recipeId, string projectPath, bool dryRun, bool jsonOutput) {
            try {
                IXamlNexusRecipeCatalog catalog = BuiltInRecipeCatalog.Create();
                IXamlNexusRecipe? recipe = catalog.Find(recipeId);
                if (recipe is null) {
                    string available = string.Join(", ", catalog.Recipes.Select(item => RecipeCommandNames.ToCommandName(item.Descriptor.Id)));
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
                        recipeId = RecipeCommandNames.ToCommandName(result.RecipeId),
                        version = result.RecipeVersion,
                        changedFiles = result.ChangedFiles,
                    });
                    return SuccessExitCode;
                }
                AnsiConsole.MarkupLine($"[green]Removed Recipe:[/] {Markup.Escape(RecipeCommandNames.ToCommandName(result.RecipeId))} {Markup.Escape(result.RecipeVersion)}");
                foreach (string file in result.ChangedFiles)
                    AnsiConsole.MarkupLine($"  [grey]-[/] {Markup.Escape(file)}");
                return SuccessExitCode;
            }
            catch (Exception exception) {
                ShowCommandError("remove", XamlNexus.Common.Utils.LanguageRegistry.GetExceptionMessage(exception), jsonOutput, GetErrorCode(exception));
                return GenerationFailureExitCode;
            }
        }

        /// <summary>
        /// 将指定 Recipe 预览更新或更新到内置版本；已匹配版本时直接报告无需更新
        /// </summary>
        private static int UpdateRecipe(string recipeId, string projectPath, bool dryRun, bool jsonOutput) {
            try {
                IXamlNexusRecipeCatalog catalog = BuiltInRecipeCatalog.Create();
                IXamlNexusRecipe? recipe = catalog.Find(recipeId);
                if (recipe is null) {
                    string available = string.Join(", ", catalog.Recipes.Select(item => RecipeCommandNames.ToCommandName(item.Descriptor.Id)));
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
                            recipeId = RecipeCommandNames.ToCommandName(recipe.Descriptor.Id),
                            version = recipe.Descriptor.Version,
                            dryRun,
                        });
                        return SuccessExitCode;
                    }
                    AnsiConsole.MarkupLine(
                        $"[green]Recipe is up to date:[/] {Markup.Escape(RecipeCommandNames.ToCommandName(recipe.Descriptor.Id))} " +
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
                        recipeId = RecipeCommandNames.ToCommandName(result.RecipeId),
                        version = result.RecipeVersion,
                        changedFiles = result.ChangedFiles,
                    });
                    return SuccessExitCode;
                }
                AnsiConsole.MarkupLine(
                    $"[green]Updated Recipe:[/] {Markup.Escape(RecipeCommandNames.ToCommandName(result.RecipeId))} {Markup.Escape(result.RecipeVersion)}");
                foreach (string file in result.ChangedFiles)
                    AnsiConsole.MarkupLine($"  [grey]~[/] {Markup.Escape(file)}");
                return SuccessExitCode;
            }
            catch (Exception exception) {
                ShowCommandError("update", XamlNexus.Common.Utils.LanguageRegistry.GetExceptionMessage(exception), jsonOutput, GetErrorCode(exception));
                return GenerationFailureExitCode;
            }
        }

    }
}
