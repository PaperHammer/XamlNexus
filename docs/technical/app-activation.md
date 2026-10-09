# Application activation and single instance

[English](app-activation.md) | [简体中文](app-activation.zh-CN.md)

The pure WinUI template registers a project-specific AppInstance key in Program.Main. A second launch redirects its Windows activation arguments to the existing process before constructing App, services, or windows, then exits. The existing process restores a minimized window, shows and activates the same MainWindow, and retains its pages and unsaved work.

Initial and redirected activation share one queue. Delivery starts after module InitializeAsync finishes and the main window exists. Requests run serially in arrival order. Handlers run on the UI thread; ordinary await resumes on that thread. Failed requests are logged and subsequent requests continue. The framework does not log raw launch arguments.

## Business handlers

Implement UIComponent.Activation.IAppActivationHandler in a frontend project such as MainPanel. HandleAsync receives the complete Microsoft.Windows.AppLifecycle.AppActivationArguments and a CancellationToken. For Launch activation, Data implements Windows.ApplicationModel.Activation.ILaunchActivatedEventArgs; use your own business parser for Arguments. Navigate with await NavigateAsync(...) so unsaved-work guards still apply.

Register the implementation in an IXamlNexusModule.ConfigureServices in the UI project:

~~~csharp
services.AddSingleton<IAppActivationHandler, BusinessActivationHandler>();
~~~

Handlers run in DI registration order. Use asynchronous APIs and honor cancellation. Shutdown cancels the active request and drops queued requests. The framework forwards activation data but does not interpret it as a navigation destination.

## Scope and validation

- New pure WinUI basic and standard projects use this entry point, including projects with the tray Recipe installed or removed. A tray-hidden window reuses the same MainWindow.
- Full AppActivationArguments are retained for Launch, Protocol, File, and other activation kinds. Applications must configure their own protocol and file associations; the template does not register them automatically.
- The hybrid architecture retains its WPF host, UIRunnerService, and named-pipe ownership of the frontend process. Hybrid argument forwarding needs to follow the host lifecycle.
- DISABLE_XAML_GENERATED_MAIN selects the custom STA entry point. It installs DispatcherQueueSynchronizationContext and pumps COM while redirecting activation instead of blocking the STA on the SDK task.
- Existing generated projects do not automatically receive a replacement App.xaml.cs. Review scaffold upgrade differences and merge application-specific startup logic.

Queue tests cover activation during initialization, serial delivery, failure recovery, cancellation, and dispatcher rejection. Generated-project builds verify the entry point and SDK types; window restoration and cross-process delivery require separate Windows runtime verification.

Reference: [Microsoft single-instance guidance](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/applifecycle/applifecycle-single-instance).

## Headless acceptance

~~~powershell
pwsh -NoProfile -File eng/Test-AppInstance.ps1 -Repetitions 2
~~~

The script builds an independent console probe using AppInstance and a COM wait pump. It creates no WinUI windows and sends no mouse or keyboard input. It reads the SDK version from the pure WinUI template by default. A common parent process launches both children with the same user, elevation, and working directory. Cases cover identical paths and both directions of mixed/lowercase and mixed/uppercase paths. Each case checks the original PID, argument forwarding including spaces, and the versions and SHA-256 hashes of loaded SDK binaries.

Results are written to .artifacts/app-instance-*/Results.json. Identity separation or redirection failure produces a nonzero exit code. The script owns and cleans up its probe processes. Window restoration, foreground focus, and tray behavior require separate verification.

On 2026-10-09, controlled tests using SDK 1.8.260317003 passed 2/2 identical-path cases but observed identity separation in all 8 case-variation cases. User, elevation, working directory, and SDK binary hashes matched in every pair. The runtime DLL hashes also matched those in the earlier generated UI project. The current integration has not passed case-variation acceptance. A normalization fix in SDK main does not establish that a particular release package includes it.
