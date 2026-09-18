using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using H.NotifyIcon;
using XamlNexus.Gallery.Models.Datas.Interfaces;
using XamlNexus.Gallery.UIComponent.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;

namespace XamlNexus.Gallery.UI.Modules;

internal sealed class SystemTrayModule : IXamlNexusModule {
    [ModuleInitializer]
    internal static void Register() =>
        XamlNexusModuleCatalog.Register(static () => new SystemTrayModule());

    public void ConfigureServices(IServiceCollection services) =>
        services.AddSingleton<SystemTrayService>()
            .AddSingleton<INotificationService>(provider => provider.GetRequiredService<SystemTrayService>());

    public Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default) {
        services.GetRequiredService<SystemTrayService>().Initialize();
        return Task.CompletedTask;
    }
}

public sealed class SystemTrayService(MainWindow mainWindow) : IDisposable, INotificationService {
    public void Initialize() {
        if (_trayIcon is not null) return;
        var toggleWindow = new XamlUICommand { Label = "Show / Hide" };
        toggleWindow.ExecuteRequested += (_, _) => ToggleWindow();
        var exit = new XamlUICommand { Label = "Exit" };
        exit.ExecuteRequested += (_, _) => App.ShutDown();
        var menu = new MenuFlyout();
        _toggleItem = new MenuFlyoutItem { Command = toggleWindow };
        menu.Items.Add(_toggleItem);
        menu.Items.Add(new MenuFlyoutSeparator());
        _exitItem = new MenuFlyoutItem { Command = exit };
        menu.Items.Add(_exitItem);
        RefreshLanguage(null, EventArgs.Empty);
        LanguageUtil.LanguageUpdated += RefreshLanguage;

        _trayIcon = new TaskbarIcon {
            ToolTipText = "XamlNexus.Gallery",
            IconSource = new BitmapImage(new Uri("ms-appx:///Assets/xamlnexus.ico")),
            ContextFlyout = menu,
            LeftClickCommand = toggleWindow,
            NoLeftClickDelay = true,
        };
        _trayIcon.ForceCreate(enablesEfficiencyMode: false);

    }

    public void ShowNotification(string title, string message) {
        if (_trayIcon is null) throw new InvalidOperationException("The tray service is not initialized.");
        _trayIcon.ShowNotification(title, message);
    }

    private void RefreshLanguage(object? sender, EventArgs e) {
        if (_toggleItem is not null) _toggleItem.Text = LanguageUtil.GetI18n("Showcase_TrayToggle");
        if (_exitItem is not null) _exitItem.Text = LanguageUtil.GetI18n("Showcase_TrayExit");
    }

    private void ToggleWindow() {
        if (_isHidden) mainWindow.Show();
        else mainWindow.Hide();
        _isHidden = !_isHidden;
    }

    public void Dispose() {
        LanguageUtil.LanguageUpdated -= RefreshLanguage;
        _trayIcon?.Dispose();
        _trayIcon = null;
    }

    private MenuFlyoutItem? _toggleItem;
    private MenuFlyoutItem? _exitItem;
    private TaskbarIcon? _trayIcon;
    private bool _isHidden;
}