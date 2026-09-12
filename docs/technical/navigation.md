# Unified navigation contract

[English](navigation.md) | [简体中文](navigation.zh-CN.md)

Pure WinUI and hybrid WinUI frontends share the same contract in the generated `UIComponent/Navigation` directory. Navigation does not involve the hybrid backend or RPC.

## Register a page

`xamlnexus page add Orders` generates the Page, ViewModel, and `UI/Navigation/OrdersNavigation.cs`:

```csharp
using System.Runtime.CompilerServices;
using MyApp.UIComponent.Navigation;

namespace MyApp.UI.Navigation;

internal static class OrdersNavigation {
    [ModuleInitializer]
    internal static void Register() => NavigationRegistry.Default.Register(new NavigationEntry(
        "Orders", typeof(MyApp.MainPanel.OrdersPage), "Orders", Order: 10));
}
```

The registration file is compiled into the UI assembly and runs automatically at startup. Rebuild and run after adding it. The menu reads a startup registration snapshot; runtime page installation/removal is not supported. Pages inherit ArcPage. AppObjectFactory supports parameterless constructors and constructor injection of registered services; ArcNavigationContentView still manages page creation and lifecycle.

`NavigationEntry` describes route, page type, title, optional resource key, icon glyph, order, and footer placement. Routes are case-sensitive and unique; duplicates throw instead of replacing pages. Menus sort by Order then Route. Display titles are independent of routes. Built-in `home` and `settings` routes reside in `UI/Navigation/BuiltInNavigation.cs`.

For translated titles, add the same string key to each UIComponent language's Resources.resw and set TitleResourceKey. The standard menu subscribes to LanguageUtil.LanguageUpdated to refresh titles and tooltips and unsubscribes when the window closes.

## Standard and custom windows

`INavigationRegistry` exposes Register, Entries, and Resolve. NavigationRegistry is the default implementation, with `NavigationRegistry.Default` shared in the process. MainWindow uses NavigationMenu to create items, stores routes in NavigationViewItem.Tag, resolves a selected route to its page type, and calls NaviContent.Navigate(pageType). There is no per-page switch to maintain in the window.

A custom window can reuse the adapter:

```csharp
var menu = new NavigationMenu(myNavigationView, NavigationRegistry.Default);
menu.Select("home");
// Read SelectedItemContainer.Tag on selection and call the content control's Navigate.
// Call menu.Dispose() when the window closes.
```

Alternatively, enumerate Entries to render your own controls and use Resolve for routes. The contract does not prescribe a window name, XAML layout, or menu control. The custom host owns selection handling, title refresh, and page display.

## Generation and migration boundaries

`page add` checks for `UIComponent/Navigation/INavigationRegistry.cs`. If present, it generates a standalone registration file without reading or rewriting MainWindow. A custom host must consume the registry; CLI `automatic` means registration code was generated, not that the host's usage was verified.

Older projects without the interface receive only the three page-related files and a manual integration notice. `--no-navigation` also generates only those three files. Older windows can keep their existing navigation or migrate explicitly; page add does not rewrite existing windows or the example project. Generated business files are user-maintained, not removable Recipes. Remove the registration file when deleting a page.

Dry runs, file conflict checks, and transactional rollback still apply. Module DI lifecycle and navigation registration govern services and pages separately. Adding navigation requires no database and does not change ViewModel ownership.
