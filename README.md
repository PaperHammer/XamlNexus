# XAML-Nexus

![NuGet Version](https://img.shields.io/nuget/v/XamlNexus)
[![GitHub stars](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fapi.github.com%2Frepos%2FPaperHammer%2FXamlNexus&query=stargazers_count&label=Stars&color=pink)](https://github.com/PaperHammer/XamlNexus/stargazers)
[![Documentation](https://img.shields.io/badge/Docs-Quickstart-green)](docs/user-guide/quickstart.zh-CN.md)
[![Issues](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fapi.github.com%2Frepos%2FPaperHammer%2FXamlNexus&query=open_issues&label=Issues&color=orange)](https://github.com/PaperHammer/XamlNexus/issues)

XamlNexus is a .NET CLI for generating Windows desktop prototypes with WinUI 3 or a WinUI 3 frontend and WPF background host. It includes ArcXaml, navigation, MVVM, DI, configuration and logging. The default standard profile also includes settings, themes and language controls.

[中文快速开始：创建 → 运行 → 修改首页 → 添加页面 → 添加能力](docs/user-guide/quickstart.zh-CN.md)

## Installation

Windows and a .NET 8 / WinUI build environment are required.

```powershell
dotnet tool install --global XamlNexus
xamlnexus --version
```

This guide describes the current source. Public packages gain new commands when the corresponding version is published.
Use `dotnet tool update --global XamlNexus` to update an installed tool.
For unreleased source, follow the source-build instructions in the quickstart.
Uninstall with `dotnet tool uninstall --global XamlNexus`.

The installed command is `xamlnexus`; installation does not create an `xn` command or change your shell profile.
For an optional PowerShell shortcut, set the alias yourself:

```powershell
Set-Alias -Name xn -Value xamlnexus
xn --help
```

Both names then work in the current session. Add the `Set-Alias` line to `$PROFILE` to use the shortcut in new PowerShell sessions.

## First development workflow

```powershell
xamlnexus new MyApp
cd MyApp
xamlnexus run
```

Press Ctrl+C to stop this run before editing and running again.
Edit `MyApp.MainPanel/MainPage.xaml` for the home layout and
`MyApp.MainPanel/ViewModels/MainViewModel.cs` for page state and logic.

```powershell
xamlnexus page add Workspace --dry-run
xamlnexus page add Workspace
xamlnexus run
```

The new ordinary page includes a ViewModel and navigation registration. Stop the app, then add capabilities when needed:

```powershell
xamlnexus recipes
xamlnexus add sqlite
xamlnexus run
```

`run` builds and starts Debug/x64/unpackaged applications. It selects the WinUI executable or hybrid host automatically.
It does not provide watch or hot reload. Use `run --dry-run` to inspect the plan or `run --no-build` to start existing output.

## Choose a starting point

```powershell
# Interactive architecture, profile and component selection
xamlnexus

# Core infrastructure without the full settings panel
xamlnexus new MinimalApp --profile basic

# Compose capabilities during creation
xamlnexus new DataApp --profile basic --features settings,sqlite

# WinUI frontend with a WPF background host
xamlnexus new HybridApp --preset hybrid
```

`--preset winui|hybrid` selects architecture; `--profile standard|basic` selects initial capabilities.
The defaults are winui and standard. Basic retains theme, localization, configuration and logging infrastructure.

| Recipe | Availability |
|---|---|
| `settings` | Optional in basic; included in standard |
| `sqlite` | Both architectures; hybrid data access belongs to the host |
| `system-tray` | Optional in pure WinUI; already built into hybrid |
| `app-update` | Optional in pure WinUI, depends on settings and needs your update source; built into hybrid |
| `editorconfig` | Both architectures |

For an existing basic project, `xamlnexus add settings,sqlite` installs both transactionally.
Do not add capabilities that are already installed. Creating pages does not generate business CRUD or RPC methods.

SLNX is available with `--solution-format slnx` (requires the selected .NET SDK 9.0.200+).
The default remains `sln`. Upgrades preserve the existing format; they do not convert solutions.

## Project maintenance

Run from the generated project directory, or use `--project <directory>`:

```powershell
xamlnexus list
xamlnexus validate
xamlnexus doctor
xamlnexus update sqlite --dry-run
xamlnexus upgrade --dry-run
xamlnexus --help
```

`update sqlite` applies to projects with SQLite installed.
The generated `xamlnexus.json` records architecture, starting profile and installed capabilities.
Publication and signing are separate from development and remain maintainer-controlled.

## Usage notes

- Generated source and configuration are yours to edit. Component update/removal stops when tracked files have changed or are missing; scaffold upgrades report changes they cannot merge safely.
- Use `--dry-run` before automatic changes. `--json` only changes output formatting and does not prevent writes. `upgrade --conflict-output` writes conflict files and cannot be combined with `--dry-run`.
- `new -p` selects a preset; project commands use `-p` for the project path. Quote paths containing spaces.
- Updating the CLI with `dotnet tool update --global XamlNexus` does not update existing projects. Use `update <id>` for a component or `upgrade` for scaffold infrastructure.
- `xn` requires the PowerShell alias above. Development running does not replace installer creation, signing, or release configuration.

## 中文使用说明

在 Windows 上准备 .NET 8 SDK 和 WinUI 构建环境后，安装工具并创建项目。正式命令是 `xamlnexus`，安装不会自动提供 `xn` 或修改终端配置。下面演示可选的 PowerShell 别名，需要手动设置；不使用别名时，将示例中的 `xn` 写成 `xamlnexus` 即可：

```powershell
dotnet tool install --global XamlNexus
Set-Alias -Name xn -Value xamlnexus
xn new MyApp
cd MyApp
xn run
```

按 Ctrl+C 停止运行后，修改 `MyApp.MainPanel/MainPage.xaml` 和 `ViewModels/MainViewModel.cs`，再执行 `xn run`。添加页面用 `xn page add Orders`，查看可用组件用 `xn recipes`，按需添加用 `xn add sqlite`。

- `xn` 是当前 PowerShell 会话的别名；把 `Set-Alias` 那行加入 `$PROFILE` 才能在新会话中使用。
- 默认生成标准版；需要省去完整设置界面时用 `--profile basic`。两者都保留配置、日志、导航、主题和多语言基础机制。
- 所有生成源码和配置都可以修改。组件更新或移除遇到已修改、缺失的文件时会停止；脚手架升级无法安全合并时会报告冲突。
- 自动修改前先保存或提交代码，并用 `--dry-run` 预览。`--json` 只控制输出格式，不代表预览。
- `run` 使用 Debug/x64/非打包模式，没有热重载。安装包、签名和在线更新发布需配置生成项目的发布文件。

完整参数与示例：[English command reference](docs/user-guide/commands.md) | [中文命令详解](docs/user-guide/commands.zh-CN.md)。

## 命令帮助与列表 / Help and lists

| 命令 / Command | 用途 / Purpose |
|---|---|
| `xamlnexus --help` 或 / or `xamlnexus help` | 查看命令、参数和示例 / Show commands, options, and examples |
| `xamlnexus new --help` | 查看帮助，目前仍显示全局帮助 / Show help; currently the same global help |
| `xamlnexus recipes` | 列出当前工具提供的可添加组件 / List available components |
| `xamlnexus list` | 列出当前项目已安装的模块 / List installed project modules |
| `xamlnexus list --project ./MyApp` | 查看指定项目的模块 / List modules in a specified project |
| `xamlnexus --version` | 查看工具版本 / Show the tool version |

参数统一列在 `--help` 中，目前没有独立的参数列表命令或按子命令区分的帮助。`recipes` 和 `list` 支持 `--json`，方便脚本读取。设置上文的 PowerShell 别名后，这些命令也可以使用 `xn`。

Options are listed in `--help`; there is currently no separate option-list command or command-specific help. Both `recipes` and `list` support `--json` for scripts. After setting the PowerShell alias above, you can also use `xn`.

完整参数说明 / Full command reference: [English](docs/user-guide/commands.md) | [简体中文](docs/user-guide/commands.zh-CN.md).

## Guides

- Documentation: [English](docs/README.md) | [简体中文](docs/README.zh-CN.md)
- Architecture: [English](docs/technical/architecture.md) | [整体架构与关键流程](docs/technical/architecture.zh-CN.md)
- [Quickstart / 中文快速开始](docs/user-guide/quickstart.zh-CN.md)
- [Profiles and capability boundaries](docs/introduction/product-model.zh-CN.md)
- [Pages and ViewModels](docs/user-guide/business-page.zh-CN.md)
- [Navigation](docs/technical/navigation.md)
- [SQLite](docs/user-guide/sqlite-recipe.md)
- [Settings and live localization](docs/user-guide/localization.md)
- [Project manifest](docs/technical/project-manifest.md)
- [Scaffold upgrades](docs/user-guide/project-upgrade.md)
- [Change previews and JSON output](docs/user-guide/change-plans.md)

## Build from this repository

```powershell
dotnet build ./src/XamlNexus/XamlNexus.csproj
dotnet ./src/XamlNexus/bin/Debug/net8.0/XamlNexus.dll --help
```

Generated-project build acceptance (PowerShell 7, no GUI launch):

```powershell
./eng/Test-GeneratedProjects.ps1
./eng/Test-GeneratedProjects.ps1 -Profile standard
```

Each invocation checks both architectures before and after adding a page and components.
Logs are saved under `.artifacts/generated-build-*/`; generated projects remain in the reported temporary directory.
