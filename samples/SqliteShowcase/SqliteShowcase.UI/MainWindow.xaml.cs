using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;
using SqliteShowcase.AppSettingsPanel;
using SqliteShowcase.Common;
using SqliteShowcase.Common.Logging;
using SqliteShowcase.MainPanel;
using SqliteShowcase.UIComponent;
using SqliteShowcase.UIComponent.Templates;
using SqliteShowcase.UIComponent.Utils;
using SqliteShowcase.Models.Datas.Interfaces;
using SqliteShowcase.Models.Cores.Interfaces;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SqliteShowcase.UI {
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

            _userSettings = userSettings;
            this.Closed += MainWindow_Closed;
        }

        private void InitWindowConst() {
            WindowConsts.ArcWindowInstance = this;
            WindowConsts.WindowHandle = WindowNative.GetWindowHandle(this);
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args) {
            App.ShutDown();
        }

        #region navigation control
        private void OnNavigationViewSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args) {
            try {
                Type? pageType = args.SelectedItemContainer?.Name switch {
                    "Nav_MainPage" => typeof(MainPage),
                    "Nav_AppSettings" => typeof(AppSettings),
                    _ => null,
                };
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

        private readonly IUserSettingsClient _userSettings;
        private readonly ArcWindowManagerKey _windowKey;
    }
}
