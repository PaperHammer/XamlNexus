# System tray

[English](tray.README.md) | [简体中文](tray.README.zh-CN.md)

This Recipe adds `{{ProjectName}}.UI/Modules/SystemTrayModule.cs` and H.NotifyIcon.WinUI to a pure WinUI project. The module is registered automatically at startup; no App.xaml.cs edits are required.

The tray icon supports showing or hiding the window and a WinUI Fluent menu with an Exit command. Closing the window offers Hide to tray, Exit application, or Cancel. Remembering the choice saves `WindowCloseBehavior` in the normal user settings JSON:

- `Ask`: ask whenever the window is closed (default).
- `HideToTray`: hide the window and keep the application running.
- `Exit`: exit the application.

With the settings Recipe installed, the settings panel exposes this choice. Either installation order is supported. Without the panel, the close dialog still works and can remember the choice. The tray menu Exit command always exits.

Stop the application before adding or removing the Recipe, then build and run again. Removal: `xamlnexus remove tray`. Modified managed files are protected by transaction checks. Hybrid already has its own built-in tray integration; this Recipe targets pure WinUI.
