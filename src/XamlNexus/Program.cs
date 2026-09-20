using XamlNexus.Tooling.CommandLine;
using System.Reflection;
using Spectre.Console;
using XamlNexus.Common.CommandLine;
using XamlNexus.Common.Utils;

namespace XamlNexus {
    internal partial class Program {
        private const int SuccessExitCode = 0;
        private const int GenerationFailureExitCode = 1;
        private const int UsageErrorExitCode = 2;

        /// <summary>
        /// 初始化控制台并解析、分发命令行请求，将参数错误和未处理异常转换为退出码
        /// </summary>
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
                                code = CliErrors.InvalidArguments.Code,
                                message = CliErrors.InvalidArguments.GetMessage(parseResult.Error),
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
                    case CliCommand.Gallery:
                        return RunGalleryAsync().GetAwaiter().GetResult();
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
                        return Doctor(options.ProjectPath!, options.JsonOutput, options.EnvironmentOnly);
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

        /// <summary>
        /// 读取当前程序集的信息版本并移除构建元数据；未声明版本时返回 unknown
        /// </summary>
        private static string GetVersion() {
            var rawVersion = Assembly
                .GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            return rawVersion?.Split('+')[0] ?? "unknown";
        }

        /// <summary>
        /// 输出当前语言对应的命令行帮助
        /// </summary>
        private static void ShowHelp() => AnsiConsole.WriteLine(LanguageRegistry.GetText("Cli_Help"));
    }
}
