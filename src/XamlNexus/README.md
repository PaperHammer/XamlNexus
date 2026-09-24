# XAML-Nexus

XamlNexus is a .NET CLI for generating Windows desktop prototypes with WinUI 3 or a WinUI 3 frontend and WPF background host. It includes ArcXaml, navigation, MVVM, DI, configuration and logging. The default standard profile also includes settings, themes and language controls.

[中文快速开始：创建 → 运行 → 修改首页 → 添加页面 → 添加能力](https://github.com/PaperHammer/XamlNexus/blob/main/docs/user-guide/quickstart.zh-CN.md)

## Installation

Windows and a .NET 8 / WinUI build environment are required.

```powershell
dotnet tool install --global XamlNexus
xamlnexus --version
xamlnexus doctor --environment
```

This guide describes the current source. Public packages gain new commands when the corresponding version is published.
Use `dotnet tool update --global XamlNexus` to update an installed tool.
Run `xamlnexus gallery` to open the Gallery matching the installed tool version. Official packages include a versioned download manifest: the first launch downloads and verifies the Windows x64 archive; later launches use the local cache and work offline. After updating the tool, run the command again to fetch its matching Gallery. Close a running Gallery before opening another version.
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
xamlnexus page add Projects --kind list
xamlnexus page add ProjectDetails --kind details
xamlnexus page add ProjectEditor --kind form
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
| `tray` | Optional in pure WinUI; already built into hybrid |
| `updater` | Optional in pure WinUI, depends on settings and needs your update source; built into hybrid |
| `editorconfig` | Both architectures |

For an existing basic project, `xamlnexus add settings,sqlite` installs both transactionally.
Do not add capabilities that are already installed. Creating pages does not generate business CRUD or RPC methods.

SLNX is available with `--solution-format slnx` (requires the selected .NET SDK 9.0.200+).
The default remains `sln`. Upgrades preserve the existing format; they do not convert solutions.

## Project maintenance

Run from the generated project directory, or use `--project <directory>`:

```powershell
xamlnexus list
xamlnexus status
xamlnexus validate
xamlnexus doctor
xamlnexus update sqlite --dry-run
xamlnexus update --all --dry-run
xamlnexus upgrade --dry-run
xamlnexus --help
```

`status` summarizes project health and available maintenance actions. `update sqlite` applies to projects with SQLite installed; `update --all` updates all outdated Recipes atomically.
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

完整参数与示例：[English command reference](https://github.com/PaperHammer/XamlNexus/blob/main/docs/user-guide/commands.md) | [中文命令详解](https://github.com/PaperHammer/XamlNexus/blob/main/docs/user-guide/commands.zh-CN.md)。

## Guides

- Documentation: [English](https://github.com/PaperHammer/XamlNexus/blob/main/docs/README.md) | [简体中文](https://github.com/PaperHammer/XamlNexus/blob/main/docs/README.zh-CN.md)
- Architecture: [English](https://github.com/PaperHammer/XamlNexus/blob/main/docs/technical/architecture.md) | [整体架构与关键流程](https://github.com/PaperHammer/XamlNexus/blob/main/docs/technical/architecture.zh-CN.md)
- [Quickstart / 中文快速开始](https://github.com/PaperHammer/XamlNexus/blob/main/docs/user-guide/quickstart.zh-CN.md)
- [Profiles and capability boundaries](https://github.com/PaperHammer/XamlNexus/blob/main/docs/introduction/product-model.zh-CN.md)
- [Pages and ViewModels](https://github.com/PaperHammer/XamlNexus/blob/main/docs/user-guide/business-page.zh-CN.md)
- [Navigation](https://github.com/PaperHammer/XamlNexus/blob/main/docs/technical/navigation.md)
- [SQLite](https://github.com/PaperHammer/XamlNexus/blob/main/docs/user-guide/sqlite-recipe.md)
- [Settings and live localization](https://github.com/PaperHammer/XamlNexus/blob/main/docs/user-guide/localization.md)
- [Project manifest](https://github.com/PaperHammer/XamlNexus/blob/main/docs/technical/project-manifest.md)
- [Scaffold upgrades](https://github.com/PaperHammer/XamlNexus/blob/main/docs/user-guide/project-upgrade.md)
- [Change previews and JSON output](https://github.com/PaperHammer/XamlNexus/blob/main/docs/user-guide/change-plans.md)
