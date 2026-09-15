using System;
using WinUIEx;
using Microsoft.Extensions.DependencyInjection;
using Winui3_XamlNexus.Common.Utils.DI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;
using Winui3_XamlNexus.Common;
using Winui3_XamlNexus.Common.Logging;
using Winui3_XamlNexus.UIComponent.Navigation;
using Winui3_XamlNexus.UIComponent;
using Winui3_XamlNexus.UIComponent.Templates;
using Winui3_XamlNexus.UIComponent.Utils;
using Winui3_XamlNexus.Models.Datas.Interfaces;
using Winui3_XamlNexus.Models.Cores.Interfaces;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Winui3_XamlNexus.UI {
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : ArcWindow {
        public override ArcWindowHost ContentHost => this.MainHost;
        public override NavigationView AppNavView => this.NavigationViewControl;
        public override bool IsMainWindow => true;
        public override ArcWindowManagerKey Key => _windowKey;

        public MainWindow(IUserSettingsClient userSettings)
            : base(userSettings.Settings.ApplicationTheme, userSettings.Settings.SystemBackdrop) {
            _windowKey = new ArcWindowManagerKey(ArcWindowKey.Main);
            this.InitializeComponent();
            this.InitWindowConst();
            base.InitializeWindow();
            _navigationMenu = new NavigationMenu(NavigationViewControl, NavigationRegistry.Default);
            _navigationMenu.Select("home");

            _userSettings = userSettings;
            this.AppWindow.Closing += (_, _) => {
                // With tray installed, its close dialog decides whether to hide or exit.
                if (App.IsShuttingDown || AppServiceLocator.Services.GetService<ISystemTraySettings>() is null)
                    this.Hide();
            };
            this.Closed += MainWindow_Closed;
        }

        private void InitWindowConst() {
            WindowConsts.ArcWindowInstance = this;
            WindowConsts.WindowHandle = WindowNative.GetWindowHandle(this);
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args) {
            _navigationMenu.Dispose();
            App.ShutDown();
        }

        #region navigation control
        private void OnNavigationViewSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args) {
            try {
                Type? pageType = args.SelectedItemContainer?.Tag is string route
                    ? NavigationRegistry.Default.Resolve(route) : null;
                if (pageType is null)
                    return;

                NaviContent.Navigate(pageType);
            }
            catch (Exception ex) {
                GlobalMessageUtil.ShowException(ex, ArcWindowManager.GetArcWindow(Key));
                ArcLog.GetLogger<MainWindow>().Error(ex);
            }
        }
        #endregion

        private async void LightAndDarkButton_Click(object sender, RoutedEventArgs e) {
            LightAndDarkButton.IsEnabled = false;
            AppTheme previousTheme = _userSettings.Settings.ApplicationTheme;
            try {
                var nxTheme = GetNextTheme(ArcThemeUtil.MainWindowAppTheme);
                UpdateThemeFromThemeBtnClick(nxTheme);
                _userSettings.Settings.ApplicationTheme = nxTheme;
                await _userSettings.SaveAsync<ISettings>();
            }
            catch (Exception exception) {
                _userSettings.Settings.ApplicationTheme = previousTheme;
                UpdateThemeFromThemeBtnClick(previousTheme);
                ArcLog.GetLogger<MainWindow>().Error(exception);
                GlobalMessageUtil.ShowException(exception);
            }
            finally {
                LightAndDarkButton.IsEnabled = true;
            }
        }

        private static AppTheme GetNextTheme(AppTheme current) {
            return current switch {
                AppTheme.Light => AppTheme.Dark,
                AppTheme.Dark => AppTheme.Auto,
                AppTheme.Auto => AppTheme.Light,
                _ => AppTheme.Light
            };
        }

        private readonly NavigationMenu _navigationMenu;
        private readonly IUserSettingsClient _userSettings;
        private readonly ArcWindowManagerKey _windowKey;
    }
}
