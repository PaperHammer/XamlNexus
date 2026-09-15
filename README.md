# XamlNexus

[![NuGet](https://img.shields.io/nuget/v/XamlNexus)](https://www.nuget.org/packages/XamlNexus)
[![Stars](https://img.shields.io/github/stars/PaperHammer/XamlNexus)](https://github.com/PaperHammer/XamlNexus/stargazers)
[![Issues](https://img.shields.io/github/issues/PaperHammer/XamlNexus)](https://github.com/PaperHammer/XamlNexus/issues)

**English** | [简体中文](README.zh-CN.md)

A .NET command-line tool for creating Windows desktop prototypes with WinUI 3. Generate a project, add pages and components, and start building your application.

## Features

- **Two architectures:** a WinUI 3 application, or a WinUI 3 frontend with a WPF background host.
- **Application foundations:** ArcXaml, navigation, MVVM, dependency injection, configuration, and logging.
- **Optional components:** settings, SQLite, tray integration, application updates, and EditorConfig.
- **Project maintenance:** preview changes, validate projects, diagnose configuration, and upgrade generated infrastructure.

## Install

Prepare Windows, the .NET 8 SDK, and the WinUI build environment.

```powershell
dotnet tool install --global XamlNexus
xamlnexus --version
```

Update or uninstall the tool:

```powershell
dotnet tool update --global XamlNexus
dotnet tool uninstall --global XamlNexus
```

This README describes the current source; the published NuGet package may not yet include all changes.

## Quick start

```powershell
xamlnexus new MyApp
cd MyApp
xamlnexus run
```

Press **Ctrl+C** to stop before editing or running again. Edit `MyApp.MainPanel/MainPage.xaml` for the home page and `MyApp.MainPanel/ViewModels/MainViewModel.cs` for its state and logic.

Add a page, then run the application:

```powershell
xamlnexus page add Workspace --dry-run
xamlnexus page add Workspace
xamlnexus run
```

After stopping the application, add a component:

```powershell
xamlnexus recipes
xamlnexus add sqlite
xamlnexus run
```

`run` builds and starts the appropriate executable in Debug/x64/unpackaged mode. It does not watch files or provide hot reload. Use `run --dry-run` to inspect the commands or `run --no-build` to use existing output.

## Project options

Run `xamlnexus` without arguments for interactive setup, or select options explicitly:

```powershell
xamlnexus new MinimalApp --profile basic
xamlnexus new DataApp --profile basic --features settings,sqlite
xamlnexus new HybridApp --preset hybrid
```

| Option | Choices |
|---|---|
| `--preset` | `winui` (default) or `hybrid` |
| `--profile` | `standard` (default, includes settings) or `basic` (omits the full settings panel) |
| `--solution-format` | `sln` (default) or `slnx` (requires SDK 9.0.200+) |

Both profiles retain theme, localization, configuration, and logging infrastructure.

| Component | Purpose and availability |
|---|---|
| `settings` | Settings panel; included in standard, optional in basic |
| `sqlite` | Local data storage; hybrid data access runs in the host |
| `tray` | System tray integration; optional in WinUI, built into hybrid |
| `updater` | Application updates; optional in WinUI and depends on settings, built into hybrid; requires an update source |
| `editorconfig` | Editor formatting configuration |

Use `xamlnexus add settings,sqlite` to install multiple components. Already installed components are skipped. Read the generated component READMEs for configuration details.

## Maintain a project

Run these commands from the project directory, or specify `--project <directory>`:

```powershell
xamlnexus list
xamlnexus validate
xamlnexus doctor
xamlnexus update sqlite --dry-run
xamlnexus upgrade --dry-run
xamlnexus --help
```

- `update <id>` updates an installed component; `upgrade` updates scaffold infrastructure. Updating the CLI does not update existing projects.
- Generated files are editable. Component update/removal checks tracked files; scaffold upgrades report merge conflicts.
- `--dry-run` previews changes. `--json` only changes the output format and does not prevent writes.
- Installer creation, signing, and update distribution are configured separately in the generated publishing files.

For an optional shortcut in the current PowerShell session:

```powershell
Set-Alias -Name xn -Value xamlnexus
xn --help
```

## Documentation

- [Documentation index](docs/README.md)
- [Quickstart](docs/user-guide/quickstart.md)
- [Command reference](docs/user-guide/commands.md)
- [Architecture](docs/technical/architecture.md)
- [Profiles and capabilities](docs/introduction/product-model.md)
- [Pages and ViewModels](docs/user-guide/business-page.md)
- [SQLite](docs/user-guide/sqlite-recipe.md)
- [Settings and localization](docs/user-guide/localization.md)
- [Scaffold upgrades](docs/user-guide/project-upgrade.md)
- [Release policy](.github/RELEASING.md)

## Build from source

```powershell
dotnet build ./src/XamlNexus/XamlNexus.csproj
dotnet ./src/XamlNexus/bin/Debug/net8.0/XamlNexus.dll --help
```

To verify generated projects, use PowerShell 7 and the Windows/WinUI build toolchain:

```powershell
./eng/Test-GeneratedProjects.ps1
./eng/Test-GeneratedProjects.ps1 -Profile standard
```

Each invocation checks both architectures before and after adding a page and components, without launching the UI. Logs are written to `.artifacts/generated-build-*/`; generated projects are retained in the reported temporary directory.
