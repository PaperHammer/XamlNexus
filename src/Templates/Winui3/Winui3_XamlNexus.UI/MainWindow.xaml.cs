using System;
using System.Threading;
using System.Threading.Tasks;
using Winui3_XamlNexus.UIComponent.Activation;
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
        public override ArcNavigationContentView NavigationContent => NaviContent;
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
            this.AppWindow.Closing += (_, args) => {
                if (args.Cancel) return;
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
        private object? selectedNavigationItem;
        private bool restoringNavigationSelection;
        private async void OnNavigationViewSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args) {
            if (restoringNavigationSelection) return;
            object? targetItem = sender.SelectedItem;
            Type? pageType = args.SelectedItemContainer?.Tag is string route
                ? NavigationRegistry.Default.Resolve(route) : null;
            if (pageType is null) return;
            RestoreNavigationSelection(sender, selectedNavigationItem);
            try {
                if (await NaviContent.NavigateAsync(pageType)) {
                    selectedNavigationItem = targetItem;
                    RestoreNavigationSelection(sender, targetItem);
                }
            }
            catch (Exception ex) {
                GlobalMessageUtil.ShowException(ex, ArcWindowManager.GetArcWindow(Key));
                ArcLog.GetLogger<MainWindow>().Error(ex);
            }
        }

        /// <summary>Business activation shares the same leave guard as sidebar navigation.</summary>
        public async Task<ActivationNavigationResult> NavigateActivationAsync(BusinessActivationRequest request,
            CancellationToken cancellationToken = default) {
            var navigator = new BusinessActivationNavigator(NavigationRegistry.Default);
            var result = await navigator.NavigateAsync(request, async (pageType, payload, token) => {
                if (!await NaviContent.NavigateAsync(pageType, payload, cancellationToken: token)) return false;
                restoringNavigationSelection = true;
                try {
                    _navigationMenu.Select(request.Route);
                    selectedNavigationItem = NavigationViewControl.SelectedItem;
                }
                finally { restoringNavigationSelection = false; }
                return true;
            }, cancellationToken);
            if (result == ActivationNavigationResult.RouteNotFound)
                throw new InvalidOperationException($"Unknown business route '{request.Route}'.");
            return result;
        }

        private void RestoreNavigationSelection(NavigationView sender, object? item) {
            restoringNavigationSelection = true;
            try { sender.SelectedItem = item; }
            finally { restoringNavigationSelection = false; }
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
