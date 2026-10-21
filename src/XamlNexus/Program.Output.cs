using XamlNexus.Tooling.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spectre.Console;
using XamlNexus.Common.Projects;
using XamlNexus.Common.Recipes;

namespace XamlNexus {
    internal partial class Program {
        /// <summary>
        /// 以 JSON 或控制台文本展示 Recipe 操作的版本和文件变更预览
        /// </summary>
        private static void ShowRecipePreview(XamlNexusRecipePreview preview, bool jsonOutput, string[]? skippedRecipes = null) {
            if (jsonOutput) {
                WriteJson(new {
                    operation = preview.Operation,
                    status = "planned",
                    dryRun = true,
                    recipeId = RecipeCommandNames.ToCommandName(preview.RecipeId),
                    fromVersion = preview.FromVersion,
                    toVersion = preview.ToVersion,
                    manifestWillChange = true,
                    changes = preview.Changes,
                    skippedRecipes = skippedRecipes?.Select(RecipeCommandNames.ToCommandName),
                });
                return;
            }

            string versions = preview.FromVersion is null
                ? preview.ToVersion
                : $"{preview.FromVersion} -> {preview.ToVersion}";
            AnsiConsole.MarkupLine(
                $"[green]Recipe plan:[/] {Markup.Escape(preview.Operation)} " +
                $"{Markup.Escape(RecipeCommandNames.ToCommandName(preview.RecipeId))} {Markup.Escape(versions)}");
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

        /// <summary>显示单个 Recipe 已提交的版本和文件列表</summary>
        private static void ShowAppliedRecipe(string title, XamlNexusRecipeApplyResult result, string? symbol = null) {
            AnsiConsole.MarkupLine(
                $"[green]{title}:[/] {Markup.Escape(RecipeCommandNames.ToCommandName(result.RecipeId))} {Markup.Escape(result.RecipeVersion)}");
            foreach (string file in result.ChangedFiles) {
                if (symbol is null) AnsiConsole.WriteLine($"  {file}");
                else AnsiConsole.MarkupLine($"  [grey]{symbol}[/] {Markup.Escape(file)}");
            }
        }

        /// <summary>
        /// 按输出模式报告命令错误；JSON 包含操作名和可选错误码，文本模式显示错误消息
        /// </summary>
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

        /// <summary>
        /// 提取 Recipe 或项目升级异常的错误码；其他异常返回 null
        /// </summary>
        private static string? GetErrorCode(Exception exception) => exception switch {
            XamlNexusRecipeException recipeException => recipeException.Code,
            XamlNexusProjectUpgradeException upgradeException => upgradeException.Code,
            _ => null,
        };

        /// <summary>
        /// 将对象序列化为缩进的 JSON 并写入标准输出，属性名和枚举文本使用 camelCase
        /// </summary>
        private static void WriteJson(object value) {
            var options = new JsonSerializerOptions {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };
            options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            Console.WriteLine(JsonSerializer.Serialize(value, options));
        }

        /// <summary>
        /// 转义消息中的 Spectre 标记字符，并以红色错误前缀输出操作错误。
        /// </summary>
        private static void ShowOperationalError(string message) {
            AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(message)}");
        }

    }
}
