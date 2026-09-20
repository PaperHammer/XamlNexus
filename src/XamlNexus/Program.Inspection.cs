using XamlNexus.Tooling.CommandLine;
using Spectre.Console;
using XamlNexus.Common.Projects;

namespace XamlNexus {
    internal partial class Program {
        /// <summary>
        /// 定位项目并以 JSON 或表格列出清单中的项目属性和已登记模块
        /// </summary>
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
                ShowCommandError("list", CliErrors.ListFailed.GetMessage(XamlNexus.Common.Utils.LanguageRegistry.GetExceptionMessage(exception)), jsonOutput, CliErrors.ListFailed.Code);
                return GenerationFailureExitCode;
            }
        }

        /// <summary>
        /// 检查项目清单与受管理结构的一致性并输出问题；仅有警告时仍返回成功
        /// </summary>
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
                ShowCommandError("validate", CliErrors.ValidationFailed.GetMessage(XamlNexus.Common.Utils.LanguageRegistry.GetExceptionMessage(exception)), jsonOutput, CliErrors.ValidationFailed.Code);
                return GenerationFailureExitCode;
            }
        }

    }
}
