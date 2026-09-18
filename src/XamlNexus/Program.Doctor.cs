using System.Text.Json;
using System.Text.Json.Serialization;
using Spectre.Console;
using XamlNexus.Common.Utils;
using XamlNexus.Common.Projects;
using XamlNexus.Recipes.BuiltIn;

namespace XamlNexus {
    internal partial class Program {
        /// <summary>
        /// 诊断项目健康状态并输出 JSON 或诊断表；存在错误或诊断失败时返回失败退出码。
        /// </summary>
        private static int Doctor(string projectPath, bool jsonOutput, bool environmentOnly = false) {
            try {
                XamlNexusDoctorReport report = environmentOnly
                    ? XamlNexusDoctor.DiagnoseEnvironment(projectPath)
                    : XamlNexusDoctor.Diagnose(XamlNexusProjectLocator.Locate(projectPath), BuiltInRecipeCatalog.Create());
                if (jsonOutput) {
                    var jsonOptions = new JsonSerializerOptions {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    };
                    jsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
                    Console.WriteLine(JsonSerializer.Serialize(report, jsonOptions));
                }
                else {
                    AnsiConsole.MarkupLine($"{LanguageRegistry.GetText(environmentOnly ? "DoctorScopeEnvironment" : "DoctorScopeProject")}: [cyan]{Markup.Escape(report.RootDirectory)}[/]");
                    var table = new Table().Border(TableBorder.Rounded);
                    table.AddColumn(LanguageRegistry.GetText("DoctorStatus"));
                    table.AddColumn(LanguageRegistry.GetText("DoctorCode"));
                    table.AddColumn(LanguageRegistry.GetText("DoctorCategory"));
                    table.AddColumn(LanguageRegistry.GetText("DoctorDiagnostic"));
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
                            ? "[green]" + Markup.Escape(string.Format(LanguageRegistry.GetText("DoctorHealthy"), report.WarningCount)) + "[/]"
                            : "[red]" + Markup.Escape(string.Format(LanguageRegistry.GetText("DoctorUnhealthy"), report.WarningCount, report.ErrorCount)) + "[/]");
                }
                return report.IsHealthy ? SuccessExitCode : GenerationFailureExitCode;
            }
            catch (Exception exception) {
                if (jsonOutput) {
                    Console.WriteLine(JsonSerializer.Serialize(new { error = XamlNexus.Common.Utils.LanguageRegistry.GetExceptionMessage(exception) }));
                }
                else {
                    ShowOperationalError(XamlNexus.Common.Utils.LanguageRegistry.GetExceptionMessage(exception));
                }
                return GenerationFailureExitCode;
            }
        }

    }
}
