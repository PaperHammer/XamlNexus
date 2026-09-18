# Product model and capability boundaries

[English](product-model.md) | [简体中文](product-model.zh-CN.md)

XamlNexus generates a runnable Windows standard client ready for page development. You can choose a basic profile during creation and add components as needed.

## Creation options

- `--preset winui|hybrid`: architecture; pure WinUI is the default. Hybrid combines a WinUI frontend with a WPF background host.
- `--profile standard|basic`: initial capabilities; defaults to standard.
- `--features settings,sqlite`: compose components during creation using Recipe dependencies and installation.
- Run `xamlnexus` without arguments to select architecture, profile, and components interactively.
- `--solution-format sln|slnx`: defaults to sln; SLNX requires the selected SDK to be 9.0.200 or later.

Basic removes the full settings panel while retaining startup, navigation, MVVM, DI, configuration, logging, themes, and localization infrastructure. Standard includes settings. Both profiles reuse existing templates rather than maintaining duplicate template trees. Hybrid retains its existing tray, updater, and host capabilities; basic does not make all of them optional.

## Capabilities

| Capability | Basic | Standard (default) | Current boundary |
|---|---|---|---|
| Startup, windows, navigation, single instance | Included | Included | Not split into individual Recipes |
| MVVM, DI, module lifecycle | Included | Included | Pages own ViewModels; the container owns shared services |
| Configuration, logs, exception handling | Included | Included | Existing recovery and error handling retained |
| Theme and localization infrastructure | Included | Included | Settings provides the configuration UI |
| Full settings panel | Optional | Included | `add settings` |
| SQLite | Optional | Optional | Both architectures; hybrid frontend accesses host data through RPC |
| Tray, menus, notifications | Architecture-dependent | Architecture-dependent | Pure WinUI: `add tray`; hybrid: built in |
| App updates | Architecture-dependent | Architecture-dependent | Pure WinUI: `add updater`, depending on settings; hybrid: built in |
| Logon startup | Core implementation retained | Configurable in settings | No standalone Recipe; registration is not enabled by default |
| Editor configuration | Optional | Optional | `add editorconfig` |
| Publishing, installers, signing | Scripts included | Scripts included | Maintainer configuration and execution; separate from development running |
| Ordinary page generation | `page add` | `page add` | Page, ViewModel, navigation registration; blank/list kinds, no form kind |

Actual installation state is shown by `xamlnexus list` and `xamlnexus.json` modules. Profile records creation intent; later removals are not undone merely because of the initial profile. Legacy manifests without a profile are treated as standard; preset retains its architecture meaning.

## First development flow

Follow the [quickstart](../user-guide/quickstart.md):

```text
new → run → edit home page → page add → add capabilities → run
```

`run` handles Debug/x64/unpackaged builds and startup, Ctrl+C, exit codes, and duplicate-run protection for the same project. It does not provide watch, hot reload, or MSIX running.

Business pages are not recorded as removable Recipes. Both frontends share the [navigation contract](../technical/navigation.md) and page factory; adding an ordinary page does not require rewriting App.xaml.cs or MainWindow. See [page development](../user-guide/business-page.md) and [module lifecycle](../technical/module-lifecycle.md).

## Component boundaries

Source and configuration from both initial generation and `add` belong to the user, including version properties and publishing scripts. Hashes and baselines protect changes during updates, upgrades, and removal; they are not editing permissions. Content differences do not block running, page generation, or component installation. Missing required files still need attention, and compilation reports actual build errors.

Unsafe automatic changes must report conflicts without overwriting user content or requiring hash recalculation to continue development. Capabilities suit components when they have distinct uses, dependencies, or configuration costs and clear installation/removal boundaries. Existing projects support batch add, dependency/conflict checks, previews, updates, removal, and rollback. Creation reuses included capabilities; explicitly adding an already installed module to an existing project reports an error.

App updates still use the settings UI and shared contracts; basic projects automatically receive settings when installing updater. Themes and localization remain core mechanisms, without standalone Recipes or a generic settings-extension framework. Hybrid SQLite access belongs to the host; ordinary page generation does not create custom business RPCs.

## Serialization compatibility

Unused MessagePack references and MVVM attributes have been removed from both templates and the Showcase. JSON storage and gRPC/Protobuf wire formats are unchanged.
Existing applications that use MessagePack themselves need a separate assessment before removing the dependency.
