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
