# Generated application module lifecycle

[English](module-lifecycle.md) | [简体中文](module-lifecycle.zh-CN.md)

Both official Presets expose the same two-stage module lifecycle in their
owning application project:

1. `ConfigureServices(IServiceCollection)` runs before the service provider is
   built.
2. `InitializeAsync(IServiceProvider, CancellationToken)` runs after the
   provider exists but before the application accepts work.

For pure WinUI, initialization completes before the first window is shown. For
the hybrid Preset, it completes in the WPF background host before the
named-pipe gRPC server starts.

The hybrid background catalog also supports `IXamlNexusGrpcModule`. Its
bindings are added after core services and before the named-pipe server starts.
The hybrid WinUI frontend has its own module catalog for client-side service
registration and initialization.

Recipe modules implement `IXamlNexusModule` in the host's `Modules` namespace
and register a factory through a module initializer:

```csharp
[ModuleInitializer]
internal static void Register() =>
    XamlNexusModuleCatalog.Register(static () => new MyModule());
```

The explicit factory reference is safe with the generated projects' trimming
settings and avoids runtime assembly scanning. Registered modules are
instantiated and executed in fully qualified type-name order, so startup order
is deterministic. A module initialization exception aborts startup; modules
must not allow the UI or IPC endpoints to operate against partially initialized
state.

The built-in SQLite Recipe uses this lifecycle to register its context factory
and finish migrations before database-backed work becomes reachable.


## Using module services from pages

New frontend templates create navigation pages through `AppObjectFactory`.
`page add` also uses this factory for the page-owned ViewModel. Constructor
arguments come from the existing application service provider; the page or
ViewModel itself does not require service registration. Parameterless types
continue to work. Missing constructor dependencies fail explicitly.

The factory always creates a new caller-owned object. It does not create a
per-page DI scope or arrange disposal of the business object; KeepAlive still
controls page reuse, and resource-owning ViewModels need explicit cleanup in
the page lifecycle. Container-owned shared services must not be disposed by
pages. Use context factories or explicit operation scopes for database work.
See the [business page guide](../user-guide/business-page.md) for SQLite injection.

## Application shutdown and owned resources

The generated WinUI application, hybrid frontend and WPF host await DI container disposal, supporting both `IDisposable` and `IAsyncDisposable` services. Register resource-owning shared services with the container. Caller-owned pages/ViewModels still need their own lifecycle cleanup; container disposal does not take ownership of them.

The main WinUI window's final shutdown handler is registered after module initialization. Existing leave confirmation and module-installed tray policies run first. A real exit request is deferred while the window remains alive and hidden, then the application exits after cleanup. Custom close policies should be registered during initialization, before this final handler, and preserve `RequestCloseAsync`.

The hybrid host first waits for actual UI exit. It then stops activation delivery and RPC, saves pending settings while services are available, closes its owned Job, awaits container disposal and removes notifications. Repeated host shutdown requests wait for cleanup completion. Once shutdown is approved, caller cancellation does not abort resource disposal.

`ApplicationCleanup` logs individual application-stage failures and continues later stages. This does not guarantee every service in a failing DI container was disposed: the DI container can stop its own disposal sequence when a service throws. Resource disposers should be reliable and complete promptly; the framework does not forcibly time out them.

Async container disposal may resume subsequent service disposal on a worker thread. Services owning XAML/WPF resources must marshal cleanup to their UI dispatcher. The tray Recipe 1.0.1 implements `DisposeAsync` and dispatches tray cleanup to the window thread. See the [.NET 8 DI implementation](https://github.com/dotnet/runtime/blob/v8.0.0/src/libraries/Microsoft.Extensions.DependencyInjection/src/ServiceLookup/ServiceProviderEngineScope.cs).

Headless tests exercise actual DI disposal, delayed async services, and application-stage failure recovery. They do not prove native close/dialog/tray behavior; generated-project builds establish compilation only.
