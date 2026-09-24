using XamlNexus.Tooling.CommandLine;
using XamlNexus.Common.Generators;
using XamlNexus.Common.Utils;

namespace XamlNexus.Tooling.CommandLine;

public enum CliCommand {
    Interactive,
    New,
    Run,
    Gallery,
    PageAdd,
    List,
    Status,
    Validate,
    Recipes,
    Add,
    Remove,
    Update,
    Doctor,
    Upgrade,
    Help,
    Version,
}

public sealed record CliOptions(
    CliCommand Command,
    ProjectConfig? Project = null,
    string? ProjectPath = null,
    string? RecipeId = null,
    bool JsonOutput = false,
    bool DryRun = false,
    string? ConflictOutputPath = null,
    string? PageName = null,
    bool SkipNavigation = false,
    string Profile = "standard",
    IReadOnlyList<string>? Features = null,
    bool NoBuild = false,
    string? ResolveFromPath = null,
    bool EnvironmentOnly = false,
    string PageKind = "blank",
    bool UpdateAll = false);

public sealed record CliParseResult(CliOptions? Options, string? Error) {
    public bool Success => Options is not null;

    public static CliParseResult Parsed(CliOptions options) => new(options, null);

    public static CliParseResult Failed(string error) => new(null, error);
}

public static class CliParser {
    public static CliParseResult Parse(string[] args, string currentDirectory) {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length == 0)
            return CliParseResult.Parsed(new CliOptions(CliCommand.Interactive));

        if (args[0].Equals("gallery", StringComparison.OrdinalIgnoreCase)) {
            if (args.Skip(1).Any(IsHelp)) return CliParseResult.Parsed(new CliOptions(CliCommand.Help));
            return args.Length == 1
                ? CliParseResult.Parsed(new CliOptions(CliCommand.Gallery))
                : CliParseResult.Failed("Usage: xamlnexus gallery");
        }

        if (args[0].Equals("page", StringComparison.OrdinalIgnoreCase)) {
            if (args.Skip(1).Any(IsHelp)) return CliParseResult.Parsed(new CliOptions(CliCommand.Help));
            if (args.Length < 2 || !args[1].Equals("add", StringComparison.OrdinalIgnoreCase))
                return CliParseResult.Failed("Usage: xamlnexus page add Orders [--project <directory>] [--kind blank|list|details|form] [--no-navigation] [--dry-run] [--json]");
            return ParseNamedCommand(CliCommand.PageAdd, args, currentDirectory, startIndex: 2);
        }

        if (args.Length == 1) {
            if (IsHelp(args[0]) || args[0].Equals("help", StringComparison.OrdinalIgnoreCase))
                return CliParseResult.Parsed(new CliOptions(CliCommand.Help));

            if (IsVersion(args[0]) || args[0].Equals("version", StringComparison.OrdinalIgnoreCase))
                return CliParseResult.Parsed(new CliOptions(CliCommand.Version));
        }

        if (args[0].Equals("list", StringComparison.OrdinalIgnoreCase))
            return ParseProjectCommand(CliCommand.List, args, currentDirectory);

        if (args[0].Equals("status", StringComparison.OrdinalIgnoreCase))
            return ParseProjectCommand(CliCommand.Status, args, currentDirectory);

        if (args[0].Equals("run", StringComparison.OrdinalIgnoreCase)) {
            if (args.Skip(1).Any(IsHelp)) return CliParseResult.Parsed(new CliOptions(CliCommand.Help));
            var remaining = new List<string> { "run" };
            bool noBuild = false, dryRun = false;
            for (int index = 1; index < args.Length; index++) {
                string argument = args[index];
                if (argument == "--no-build") {
                    if (noBuild) return CliParseResult.Failed("The --no-build option can only be specified once.");
                    noBuild = true;
                }
                else if (argument == "--dry-run") {
                    if (dryRun) return CliParseResult.Failed("The --dry-run option can only be specified once.");
                    dryRun = true;
                }
                else {
                    remaining.Add(argument);
                    if (argument is "--project" or "-p" && index + 1 < args.Length) remaining.Add(args[++index]);
                }
            }
            var parsed = ParseProjectCommand(CliCommand.Run, remaining.ToArray(), currentDirectory);
            return parsed.Success ? CliParseResult.Parsed(parsed.Options! with { NoBuild = noBuild, DryRun = dryRun }) : parsed;
        }

        if (args[0].Equals("validate", StringComparison.OrdinalIgnoreCase))
            return ParseProjectCommand(CliCommand.Validate, args, currentDirectory);

        if (args[0].Equals("doctor", StringComparison.OrdinalIgnoreCase))
            return ParseDoctorCommand(args, currentDirectory);

        if (args[0].Equals("upgrade", StringComparison.OrdinalIgnoreCase))
            return ParseUpgradeCommand(args, currentDirectory);

        if (args[0].Equals("recipes", StringComparison.OrdinalIgnoreCase))
            return ParseRecipesCommand(args);

        if (args[0].Equals("add", StringComparison.OrdinalIgnoreCase))
            return ParseNamedCommand(CliCommand.Add, args, currentDirectory);

        if (args[0].Equals("remove", StringComparison.OrdinalIgnoreCase))
            return ParseNamedCommand(CliCommand.Remove, args, currentDirectory);

        if (args[0].Equals("update", StringComparison.OrdinalIgnoreCase))
            return ParseUpdateCommand(args, currentDirectory);

        if (!args[0].Equals("new", StringComparison.OrdinalIgnoreCase))
            return CliParseResult.Failed($"Unknown command '{args[0]}'.");

        if (args.Skip(1).Any(IsHelp))
            return CliParseResult.Parsed(new CliOptions(CliCommand.Help));

        string? name = null;
        string preset = "winui";
        string output = currentDirectory;
        string language = "zh-CN";
        string solutionFormat = "sln";
        string? profile = null;
        string? features = null;

        for (int index = 1; index < args.Length; index++) {
            string argument = args[index];
            switch (argument.ToLowerInvariant()) {
                case "--name":
                case "-n":
                    if (!TryReadValue(args, ref index, argument, out name, out string? nameError))
                        return CliParseResult.Failed(nameError!);
                    break;
                case "--preset":
                case "-p":
                    if (!TryReadValue(args, ref index, argument, out preset, out string? presetError))
                        return CliParseResult.Failed(presetError!);
                    break;
                case "--profile":
                    if (profile is not null) return CliParseResult.Failed("The --profile option can only be specified once.");
                    if (!TryReadValue(args, ref index, argument, out profile, out string? profileError))
                        return CliParseResult.Failed(profileError!);
                    if (profile is not ("standard" or "basic"))
                        return CliParseResult.Failed("Profile must be standard or basic.");
                    break;
                case "--features":
                    if (features is not null) return CliParseResult.Failed("The --features option can only be specified once.");
                    if (!TryReadValue(args, ref index, argument, out features, out string? featuresError))
                        return CliParseResult.Failed(featuresError!);
                    if (features.Split(',').Any(string.IsNullOrWhiteSpace))
                        return CliParseResult.Failed("Use a comma-separated list of nonempty Recipe ids.");
                    break;
                case "--output":
                case "-o":
                    if (!TryReadValue(args, ref index, argument, out output, out string? outputError))
                        return CliParseResult.Failed(outputError!);
                    break;
                case "--language":
                case "-l":
                    if (!TryReadValue(args, ref index, argument, out language, out string? languageError))
                        return CliParseResult.Failed(languageError!);
                    break;
                case "--solution-format":
                case "-f":
                    if (!TryReadValue(args, ref index, argument, out solutionFormat, out string? formatError))
                        return CliParseResult.Failed(formatError!);
                    break;
                default:
                    if (argument.StartsWith('-'))
                        return CliParseResult.Failed($"Unknown option '{argument}'.");
                    if (name is not null)
                        return CliParseResult.Failed("Only one project name can be specified.");
                    name = argument;
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(name))
            return CliParseResult.Failed("A project name is required. Example: xamlnexus new MyApp");

        if (!IsValidProjectName(name))
            return CliParseResult.Failed(
                "The project name must start with a letter or underscore and contain only letters, digits, or underscores.");

        FrameworkType framework;
        switch (preset.ToLowerInvariant()) {
            case "winui":
            case "winui3":
                framework = FrameworkType.Winui3;
                break;
            case "hybrid":
            case "winui-wpf":
            case "winui3-wpf":
                framework = FrameworkType.Winui3_Wpf;
                break;
            default:
                return CliParseResult.Failed(
                    $"Unknown preset '{preset}'. Supported presets: winui, hybrid.");
        }

        language = language.ToLowerInvariant() switch {
            "zh" or "zh-cn" => "zh-CN",
            "en" or "en-us" => "en-US",
            _ => string.Empty,
        };
        if (language.Length == 0)
            return CliParseResult.Failed("Unsupported language. Supported languages: zh-CN, en-US.");

        if (!new[] { "sln", "slnx" }.Contains(solutionFormat, StringComparer.OrdinalIgnoreCase))
            return CliParseResult.Failed("Unsupported solution format. Supported formats: sln, slnx.");

        try {
            output = Path.GetFullPath(output, currentDirectory);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException) {
            return CliParseResult.Failed($"Invalid output path: {exception.Message}");
        }

        var project = new ProjectConfig {
            Profile = profile ?? "standard",
            SlnName = NormalizeProjectName(name),
            OutputPath = output,
            Language = language,
            Framework = framework,
            SlnType = solutionFormat.Equals("slnx", StringComparison.OrdinalIgnoreCase) ? SolutionType.Slnx : SolutionType.Sln,
        };
        return CliParseResult.Parsed(new CliOptions(CliCommand.New, project, Profile: profile ?? "standard",
            Features: features?.Split(',', StringSplitOptions.TrimEntries).Select(RecipeCommandNames.ToRecipeId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()));
    }

    private static CliParseResult ParseProjectCommand(
        CliCommand command,
        string[] args,
        string currentDirectory,
        bool allowDryRun = false) {
        if (args.Skip(1).Any(IsHelp))
            return CliParseResult.Parsed(new CliOptions(CliCommand.Help));

        string? projectPath = null;
        bool jsonOutput = false;
        bool dryRun = false;
        for (int index = 1; index < args.Length; index++) {
            string argument = args[index];
            if (argument is "--project" or "-p") {
                if (projectPath is not null)
                    return CliParseResult.Failed("The project path can only be specified once.");
                if (!TryReadValue(args, ref index, argument, out projectPath, out string? error))
                    return CliParseResult.Failed(error!);
            }
            else if (argument.Equals("--json", StringComparison.OrdinalIgnoreCase)) {
                if (jsonOutput) return CliParseResult.Failed("The --json option can only be specified once.");
                jsonOutput = true;
            }
            else if (allowDryRun && argument.Equals("--dry-run", StringComparison.OrdinalIgnoreCase)) {
                if (dryRun) return CliParseResult.Failed("The --dry-run option can only be specified once.");
                dryRun = true;
            }
            else if (argument.StartsWith('-')) {
                return CliParseResult.Failed($"Unknown option '{argument}'.");
            }
            else if (projectPath is not null) {
                return CliParseResult.Failed("The project path can only be specified once.");
            }
            else {
                projectPath = argument;
            }
        }

        try {
            projectPath = Path.GetFullPath(projectPath ?? currentDirectory, currentDirectory);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException) {
            return CliParseResult.Failed($"Invalid project path: {exception.Message}");
        }

        return CliParseResult.Parsed(new CliOptions(
            command,
            ProjectPath: projectPath,
            JsonOutput: jsonOutput,
            DryRun: dryRun));
    }

    private static CliParseResult ParseRecipesCommand(string[] args) {
        if (args.Skip(1).Any(IsHelp))
            return CliParseResult.Parsed(new CliOptions(CliCommand.Help));
        bool jsonOutput = false;
        foreach (string argument in args.Skip(1)) {
            if (argument.Equals("--json", StringComparison.OrdinalIgnoreCase)) {
                if (jsonOutput) return CliParseResult.Failed("The --json option can only be specified once.");
                jsonOutput = true;
            }
            else if (argument.StartsWith('-')) {
                return CliParseResult.Failed($"Unknown option '{argument}'.");
            }
            else {
                return CliParseResult.Failed("The recipes command does not accept positional arguments.");
            }
        }
        return CliParseResult.Parsed(new CliOptions(
            CliCommand.Recipes,
            JsonOutput: jsonOutput));
    }

    private static CliParseResult ParseNamedCommand(
        CliCommand command,
        string[] args,
        string currentDirectory,
        int startIndex = 1) {
        if (args.Skip(startIndex).Any(IsHelp))
            return CliParseResult.Parsed(new CliOptions(CliCommand.Help));

        string? name = null;
        string? projectPath = null;
        bool jsonOutput = false;
        bool dryRun = false;
        bool skipNavigation = false;
        string? pageKind = null;
        for (int index = startIndex; index < args.Length; index++) {
            string argument = args[index];
            if (argument is "--project" or "-p") {
                if (projectPath is not null)
                    return CliParseResult.Failed("The project path can only be specified once.");
                if (!TryReadValue(args, ref index, argument, out projectPath, out string? error))
                    return CliParseResult.Failed(error!);
            }
            else if (argument.Equals("--json", StringComparison.OrdinalIgnoreCase)) {
                if (jsonOutput) return CliParseResult.Failed("The --json option can only be specified once.");
                jsonOutput = true;
            }
            else if (argument.Equals("--dry-run", StringComparison.OrdinalIgnoreCase)) {
                if (dryRun) return CliParseResult.Failed("The --dry-run option can only be specified once.");
                dryRun = true;
            }
            else if (command == CliCommand.PageAdd && argument.Equals("--kind", StringComparison.OrdinalIgnoreCase)) {
                if (pageKind is not null) return CliParseResult.Failed("The --kind option can only be specified once.");
                if (!TryReadValue(args, ref index, argument, out pageKind, out string? kindError))
                    return CliParseResult.Failed(kindError!);
                if (pageKind is not ("blank" or "list" or "details" or "form"))
                    return CliParseResult.Failed("Page kind must be blank, list, details or form.");
            }
            else if (command == CliCommand.PageAdd && argument.Equals("--no-navigation", StringComparison.OrdinalIgnoreCase)) {
                if (skipNavigation) return CliParseResult.Failed("The --no-navigation option can only be specified once.");
                skipNavigation = true;
            }
            else if (argument.StartsWith('-')) {
                return CliParseResult.Failed($"Unknown option '{argument}'.");
            }
            else if (name is not null) {
                if (command == CliCommand.Add &&
                    (name.TrimEnd().EndsWith(',') || argument.TrimStart().StartsWith(',')))
                    name += argument.Trim();
                else
                    return CliParseResult.Failed(command == CliCommand.PageAdd
                        ? "Only one page name can be specified."
                        : "Only one Recipe id can be specified.");
            }
            else {
                name = argument;
            }
        }

        if (string.IsNullOrWhiteSpace(name))
            return CliParseResult.Failed(command == CliCommand.PageAdd
                ? "A page name is required."
                : "A Recipe id is required. Run 'xamlnexus recipes' to list available Recipes.");

        if (command == CliCommand.Add && name.Split(',').Any(string.IsNullOrWhiteSpace))
            return CliParseResult.Failed("Use a comma-separated list of nonempty Recipe ids.");

        try {
            projectPath = Path.GetFullPath(projectPath ?? currentDirectory, currentDirectory);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException) {
            return CliParseResult.Failed($"Invalid project path: {exception.Message}");
        }

        return CliParseResult.Parsed(new CliOptions(
            command,
            ProjectPath: projectPath,
            RecipeId: command == CliCommand.PageAdd ? null
                : string.Join(",", name.Split(',').Select(RecipeCommandNames.ToRecipeId)),
            PageName: command == CliCommand.PageAdd ? name : null,
            JsonOutput: jsonOutput,
            DryRun: dryRun,
            SkipNavigation: skipNavigation, PageKind: pageKind ?? "blank"));
    }

    private static CliParseResult ParseUpdateCommand(string[] args, string currentDirectory) {
        if (args.Skip(1).Any(IsHelp))
            return CliParseResult.Parsed(new CliOptions(CliCommand.Help));

        bool updateAll = args.Skip(1).Any(argument => argument.Equals("--all", StringComparison.OrdinalIgnoreCase));
        if (!updateAll)
            return ParseNamedCommand(CliCommand.Update, args, currentDirectory);

        if (args.Skip(1).Count(argument => argument.Equals("--all", StringComparison.OrdinalIgnoreCase)) > 1)
            return CliParseResult.Failed("The --all option can only be specified once.");

        string[] remaining = args.Where(argument => !argument.Equals("--all", StringComparison.OrdinalIgnoreCase)).ToArray();
        var parsed = ParseProjectCommand(CliCommand.Update, remaining, currentDirectory, allowDryRun: true);
        return parsed.Success ? CliParseResult.Parsed(parsed.Options! with { UpdateAll = true }) : parsed;
    }

    private static CliParseResult ParseUpgradeCommand(string[] args, string currentDirectory) {
        if (args.Skip(1).Any(IsHelp))
            return CliParseResult.Parsed(new CliOptions(CliCommand.Help));
        string? projectPath = null;
        bool jsonOutput = false;
        bool dryRun = false;
        string? conflictOutputPath = null;
        string? resolveFromPath = null;
        for (int index = 1; index < args.Length; index++) {
            string argument = args[index];
            if (argument is "--project" or "-p") {
                if (projectPath is not null)
                    return CliParseResult.Failed("The project path can only be specified once.");
                if (!TryReadValue(args, ref index, argument, out projectPath, out string? error))
                    return CliParseResult.Failed(error!);
            }
            else if (argument.Equals("--json", StringComparison.OrdinalIgnoreCase)) {
                if (jsonOutput) return CliParseResult.Failed("The --json option can only be specified once.");
                jsonOutput = true;
            }
            else if (argument.Equals("--dry-run", StringComparison.OrdinalIgnoreCase)) {
                if (dryRun) return CliParseResult.Failed("The --dry-run option can only be specified once.");
                dryRun = true;
            }
            else if (argument.Equals("--conflict-output", StringComparison.OrdinalIgnoreCase)) {
                if (conflictOutputPath is not null)
                    return CliParseResult.Failed("The --conflict-output option can only be specified once.");
                if (!TryReadValue(args, ref index, argument, out conflictOutputPath, out string? error))
                    return CliParseResult.Failed(error!);
            }
            else if (argument.Equals("--resolve-from", StringComparison.OrdinalIgnoreCase)) {
                if (resolveFromPath is not null)
                    return CliParseResult.Failed("The --resolve-from option can only be specified once.");
                if (!TryReadValue(args, ref index, argument, out resolveFromPath, out string? error))
                    return CliParseResult.Failed(error!);
            }
            else if (argument.StartsWith('-')) {
                return CliParseResult.Failed($"Unknown option '{argument}'.");
            }
            else if (projectPath is not null) {
                return CliParseResult.Failed("The project path can only be specified once.");
            }
            else {
                projectPath = argument;
            }
        }
        try {
            projectPath = Path.GetFullPath(projectPath ?? currentDirectory, currentDirectory);
            if (resolveFromPath is not null)
                resolveFromPath = Path.GetFullPath(resolveFromPath, currentDirectory);
            if (conflictOutputPath is not null)
                conflictOutputPath = Path.GetFullPath(conflictOutputPath, currentDirectory);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException) {
            return CliParseResult.Failed($"Invalid project path: {exception.Message}");
        }
        if (resolveFromPath is not null && conflictOutputPath is not null)
            return CliParseResult.Failed("--resolve-from cannot be combined with --conflict-output.");
        if (dryRun && conflictOutputPath is not null)
            return CliParseResult.Failed("--dry-run cannot be combined with --conflict-output because conflict export writes files.");
        return CliParseResult.Parsed(new CliOptions(
            CliCommand.Upgrade,
            ProjectPath: projectPath,
            JsonOutput: jsonOutput,
            DryRun: dryRun,
            ConflictOutputPath: conflictOutputPath,
            ResolveFromPath: resolveFromPath));
    }

    private static CliParseResult ParseDoctorCommand(string[] args, string currentDirectory) {
        if (args.Skip(1).Any(IsHelp)) return CliParseResult.Parsed(new CliOptions(CliCommand.Help));
        bool environmentOnly = false;
        var remaining = new List<string> { "doctor" };
        for (int index = 1; index < args.Length; index++) {
            if (args[index].Equals("--environment", StringComparison.OrdinalIgnoreCase)) {
                if (environmentOnly) return CliParseResult.Failed("The --environment option can only be specified once.");
                environmentOnly = true;
            }
            else {
                remaining.Add(args[index]);
                if (args[index] is "--project" or "-p" && index + 1 < args.Length)
                    remaining.Add(args[++index]);
            }
        }
        // 环境模式以当前目录选择 SDK（包括祖先 global.json），不接受项目路径。
        if (environmentOnly && remaining.Skip(1).Any(arg => !arg.Equals("--json", StringComparison.OrdinalIgnoreCase)))
            return CliParseResult.Failed("Use doctor --environment from the directory to inspect; do not specify a project path.");
        var parsed = ParseProjectCommand(CliCommand.Doctor, remaining.ToArray(), currentDirectory);
        return parsed.Success ? CliParseResult.Parsed(parsed.Options! with { EnvironmentOnly = environmentOnly }) : parsed;
    }

    private static bool TryReadValue(
        string[] args,
        ref int index,
        string option,
        out string value,
        out string? error) {
        if (index + 1 >= args.Length || args[index + 1].StartsWith('-')) {
            value = string.Empty;
            error = $"Option '{option}' requires a value.";
            return false;
        }

        value = args[++index];
        error = null;
        return true;
    }

    private static bool IsHelp(string argument) => argument is "--help" or "-h" or "-?";

    private static bool IsVersion(string argument) => argument is "--version" or "-v";

    private static bool IsValidProjectName(string name) =>
        !string.IsNullOrWhiteSpace(name)
        && (char.IsLetter(name[0]) || name[0] == '_')
        && name.All(character => char.IsLetterOrDigit(character) || character == '_');

    private static string NormalizeProjectName(string name) =>
        char.IsLower(name[0]) ? char.ToUpperInvariant(name[0]) + name[1..] : name;
}
