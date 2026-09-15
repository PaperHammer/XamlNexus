using XamlNexus.Common.Recipes;

namespace XamlNexus.Recipes.BuiltIn;

public sealed class SystemTrayRecipe : IXamlNexusRecipe, IXamlNexusRecipeUpdatePlanProvider {
    public XamlNexusRecipeDescriptor Descriptor { get; } = new() {
        Id = "system-tray",
        Version = "1.0.0",
        DisplayName = "System tray and notifications",
        Description = "Adds a WinUI system tray menu, show/hide behavior, exit command, and native balloon notifications.",
        SupportedPresets = ["winui"],
    };

    public XamlNexusRecipePlan CreatePlan(XamlNexusRecipeContext context) {
        EnsureCloseBehaviorSupport(context);
        string projectName = context.Manifest.Project.Name;
        return new XamlNexusRecipePlan {
            Changes = [XamlNexusRecipeFileChange.CreateText(
                $"{projectName}.UI/Modules/SystemTrayModule.cs",
                CreateModule(projectName)), .. RecipeReadmeResources.CreatePair("Tray", "tray", projectName)],
            ProjectOperations = [new EnsurePackageReferenceOperation(
                $"{projectName}.UI/{projectName}.UI.csproj",
                "H.NotifyIcon.WinUI",
                "2.3.2")],
        };
    }

    internal static void EnsureCloseBehaviorSupport(XamlNexusRecipeContext context) {
        string name = context.Manifest.Project.Name;
        string contract = Path.Combine(context.RootDirectory, name + ".Common", "ISystemTraySettings.cs");
        string settings = Path.Combine(context.RootDirectory, name + ".Models", "Cores", "Settings.cs");
        string settingsInterface = Path.Combine(context.RootDirectory, name + ".Models", "Cores", "Interfaces", "ISettings.cs");
        if (!File.Exists(contract) || !File.Exists(settings) || !File.Exists(settingsInterface)
            || !File.ReadAllText(settings).Contains("WindowCloseBehavior", StringComparison.Ordinal)
            || !File.ReadAllText(settingsInterface).Contains("WindowCloseBehavior", StringComparison.Ordinal))
            throw new XamlNexusRecipeException(XamlNexusRecipeErrors.MissingTrayScaffoldSupport, []);
    }

    public IReadOnlyList<XamlNexusRecipeProjectOperation> CreateUpdateOperations(
        XamlNexusRecipeContext context, string installedVersion) => [
        new EnsurePackageReferenceOperation(
            $"{context.Manifest.Project.Name}.UI/{context.Manifest.Project.Name}.UI.csproj",
            "H.NotifyIcon.WinUI", "2.3.2")
    ];

    private static string CreateModule(string projectName) => $$"""
        using System;
        using System.Runtime.CompilerServices;
        using System.Threading;
        using System.Threading.Tasks;
        using H.NotifyIcon;
        using Microsoft.Extensions.DependencyInjection;
        using Microsoft.UI.Windowing;
        using Microsoft.UI.Xaml;
        using Microsoft.UI.Xaml.Controls;
        using Microsoft.UI.Xaml.Input;
        using Microsoft.UI.Xaml.Media.Imaging;
        using {{projectName}}.Common;
        using {{projectName}}.Common.Logging;
        using {{projectName}}.Models.Cores.Interfaces;
        using {{projectName}}.Models.Datas.Interfaces;

        namespace {{projectName}}.UI.Modules;

        internal sealed class SystemTrayModule : IXamlNexusModule {
            [ModuleInitializer]
            internal static void Register() =>
                XamlNexusModuleCatalog.Register(static () => new SystemTrayModule());

            public void ConfigureServices(IServiceCollection services) {
                services.AddSingleton<SystemTrayService>();
                services.AddSingleton<ISystemTraySettings>(provider => provider.GetRequiredService<SystemTrayService>());
            }

            public async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default) {
                await services.GetRequiredService<SystemTrayService>().InitializeAsync();
            }
        }

        public sealed class SystemTrayService(MainWindow mainWindow, IUserSettingsClient userSettings) : ISystemTraySettings, IDisposable {
            private bool Chinese => userSettings.Settings.Language.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            public WindowCloseBehavior CloseBehavior => userSettings.Settings.WindowCloseBehavior;

            public async Task SetCloseBehaviorAsync(WindowCloseBehavior behavior) {
                if (!Enum.IsDefined(behavior)) throw new ArgumentOutOfRangeException(nameof(behavior));
                var previous = CloseBehavior;
                userSettings.Settings.WindowCloseBehavior = behavior;
                try { await userSettings.SaveAsync<ISettings>(); }
                catch { userSettings.Settings.WindowCloseBehavior = previous; throw; }
            }

            public async Task InitializeAsync() {
                if (_trayIcon is not null) return;
                // Persist the default for settings files created before tray support was installed.
                await SetCloseBehaviorAsync(Enum.IsDefined(CloseBehavior) ? CloseBehavior : WindowCloseBehavior.Ask);
                var toggleWindow = new XamlUICommand { Label = Chinese ? "显示 / 隐藏" : "Show / Hide" };
                toggleWindow.ExecuteRequested += (_, _) => QueueToggleWindow();
                // 让 MenuFlyout 创建并管理 Presenter，不能将 Presenter 直接用作 Window.Content。
                var menu = new MenuFlyout { AreOpenCloseAnimationsEnabled = false };
                var toggleItem = new MenuFlyoutItem {
                    Text = toggleWindow.Label, MinWidth = 200, Icon = new SymbolIcon(Symbol.OpenPane)
                };
                toggleItem.Click += (_, _) => QueueToggleWindow();
                var exitItem = new MenuFlyoutItem {
                    Text = Chinese ? "退出应用" : "Exit", MinWidth = 200, Icon = new SymbolIcon(Symbol.Cancel)
                };
                exitItem.Click += (_, _) => QueueExit();
                menu.Items.Add(toggleItem);
                menu.Items.Add(new MenuFlyoutSeparator());
                menu.Items.Add(exitItem);

                _trayIcon = new TaskbarIcon {
                    ToolTipText = "{{projectName}}",
                    IconSource = new BitmapImage(new Uri("ms-appx:///Assets/xamlnexus.ico")),
                    ContextFlyout = menu,
                    ContextMenuMode = ContextMenuMode.SecondWindow,
                    LeftClickCommand = toggleWindow,
                    NoLeftClickDelay = true,
                    Width = 0,
                    Height = 0,
                };
                // 以零尺寸接入视觉树以继承主题，不禁用命中测试，避免影响关联弹出菜单的输入状态。
                _themeRoot = mainWindow.ContentHost.AppRoot;
                _themeRoot.Children.Add(_trayIcon);
                _themeRoot.ActualThemeChanged += OnActualThemeChanged;
                _trayIcon.RequestedTheme = _themeRoot.ActualTheme;
                _trayIcon.ForceCreate(enablesEfficiencyMode: false);
                mainWindow.AppWindow.Closing += OnWindowClosing;
            }

            public void ShowNotification(string title, string message) =>
                _trayIcon?.ShowNotification(title, message);

            private void QueueToggleWindow() => mainWindow.DispatcherQueue.TryEnqueue(ToggleWindow);

            private void OnActualThemeChanged(FrameworkElement sender, object args) {
                if (_trayIcon is not null) _trayIcon.RequestedTheme = sender.ActualTheme;
            }

            private void ToggleWindow() {
                if (_disposed || _exiting) return;
                if (mainWindow.AppWindow.IsVisible) mainWindow.Hide();
                else { mainWindow.Show(); mainWindow.Activate(); }
            }

            private async void OnWindowClosing(AppWindow sender, AppWindowClosingEventArgs args) {
                if (_exiting || _disposed) return;
                // Cancel synchronously: WinUI does not wait for this async event handler.
                args.Cancel = true;
                if (_dialogOpen) return;
                if (CloseBehavior == WindowCloseBehavior.HideToTray) { mainWindow.Hide(); return; }
                if (CloseBehavior == WindowCloseBehavior.Exit) { QueueExit(); return; }

                _dialogOpen = true;
                try {
                    var remember = new CheckBox { Content = Chinese ? "记住我的选择" : "Remember my choice" };
                    var content = new StackPanel { Spacing = 12 };
                    content.Children.Add(new TextBlock {
                        Text = Chinese ? "关闭窗口后应用仍在托盘运行；退出应用将停止所有功能。" : "Closing the window keeps the app running in the tray. Exiting stops the application.",
                        TextWrapping = TextWrapping.Wrap,
                    });
                    content.Children.Add(remember);
                    _closeDialog = new ContentDialog {
                        XamlRoot = mainWindow.Content.XamlRoot,
                        RequestedTheme = ((FrameworkElement)mainWindow.Content).ActualTheme,
                        Title = Chinese ? "关闭窗口还是退出应用？" : "Close window or exit application?",
                        Content = content,
                        PrimaryButtonText = Chinese ? "关闭窗口" : "Close window",
                        SecondaryButtonText = Chinese ? "退出应用" : "Exit application",
                        CloseButtonText = Chinese ? "取消" : "Cancel",
                        DefaultButton = ContentDialogButton.Primary,
                    };
                    var result = await _closeDialog.ShowAsync();
                    if (_disposed || _exiting || result == ContentDialogResult.None) return;
                    var behavior = result == ContentDialogResult.Primary ? WindowCloseBehavior.HideToTray : WindowCloseBehavior.Exit;
                    if (remember.IsChecked == true) await SetCloseBehaviorAsync(behavior);
                    if (behavior == WindowCloseBehavior.HideToTray) mainWindow.Hide();
                    else QueueExit();
                }
                catch (Exception exception) {
                    ArcLog.GetLogger<SystemTrayService>().Error("Could not apply window close behavior", exception);
                    {{projectName}}.UIComponent.Utils.GlobalMessageUtil.ShowException(exception);
                }
                finally { _dialogOpen = false; _closeDialog = null; }
            }

            private void QueueExit() => mainWindow.DispatcherQueue.TryEnqueue(ExitApplication);

            private void ExitApplication() {
                if (_exiting || _disposed) return;
                _exiting = true;
                // Hide immediately so service and native tray cleanup cannot leave a frozen window visible.
                mainWindow.Hide();
                _closeDialog?.Hide();
                // The host performs its normal Closed cleanup and releases the DI-owned tray icon.
                mainWindow.Close();
            }

            public void Dispose() {
                if (_disposed) return;
                _disposed = true;
                mainWindow.AppWindow.Closing -= OnWindowClosing;
                _closeDialog?.Hide();
                if (_themeRoot is not null) {
                    _themeRoot.ActualThemeChanged -= OnActualThemeChanged;
                    if (_trayIcon is not null) _themeRoot.Children.Remove(_trayIcon);
                    _themeRoot = null;
                }
                _trayIcon?.Dispose();
                _trayIcon = null;
            }

            private TaskbarIcon? _trayIcon;
            private Grid? _themeRoot;
            private bool _disposed;
            private bool _exiting;
            private bool _dialogOpen;
            private ContentDialog? _closeDialog;
        }
        """;
}
