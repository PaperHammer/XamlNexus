# Application activation and single instance

[English](app-activation.md) | [简体中文](app-activation.zh-CN.md)

The pure WinUI template registers a project-specific AppInstance key in Program.Main. A second launch redirects its Windows activation arguments to the existing process before constructing App, services, or windows, then exits. The existing process restores a minimized window, shows and activates the same MainWindow, and retains its current page for ordinary launches. Business destinations navigate only after the leave guard approves.

Initial and redirected activation share one queue. Delivery starts after module InitializeAsync finishes and the main window exists. Requests run serially in arrival order. Handlers run on the UI thread; ordinary await resumes on that thread. Failed requests are logged and subsequent requests continue. The framework does not log raw launch arguments.

## Business navigation

Launch a compiled app from PowerShell 7:

~~~powershell
# Orders is registered by page add Orders --kind form
& .\MyApp.UI.exe --route Orders --data '{"id":"42"}'
# Initial and subsequent hybrid launches use the background host
& .\MyApp.exe --route Orders --data '{"id":"42"}'
~~~

`--route` resolves an existing, case-sensitive NavigationRegistry route. Optional `--data` must be a JSON object; omitting it uses an empty object. Windows quoting preserves spaces, quotes, backslashes, and Unicode. Parsing uses each activation event's Arguments, including redirected launches, rather than the primary process's original command line. Other arguments are retained; these two options are reserved by the framework.

The flow is ActivationQueue → BusinessActivationRequest → NavigationRegistry.Resolve → MainWindow.NavigateActivationAsync → NaviContent.NavigateAsync, after module initialization. Unknown routes, duplicate or missing option values, and invalid JSON show an error without navigating. Cancelled leave confirmation or failed saves preserve the page, payload, and sidebar selection. Selection updates only after success. Opening another record on the same page also requires leave approval.

Business data is stored as a JsonElement under BusinessActivationRequest.PayloadKey instead of being expanded into framework keys. Generated details and form pages read a string ID with `payload.GetBusinessString("id")`, which also supports the legacy in-app string `"id"` payload. Custom pages can read the complete request:

~~~csharp
using System.Text.Json;
using MyApp.UIComponent.Activation;

if (payload is not null &&
    payload.TryGet<BusinessActivationRequest>(BusinessActivationRequest.PayloadKey, out var request)) {
    JsonElement data = request.Data;
    // Validate business fields and call your data source.
}
~~~

Omitting ID uses the sample record in details pages or creates a new record in forms. Cached activation IDs are not reused. Incoming payload values take precedence over cached values, and explicit in-app navigation clears the cached external activation marker. List pages can consume request.Data for their own search and filter contracts.

Pure WinUI supports initial and redirected launches. Hybrid apps forward the host's initial arguments once to its UI child using ProcessStartInfo.ArgumentList, then navigate after UI initialization. UI restart does not replay the startup destination. Subsequent host launches forward their argument array through a separate named pipe before creating services or windows, then exit. The existing host starts processing after its modules are ready, reuses or creates its UI child, and delivers through the owned stdin/stdout channel. Ordinary launches restore the window without changing its record.

Gallery previews route/JSON navigation with these actual template sources and its SQLite business models. Gallery itself retains a separate Mutex entry point; this preview does not validate SDK delivery or hybrid-host shutdown. See the [Gallery acceptance steps](../../samples/XamlNexus.Gallery/README.md#业务流程验收--business-workflow-acceptance).

## Hybrid delivery contract

Mutex still determines the unique host. HybridActivationPipe transports complete argument arrays without changing existing numeric gRPC commands. The listener opens before expensive module initialization and processing starts after host readiness. UIHostControl serializes activation and close requests; incoming requests can be queued while a user decides whether to save. Cancellation and failed saves retain the current page through MainWindow's existing leave guard.

Each queue holds up to 64 requests. A request supports up to 128 arguments and 64 KiB of UTF-8 JSON. A host receipt means acceptance into its in-memory queue; a UI receipt means acceptance into its control queue. Neither confirms navigation success. Host connection/receipt waits at most 15 seconds and UI delivery waits at most 30 seconds; user confirmation and saving have no deadline. Timeouts may occur after acceptance, so requests are never automatically retried or replayed. Host exit drops pending work; delivery is not durable or exactly once across exits. UI exit before receipt fails that delivery; a later new activation can create another UI.

CurrentUserOnly restricts the pipe to the same Windows user and elevation level; see [Microsoft PipeOptions documentation](https://learn.microsoft.com/dotnet/api/system.io.pipes.pipeoptions). Permission mismatch, owner exit, initialization failure, full queues and timeouts never create a second host. Sender failures return a nonzero exit code. Failures after host receipt are logged by the host; business navigation errors appear in the UI. Launch MyApp.exe, rather than UI.exe, to use host redirection. Development launches with --xamlnexus-run still refuse to join an existing host.

## Business handlers

Implement UIComponent.Activation.IAppActivationHandler in a frontend project such as MainPanel. HandleAsync receives the complete Microsoft.Windows.AppLifecycle.AppActivationArguments and a CancellationToken. For Launch activation, Data implements Windows.ApplicationModel.Activation.ILaunchActivatedEventArgs; use your own business parser for Arguments. Navigate with await NavigateAsync(...) so unsaved-work guards still apply.

Register the implementation in an IXamlNexusModule.ConfigureServices in the UI project:

~~~csharp
services.AddSingleton<IAppActivationHandler, BusinessActivationHandler>();
~~~

Handlers run in DI registration order. Use asynchronous APIs and honor cancellation. Shutdown cancels the active request and drops queued requests. Ordinary Launch and Protocol/File events still reach these handlers. Launch events containing reserved business options use the built-in router and do not subsequently invoke other handlers, whether successful, cancelled, or invalid. This prevents duplicate navigation and guard bypass.

## Scope and validation

- New pure WinUI basic and standard projects use this entry point, including projects with the tray Recipe installed or removed. A tray-hidden window reuses the same MainWindow.
- Full AppActivationArguments are retained for Launch, Protocol, File, and other activation kinds. Applications must configure their own protocol and file associations; the template does not register them automatically.
- The hybrid architecture retains its WPF host, UIRunnerService, and named-pipe ownership of the frontend process. Initial and subsequent business arguments follow this host lifecycle; the frontend remains a managed child.
- DISABLE_XAML_GENERATED_MAIN selects the custom STA entry point. It installs DispatcherQueueSynchronizationContext and pumps COM while redirecting activation instead of blocking the STA on the SDK task.
- Existing generated projects do not automatically receive a replacement App.xaml.cs. Review scaffold upgrade differences and merge application-specific startup logic.

Queue tests cover activation during initialization, serial delivery, failure recovery, cancellation, and dispatcher rejection. Generated-project builds verify the entry point and SDK types; window restoration and cross-process delivery require separate Windows runtime verification.

Reference: [Microsoft single-instance guidance](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/applifecycle/applifecycle-single-instance).

## Headless acceptance

~~~powershell
pwsh -NoProfile -File eng/Test-AppInstance.ps1 -Repetitions 2
~~~

The script builds an independent console probe using AppInstance and a COM wait pump. It creates no WinUI windows and sends no mouse or keyboard input. It reads the SDK version from the pure WinUI template by default. A common parent process launches both children with the same user, elevation, and working directory. Cases cover identical paths and both directions of mixed/lowercase and mixed/uppercase paths. Each case checks the original PID, argument forwarding including spaces, and the versions and SHA-256 hashes of loaded SDK binaries.

Results are written to .artifacts/app-instance-*/Results.json. Identity separation or redirection failure produces a nonzero exit code. The script owns and cleans up its probe processes. The path matrix also checks registered business-route parsing and JSON with Unicode, spaces, quotes, and backslashes after real SDK redirection. The probe uses a headless navigation callback; template tests separately exercise the actual ArcNavigateAsync guards, same-page updates, and cached payload handling. Window restoration, foreground focus, and tray behavior require separate verification.

On 2026-10-09, controlled tests using SDK 1.8.260317003 passed 2/2 identical-path cases but observed identity separation in all 8 case-variation cases. User, elevation, working directory, and SDK binary hashes matched in every pair. The runtime DLL hashes also matched those in the earlier generated UI project. This is the pre-fix result. A normalization fix in SDK main does not establish that a particular release package includes it.

### Canonical launch and bounded redirection

The 2026-10-12 compatibility fix retains SDK 1.8.260317003. Before accessing AppInstance, unpackaged apps resolve the executable path through its file handle. If its spelling differs, a canonical launch preserves arguments, working directory, and child exit code. The forwarding launcher constructs no App, service container, or window. File-system canonical paths preserve case-sensitive directory semantics; MSIX skips this relaunch to retain Windows activation data.

Activation is subscribed before publishing the instance key. Redirection pumps the STA for at most 15 seconds and watches owner exit. Failure reports an error rather than starting another UI. Registration retries a bounded number of times when the previous owner exits between registration and lookup.

The probe compiles the actual CanonicalAppLaunch, ActivationRedirector, and ActivationQueue template sources. Its default scenarios include path casing, concurrent start, activation during initialization, owner exit, and the initial business route. Use -RawSdk to reproduce the SDK without the path compatibility layer; nonzero exit is expected on this release. Window restoration and packaged runtime behavior still require separate verification.

### Hybrid forwarding and close probes

~~~powershell
dotnet run --project eng/diagnostics/HybridActivationProbe/HybridActivationProbe.csproj -c Release
dotnet run --project eng/diagnostics/HybridCloseProbe/HybridCloseProbe.csproj -c Release
~~~

HybridActivationProbe owns its secondary launchers and simulated UI processes under the same user/elevation. It compiles the actual template pipe, UI control channel, receipt coordinator, business parser and router. Checks cover initialization queuing, complete JSON, cancellation preserving modeled draft state, ordinary launches, eight concurrent deliveries, UI recreation and bounded failure after host exit. Unit tests additionally cover full queues, malformed frames, stale/PID-mismatched receipts, UI timeout and close serialization while navigation awaits a decision. The probe does not execute the real WPF App, UIRunnerService, native windows or ContentDialog. Generated-project builds validate that wiring compiles; actual restoration, focus, dialogs and MSIX runtime require separate acceptance.
