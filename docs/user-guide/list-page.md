# List page template

English | [简体中文](list-page.zh-CN.md)

```powershell
xamlnexus page add Projects --kind list --dry-run
xamlnexus page add Projects --kind list
xamlnexus run
```

Available for winui/hybrid and basic/standard. Omitting `--kind`, or using `--kind blank`, retains ordinary page generation. Supports `--project`, `--no-navigation`, `--dry-run`, and `--json`. Projects without the navigation contract receive a manual integration notice. Any target-file collision rejects the entire operation before writing.

## Generated files

For MyApp and Projects:

- `MyApp.MainPanel/ProjectsPage.xaml`: search, refresh, progress, error feedback, list and empty state.
- `MyApp.MainPanel/ProjectsPage.xaml.cs`: page lifecycle and language subscription cleanup.
- `MyApp.MainPanel/ViewModels/ProjectsViewModel.cs`: asynchronous loading, filtering, refresh command and cancellation.
- `MyApp.MainPanel/Services/ProjectsDataSource.cs`: item model, data source interface and runnable in-memory sample.
- `MyApp.UI/Navigation/ProjectsNavigation.cs`: navigation registration when supported and enabled.

These are user-owned business files, not a removable Recipe. Generation does not install SQLite or change service registrations.

## Replace the sample data

Implement `IProjectsDataSource.LoadAsync(CancellationToken)`, returning `IReadOnlyList<ProjectsItem>`. Supply the implementation where the page creates its ViewModel:

```csharp
public ProjectsViewModel ViewModel { get; } = new(new MyProjectsDataSource());
```

Alternatively obtain your service from the existing DI container and pass it to the ViewModel. The default constructor uses sample data without additional registrations. Hybrid frontends should call a host client rather than open the host database directly.

Pass cancellation tokens through asynchronous operations and avoid blocking UI I/O. Invoke lifecycle methods, search and commands on the UI thread; normal awaits preserve its synchronization context.

## Behavior

Generated pages do not have `[KeepAlive]` by default: leaving destroys the page, and returning creates a new page and ViewModel with a fresh query and load. Successful data and queries are retained only when the same ViewModel is reused, for example by keeping the page alive; refresh reads again. Duplicate refreshes are suppressed. Failures retain previous data, display an error and allow retry. Search matches title or description locally, case-insensitively; there is no server-side paging.

Pre-leave, destruction and unloading cancel loading, including navigation that keeps pages alive. Late results or errors from services that ignore cancellation cannot overwrite a newer visit's request.

Search, refresh, empty-state and error labels support English and Chinese and follow application language changes. They live in the generated ViewModel and can be replaced with application resource keys. Page/navigation titles default to the page name; sample records are demonstration content. Maintain your business translations separately.

This template supplies list interaction basics, not database CRUD, editing, deletion, pagination or business RPC generation.

## Retain page state

```csharp
using MyApp.UIComponent.Attributes;

[KeepAlive]
public sealed partial class ProjectsPage : ArcPage
{
    // Retains the page instance and its ViewModel.
}
```

Add the attribute to the existing generated class declaration, rather than declaring it twice. KeepAlive retains state within the current process; restart recovery needs persistence. Leaving still cancels loading and unsubscribes language events.

`samples/XamlNexus.Gallery` provides a Gallery-style comparison of ordinary and KeepAlive list pages, including instance IDs, search state, simulated slow requests, failures and retries.
