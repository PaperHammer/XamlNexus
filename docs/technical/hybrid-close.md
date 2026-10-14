# Guarded hybrid-host shutdown

[English](hybrid-close.md) | [简体中文](hybrid-close.zh-CN.md)

UIRunnerService.CloseUIAsync, RestartUIAsync, and App.RequestShutDownAsync use guarded UI close. Normal exit, tray exit, host-window close, and gRPC close/restart no longer terminate the UI because a 500 ms or 3.5 s wait elapsed.

The host sends a unique close ID through its owned child's stdin. After modules and the main window are ready, the UI dispatches ArcWindow.RequestCloseAsync on its UI queue. Startup requests remain buffered. Approval is confirmed by actual process exit; cancellation, a concurrent leave request, or save failure returns a matching rejection through stdout. Ordinary output is not a control response. IDs must match both the request and the owned process, so stale responses cannot affect new requests. Existing gRPC contracts and message values are retained.

Host exit waits for UI close before closing Jobs, disposing services, or stopping gRPC. The tray and background services remain alive during confirmation. New UI launches are blocked during host shutdown and re-enabled on cancellation. Restart also waits for exit and does not create a replacement after cancellation. UI teardown cancels its RPC, but this does not cancel an accepted host close operation.

Saving, user confirmation, and an unresponsive UI do not cause automatic termination. Callers can cancel waiting through CancellationToken; this does not approve destruction. Host crashes, OS termination, and explicit resource disposal may still terminate Job processes and require an application recovery strategy.

## Headless acceptance

~~~powershell
dotnet run --project eng/diagnostics/HybridCloseProbe/HybridCloseProbe.csproj -c Release
~~~

The probe compiles the actual UIHostControl and UICloseCoordinator sources and owns console child processes. It verifies cancellation/retry, saving beyond the old 3.5 s threshold, save failure, and cancellation of an unresponsive wait. Cleanup only terminates the probe's own children. Unit tests also cover duplicate requests, stale responses, and ordinary output.

These checks validate protocol/coordinator behavior; native WinUI dialogs, focus, and tray visuals require separate verification. Generated-project builds validate host and UI integration.
