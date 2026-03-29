using System.Windows;
using System.Windows.Interop;
using Microsoft.Toolkit.Uwp.Notifications;
using Winui3_Wpf_XamlNexus.Common;
using Winui3_Wpf_XamlNexus.Lang;
using Winui3_Wpf_XamlNexus.Services.Interfaces;

namespace Winui3_Wpf_XamlNexus;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window {
    public MainWindow(IUIRunnerService uiRunner) {
        _uiRunnerService = uiRunner;
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e) {
        WindowInteropHelper wndHelper = new(this);
        Consts.Runtime.MainWindowHwnd = wndHelper.Handle;
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
        notifyIcon.Dispose();
        ToastNotificationManagerCompat.Uninstall();
    }

    private void Window_SourceInitialized(object sender, EventArgs e) {
        new ToastContentBuilder()
                .AddText(LanguageManager.Instance["Winui3_Wpf_XamlNexus_isRunning"])
                .Show();
    }

    private void NotifyIcon_LeftDoubleClick(Wpf.Ui.Tray.Controls.NotifyIcon sender, RoutedEventArgs e) {
        _uiRunnerService.ShowUI();
        e.Handled = true;
    }

    private void OpenAppMenuItem_Click(object sender, RoutedEventArgs e) {
        _uiRunnerService.ShowUI();
    }

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e) {
        App.ShutDown();
    }

    private readonly IUIRunnerService _uiRunnerService;
}