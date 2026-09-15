# XamlNexus quickstart

[English](quickstart.md) | [简体中文](quickstart.zh-CN.md)

This guide starts with creating and running a project, then shows how to edit the home page and add pages and components. Standard projects include settings, themes, localization, navigation, MVVM, DI, configuration, and logging.

## 1. Prerequisites and installation

For individual options, see the [command reference](commands.md).

You need Windows, the .NET 8 SDK, and a Visual Studio 2022 or Build Tools environment capable of compiling WinUI 3.

```powershell
dotnet --info
dotnet tool install --global XamlNexus
xamlnexus --version
xamlnexus --help
```

This guide describes current source. If your installed version lacks a command shown here, update with `dotnet tool update --global XamlNexus`. Unreleased features require a source build.

From the repository root, run `dotnet build src/XamlNexus/XamlNexus.csproj`, then define an absolute-path PowerShell entry point that continues to work after changing directories:

```powershell
$XamlNexusCli = (Resolve-Path ./src/XamlNexus/bin/Debug/net8.0/XamlNexus.dll).Path
function xamlnexus { & dotnet $XamlNexusCli @args }
```

### Use the short name `xn`

Set a PowerShell alias to use `xn` wherever this guide uses `xamlnexus`:

```powershell
Set-Alias -Name xn -Value xamlnexus
xn --help
xn new MyApp
```

The alias applies to the current PowerShell session. To use it in new terminals, add `Set-Alias -Name xn -Value xamlnexus` to your PowerShell profile, `$PROFILE`. It also works with the source-build function above.

`xn` is a shell alias. The package name remains `XamlNexus`, so installation, update, and uninstall commands stay the same.

## 2. Create and run

Run from the directory where you want the application:

```powershell
xamlnexus new MyApp
cd MyApp
xamlnexus run
```

The default is pure WinUI with the standard profile. Running `xamlnexus` alone opens interactive architecture, profile, and component selection.

`run` locates, builds, and launches the startup project in **Debug / x64 / unpackaged** mode. Hybrid starts the WPF host, which starts the WinUI frontend. Stop with Ctrl+C before subsequent changes or commands; rerunning rebuilds the application. Watch, hot reload, Release running, and MSIX running are not supported.

If a build fails, read its output and run `xamlnexus doctor`. SDK checks use `dotnet --version` inside the project and respect its or an ancestor's `global.json`. A missing selected SDK or one below .NET 8 is an error even if another newer SDK is installed. `run --dry-run` shows the build plan; `--no-build` runs existing artifacts without compiling edits.

After the parent process exits, the CLI waits up to two seconds to drain output; a child retaining an output pipe will not keep it waiting indefinitely. If a creation directory is occupied, the tool atomically reserves a suffixed directory. Use the actual path in the completion output.

## 3. Edit the home page

| What to edit | MyApp location |
|---|---|
| Home layout | `MyApp.MainPanel/MainPage.xaml` |
| Home state and interaction | `MyApp.MainPanel/ViewModels/MainViewModel.cs` |
| Page initialization and control events | `MyApp.MainPanel/MainPage.xaml.cs` |
| Window layout | `MyApp.UI/MainWindow.xaml` |
| Built-in navigation | `MyApp.UI/Navigation/BuiltInNavigation.cs` |
| Module service registration | `MyApp.UI/Modules/`; hybrid backend: `MyApp/Modules/` |

At `<!-- Add page content here. -->`, insert:

```xml
<TextBlock Text="My first prototype" />
```

Run again to see the result. The home ViewModel is factory-created and can receive module-registered services through its constructor without changing startup. The standard settings panel can switch theme and language.

## 4. Add an ordinary page

After stopping the app, run inside MyApp:

```powershell
xamlnexus page add Workspace --dry-run
xamlnexus page add Workspace
xamlnexus run
```

Current templates generate and register:

- `MyApp.MainPanel/WorkspacePage.xaml`
- `MyApp.MainPanel/WorkspacePage.xaml.cs`
- `MyApp.MainPanel/ViewModels/WorkspaceViewModel.cs`
- `MyApp.UI/Navigation/WorkspaceNavigation.cs`

These are ordinary page scaffolds, not query, list, or form business implementations. Edit the Page and ViewModel directly. Custom windows must consume the shared registry; `--no-navigation` generates only the Page and ViewModel. See [pages](business-page.md) and [navigation](../technical/navigation.md).

## 5. Add capabilities

Stop the app, inspect available components, and select one:

```powershell
xamlnexus recipes
xamlnexus add sqlite --dry-run
xamlnexus add sqlite
xamlnexus run
```

SQLite adds a Data project and initialization. You implement page data operations. Hybrid data belongs to the backend; the Recipe generates an AppState RPC example, but does not generate your custom business protocols.

| Capability | Command | Boundary |
|---|---|---|
| Settings panel | `xamlnexus add settings` | For basic; already included in standard |
| SQLite | `xamlnexus add sqlite` | Both architectures |
| Tray and notifications | `xamlnexus add tray` | Optional in pure WinUI; built into hybrid |
| App updates | `xamlnexus add updater` | Optional in pure WinUI, adds settings dependency; configure your own source. Built into hybrid |
| Editor configuration | `xamlnexus add editorconfig` | Both architectures |

Existing projects support batch installation, for example `xamlnexus add settings,sqlite` in a basic project. Dependencies are handled automatically; do not explicitly re-add installed components. See [SQLite](sqlite-recipe.md), [tray](system-tray-recipe.md), and [updates](app-update-recipe.md).

## 6. Other starting points

These create independent projects rather than changing MyApp's preset:

```powershell
# Basic: core mechanisms without the full settings panel
xamlnexus new MinimalApp --profile basic
# Compose components during creation
xamlnexus new DataApp --profile basic --features settings,sqlite
# Hybrid: WinUI frontend with a WPF background host
xamlnexus new HybridApp --preset hybrid --profile standard
```

Preset chooses architecture; profile chooses initial capabilities. Basic retains themes, localization, configuration, logging, and existing hybrid backend capabilities. Profiles do not prevent later additions/removals. See the [product model](../introduction/product-model.md).

## SLNX and CLI language resources

```powershell
xamlnexus new XmlApp --solution-format slnx
```

SLN remains the default; interactive creation can also select the format. SLNX requires the selected SDK to be at least 9.0.200; generated applications still target .NET 8. See [Microsoft's SLNX announcement](https://devblogs.microsoft.com/dotnet/introducing-slnx-support-dotnet-cli/). Component add/remove, manifest validation, and upgrades support SLNX. Upgrades preserve format, without automatic conversion; manually converting a solution does not update the manifest or baseline.

Existing CLI localization and wizard strings live in `src/XamlNexus.Common/Resources/Strings.resx` (English default) and `Strings.zh-CN.resx`, loaded through ResourceManager. Keep keys aligned. `GetText` returns raw text; `GetI18n` also escapes Spectre markup. Generated application `.resw` resources remain separate. English constants in CLI help and diagnostics are not all localized yet.

## 7. Maintenance and distribution

Inside the generated project:

```powershell
xamlnexus list
xamlnexus validate
xamlnexus update sqlite --dry-run
xamlnexus upgrade --dry-run
```

From outside, add `--project <project-directory>`, for example `xamlnexus run --project ./MyApp`. `update sqlite` requires an installed SQLite component; see [upgrades](project-upgrade.md).

Development running and publishing are separate. To distribute, configure your own versions, signing, and update source in the generated workflows and scripts. [SqliteShowcase](../../samples/SqliteShowcase/README.md) demonstrates composition and distribution-directory builds.
