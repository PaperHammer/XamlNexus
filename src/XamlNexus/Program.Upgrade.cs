using Spectre.Console;
using XamlNexus.Common.Generators;
using XamlNexus.Common.Projects;
using XamlNexus.Common.Utils;
using XamlNexus.Utils;

namespace XamlNexus {
    internal partial class Program {
        /// <summary>
        /// 生成临时目标脚手架并规划升级或基线补建，按选项处理冲突、预览或应用变更，最后清理临时目录
        /// </summary>
        private static int UpgradeProject(
            string projectPath,
            bool dryRun,
            bool jsonOutput,
            string? conflictOutputPath,
            string? resolveFromPath) {
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
                        ProjectUpgradeErrors.DowngradeUnsupported.GetMessage(current.Manifest.GeneratorVersion, targetVersion),
                        jsonOutput,
                        ProjectUpgradeErrors.DowngradeUnsupported.Code);
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
                        ProjectUpgradeErrors.LegacyBaselineMissing.GetMessage(),
                        jsonOutput,
                        ProjectUpgradeErrors.LegacyBaselineMissing.Code);
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
                if (resolveFromPath is not null)
                    plan = XamlNexusProjectUpgrade.ResolveConflicts(plan, resolveFromPath);
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
                ShowCommandError("upgrade", XamlNexus.Common.Utils.LanguageRegistry.GetExceptionMessage(exception), jsonOutput, GetErrorCode(exception));
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

        /// <summary>
        /// 临时捕获生成器的标准输出以生成升级目标，并在结束时恢复原输出流
        /// </summary>
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

        /// <summary>
        /// 以 JSON 或控制台文本展示升级计划、合并策略、冲突及已导出的冲突文件
        /// </summary>
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

        /// <summary>
        /// 先比较数字版本，再区分正式版和预发布版；同类同数字版本按完整版本字符串的序号顺序比较
        /// </summary>
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

    }
}
