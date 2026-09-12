# Command reference

[English](commands.md) | [简体中文](commands.zh-CN.md)

This reference describes current source. See the [quickstart](quickstart.md) for installation and your first run. Use `xamlnexus --help` for your installed version's help and `xamlnexus --version` for its version.

## Command name and syntax

The package provides `xamlnexus`. In PowerShell, set an alias to use `xn`:

```powershell
Set-Alias -Name xn -Value xamlnexus
xn --help
```

The alias lasts for the current session. Add the `Set-Alias` line to `$PROFILE` for new PowerShell sessions. Installation, update, and uninstall commands still use the package name `XamlNexus`.

`<name>` denotes a value to replace; `[...]` denotes optional arguments. Do not type the brackets. Separate options and values with spaces, and quote paths containing spaces. Help accepts `--help`, `-h`, `-?`, or `help`; version accepts `--version`, `-v`, or `version`.

## Create a project: `new`

```powershell
xamlnexus new MyApp
xamlnexus new DataApp --profile basic --features settings,sqlite
xamlnexus new HybridApp --preset hybrid --output "D:\Projects" --language en-US
```

| Argument | Meaning and default |
|---|---|
| `<name>` or `-n, --name <name>` | Required name: starts with a letter or underscore, contains only letters, digits, or underscores; a lowercase first letter is capitalized |
| `-p, --preset <preset>` | `winui` (default) or `hybrid`; selects the process architecture |
| `-o, --output <directory>` | Parent output directory, defaulting to the current directory; the project is created in a child directory named after it |
| `--profile <profile>` | `standard` (default) or `basic`; basic omits the full settings panel but retains core services |
| `--features <ids>` | Components to add during creation, separated by commas, such as `settings,sqlite` |
| `-l, --language <language>` | `zh-CN` (default) or `en-US`; initial language of the generated project |
| `-f, --solution-format <format>` | `sln` (default) or `slnx`; SLNX requires a selected SDK of 9.0.200 or later |

Preset aliases include `winui3`, `winui-wpf`, and `winui3-wpf`; language aliases include `zh` and `en`. Prefer the standard names in the table.

Running `xamlnexus` without arguments starts interactive creation and requires an interactive terminal. Use `new` with explicit arguments in scripts. `new` does not support `--dry-run` or `--json`.

## Select an existing project

Project commands locate a project from the current directory by default. Use `-p, --project <path>` for a directory or an `xamlnexus.json` file:

```powershell
xamlnexus list --project "D:\Projects\MyApp"
xamlnexus validate --project "D:\Projects\MyApp\xamlnexus.json"
```

`run`, `list`, `validate`, `doctor`, and `upgrade` also accept a positional path, such as `xamlnexus run ./MyApp`. Supply the path once, either positionally or through `--project`. For `new`, `-p` means preset, not project path.

## Run for development: `run`

```powershell
xamlnexus run
xamlnexus run --dry-run
xamlnexus run --no-build
```

Accepts a project path, `--no-build`, `--dry-run`, and `--json`. By default it builds and runs a Debug/x64/unpackaged application, selecting the WinUI entry point or hybrid background host automatically.

- `--dry-run` shows the build and launch operations without starting the application.
- `--no-build` runs existing output without compiling recent edits; a previous build is required.
- Ctrl+C stops this run. Run the command again after editing. There are no watch, hot reload, or Release/MSIX running options.

## Add a page: `page add`

```powershell
xamlnexus page add Orders --dry-run
xamlnexus page add Orders
xamlnexus page add Details --no-navigation
```

Accepts `--project`, `--no-navigation`, `--dry-run`, and `--json`. Names start with an uppercase English letter and contain only English letters or digits, such as `Orders`.

By default, it generates an ordinary Page and ViewModel and connects navigation. `--no-navigation` skips navigation integration for projects managing navigation themselves. There are no `--kind list` or `--kind form` options, and no business CRUD generation. Edit the generated source directly; see [page development](business-page.md) for file locations and integration details.

## Inspect and manage components

| Command | Purpose | Options |
|---|---|---|
| `recipes` | List components provided by the current tool | `--json` |
| `list` | List installed project modules | Project path, `--json` |
| `add <id[,id...]>` | Add one or more components | `--project`, `--dry-run`, `--json` |
| `remove <id>` | Remove one component | `--project`, `--dry-run`, `--json` |
| `update <id>` | Update one component to the version provided by the current tool | `--project`, `--dry-run`, `--json` |

```powershell
xamlnexus recipes
xamlnexus add settings,sqlite --dry-run
xamlnexus add settings,sqlite
xamlnexus update sqlite --dry-run
xamlnexus remove sqlite --dry-run
```

The batch-add example requires a project without either component, such as a basic project. Standard already includes settings. Do not add installed components again. `remove` and `update` handle one component at a time. Updating to the same version does nothing; downgrades are rejected.

Built-in components are `settings`, `editorconfig`, `sqlite`, `system-tray`, and `app-update`. The first three support both architectures; the last two serve pure WinUI, since hybrid already includes those capabilities. `app-update` depends on `settings`. Batch addition handles dependency ordering. See the [product model](../introduction/product-model.md) for boundaries.

You can customize generated source and configuration. Component updates and removal check files and stop on user changes or missing files to protect your work. They do not discard edits or recalculate hashes to bypass checks.

## Check a project: `validate` and `doctor`

```powershell
xamlnexus validate --json
xamlnexus doctor
```

Both accept a project path and `--json`. `validate` checks the manifest and tracked files; content changes normally produce warnings, while missing files are errors. `doctor` also checks the development environment, project references, and component integration. Neither replaces builds or GUI checks. See [diagnostics](doctor.md).

## Upgrade scaffold infrastructure: `upgrade`

```powershell
xamlnexus upgrade --dry-run
xamlnexus upgrade
xamlnexus upgrade --conflict-output ./upgrade-conflicts
```

Accepts a project path, `--dry-run`, `--json`, and `--conflict-output <directory>`. It merges scaffold-managed infrastructure against generation baselines, preserving user edits that can be merged safely and reporting conflicts otherwise.

`--conflict-output` exports files for resolving merge conflicts when they occur. It writes to the destination, so it cannot be combined with `--dry-run`. It is not an export-only mode: without conflicts, the upgrade still proceeds. Upgrades preserve the solution format rather than converting SLN to SLNX. See [project upgrades](project-upgrade.md).

## Previews, JSON, and exit codes

`--dry-run` is available for `run`, `page add`, `add`, `remove`, `update`, and `upgrade`. It checks and displays pending operations without applying project changes. `--json` changes output formatting only; it does not prevent writes or application startup. Combine both for automated previews:

```powershell
xamlnexus add sqlite --dry-run --json
```

Regular commands return `0` on success, `1` on execution or validation failure, and `2` for argument errors. `run` also returns the application's exit code; Ctrl+C cancellation returns `130`. See [change previews and JSON output](change-plans.md) for fields and output boundaries.

## Three different updates

| Command | What it updates |
|---|---|
| `dotnet tool update --global XamlNexus` | The installed CLI tool |
| `xamlnexus update sqlite` | The project's installed SQLite component |
| `xamlnexus upgrade` | Scaffold-managed project infrastructure |

Updating the tool does not modify existing projects. Save or commit edits before project updates, then preview the changes. Client installers, signing, and online-update releases use the generated project's own release configuration; see the [quickstart](quickstart.md).
