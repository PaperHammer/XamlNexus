# Guarded hybrid-host shutdown

[English](hybrid-close.md) | [简体中文](hybrid-close.zh-CN.md)

UIRunnerService.CloseUIAsync, RestartUIAsync, and App.RequestShutDownAsync use guarded UI close. Normal exit, tray exit, host-window close, and gRPC close/restart no longer terminate the UI because a 500 ms or 3.5 s wait elapsed.

The host sends a unique close ID through its owned child's stdin. After modules and the main window are ready, the UI dispatches ArcWindow.RequestCloseAsync on its UI queue. Startup requests remain buffered. Approval is confirmed by actual process exit; cancellation, a concurrent leave request, or save failure returns a matching rejection through stdout. Ordinary output is not a control response. IDs must match both the request and the owned process, so stale responses cannot affect new requests. Existing gRPC contracts and message values are retained.

Host exit waits for actual UI process exit, then stops activation/RPC, saves settings before disposing their services, closes the owned Job, awaits async DI disposal and removes notifications. Application cleanup errors are logged per stage; later stages still run. Repeated shutdown requests wait for cleanup completion. See [application resource cleanup](module-lifecycle.md#application-shutdown-and-owned-resources). The tray and background services remain alive during confirmation. New UI launches are blocked during host shutdown and re-enabled on cancellation. Restart also waits for exit and does not create a replacement after cancellation. UI teardown cancels its RPC, but this does not cancel an accepted host close operation.

Saving, user confirmation, and an unresponsive UI do not cause automatic termination. Callers can cancel waiting through CancellationToken; this does not approve destruction. Host crashes, OS termination, and explicit resource disposal may still terminate Job processes and require an application recovery strategy.

## Headless acceptance

~~~powershell
dotnet run --project eng/diagnostics/HybridCloseProbe/HybridCloseProbe.csproj -c Release
~~~

The probe compiles the actual UIHostControl and UICloseCoordinator sources and owns console child processes. It verifies cancellation/retry, saving beyond the old 3.5 s threshold, save failure, and cancellation of an unresponsive wait. Cleanup only terminates the probe's own children. Unit tests also cover duplicate requests, stale responses, and ordinary output.

These checks validate protocol/coordinator behavior; native WinUI dialogs, focus, and tray visuals require separate verification. Generated-project builds validate host and UI integration.

Activation and close share the UI control queue. A close request arriving during business navigation waits for that navigation to finish; receiving new activation receipts does not bypass leave guards. See the [hybrid delivery contract](app-activation.md#hybrid-delivery-contract).
