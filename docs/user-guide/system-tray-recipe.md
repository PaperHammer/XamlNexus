# System tray Recipe

[English](system-tray-recipe.md) | [简体中文](system-tray-recipe.zh-CN.md)

Install the optional desktop integration module into a generated pure WinUI
project:

```powershell
xamlnexus add system-tray --project <project-directory>
```

The Recipe adds `Modules/SystemTrayModule.cs` to the UI project and references
the .NET 8-compatible `H.NotifyIcon.WinUI` 2.3.2 package. At startup it creates
a native notification-area icon with:

- left-click show/hide behavior;
- a context menu with **Show / Hide** and **Exit** commands;
- an initial balloon notification;
- deterministic disposal when the application shuts down.

The generated `SystemTrayService` is registered as a singleton. Code created
through the application service provider can request it by constructor
injection and publish notifications without depending directly on the tray
control:

```csharp
public sealed class SyncJob(SystemTrayService tray) {
    public void Complete() =>
        tray.ShowNotification("Sync complete", "All local files are up to date.");
}
```

The Recipe currently supports the `winui` Preset. The hybrid Preset already
contains a WPF-owned tray process, so installing a second tray owner there is
intentionally rejected.

`xamlnexus remove system-tray` deletes the generated module when it is still
unchanged. As with other ensured package references, the NuGet reference is
retained because the project or another module may also use it.
