using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Winui3_XamlNexus.AppSettingsPanel.ViewModels;
using Winui3_XamlNexus.Common.Utils.DI;
using Winui3_XamlNexus.Common;
using Winui3_XamlNexus.Models.Datas.Interfaces;
using Microsoft.UI.Xaml;
using System;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Winui3_XamlNexus.AppSettingsPanel.Views {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class GeneralSetting : Page {
        public GeneralSetting() {
            this.InitializeComponent();
            this.Unloaded += GeneralSetting_Unloaded;
            _viewModel = AppServiceLocator.Services.GetRequiredService<GeneralSettingViewModel>();
            this.DataContext = _viewModel;                    
            this.Loaded += GeneralSetting_Loaded;
        }

        private void GeneralSetting_Loaded(object sender, RoutedEventArgs e) {
            // Resolve an optional capability, so tray and settings can be installed in either order.
            _traySettings = AppServiceLocator.Services.GetService<ISystemTraySettings>();
            TraySettings.Visibility = _traySettings is null ? Visibility.Collapsed : Visibility.Visible;
            if (_traySettings is null) return;
            bool chinese = AppServiceLocator.Services.GetRequiredService<IUserSettingsClient>()
                .Settings.Language.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            _loadingTraySettings = true;
            try {
                TraySettingsHeader.Text = chinese ? "关闭主窗口时" : "When closing the main window";
                TraySettingsDescription.Text = chinese
                    ? "选择关闭窗口时询问、隐藏到托盘或退出应用"
                    : "Choose whether to ask, hide to the tray, or exit the application.";
                TrayCloseBehavior.ItemsSource = chinese
                    ? new[] { "每次询问", "隐藏到系统托盘", "退出应用" }
                    : new[] { "Ask every time", "Hide to system tray", "Exit application" };
                TrayCloseBehavior.SelectedIndex = (int)_traySettings.CloseBehavior;
            }
            finally { _loadingTraySettings = false; }
        }

        private async void TrayCloseBehavior_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (_loadingTraySettings || _traySettings is null || TrayCloseBehavior.SelectedIndex < 0) return;
            TrayCloseBehavior.IsEnabled = false;
            TraySettingsError.IsOpen = false;
            try {
                await _traySettings.SetCloseBehaviorAsync((WindowCloseBehavior)TrayCloseBehavior.SelectedIndex);
            }
            catch (Exception exception) {
                _loadingTraySettings = true;
                try { TrayCloseBehavior.SelectedIndex = (int)_traySettings.CloseBehavior; }
                finally { _loadingTraySettings = false; }
                TraySettingsError.Message = exception.Message;
                TraySettingsError.IsOpen = true;
            }
            finally { TrayCloseBehavior.IsEnabled = true; }
        }

        private ISystemTraySettings? _traySettings;
        private bool _loadingTraySettings;

        private void GeneralSetting_Unloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) {
            this.DataContext = null;
            this.Unloaded -= GeneralSetting_Unloaded;
        }

        private readonly GeneralSettingViewModel _viewModel;
    }
}
