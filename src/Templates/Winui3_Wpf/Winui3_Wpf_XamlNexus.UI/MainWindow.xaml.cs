using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;
using Winui3_Wpf_XamlNexus.AppSettingsPanel;
using Winui3_Wpf_XamlNexus.Common;
using Winui3_Wpf_XamlNexus.Common.Logging;
using Winui3_Wpf_XamlNexus.Common.Utils.IPC;
using Winui3_Wpf_XamlNexus.Common.Utils.ThreadContext;
using Winui3_Wpf_XamlNexus.Grpc.Client.Interfaces;
using Winui3_Wpf_XamlNexus.MainPanel;
using Winui3_Wpf_XamlNexus.Models.Cores.Interfaces;
using Winui3_Wpf_XamlNexus.UIComponent;
using Winui3_Wpf_XamlNexus.UIComponent.Templates;
using Winui3_Wpf_XamlNexus.UIComponent.Utils;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Winui3_Wpf_XamlNexus.UI {
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : ArcWindow {
        public override ArcWindowHost ContentHost => this.MainHost;
        public override NavigationView AppNavView => this.NavigationViewControl;
        public override bool IsMainWindow => true;
        public override ArcWindowManagerKey Key => _windowKey;

        public MainWindow(IUserSettingsClient userSettings, ICommandsClient commandsClient)
            : base(userSettings.Settings.ApplicationTheme, userSettings.Settings.SystemBackdrop) {
            _windowKey = new ArcWindowManagerKey(ArcWindowKey.Main);
            this.InitializeComponent();
            this.InitWindowConst();
            base.InitializeWindow();

            _userSettings = userSettings;
            _commandsClient = commandsClient;
            _commandsClient.UIRecieveCmd += CommandsClient_UIRecieveCmd;
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
                Type pageType = args.SelectedItemContainer.Name switch {
                    "Nav_MainPage" => typeof(MainPage),
                    "Nav_AppSettings" => typeof(AppSettings),
                    _ => throw new NotImplementedException(),
                };

                NaviContent.Navigate(pageType);
            }
            catch (Exception ex) {
                GlobalMessageUtil.ShowException(ex, ArcWindowManager.GetArcWindow(Key));
                ArcLog.GetLogger<MainWindow>().Error(ex);
            }
        }
        #endregion

        #region ipc
        private void CommandsClient_UIRecieveCmd(object? sender, int e) {
            HandleIpcMessage(e);
        }

        private void HandleIpcMessage(int type) {
            try {
                MessageType messageType = (MessageType)type;
                switch (messageType) {
                    case MessageType.cmd_active:
                        CrossThreadInvoker.InvokeOnUIThread(() => {
                            this.Activate();
                        });
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported message type: {messageType}");
                }
            }
            catch (Exception ex) {
                ArcLog.GetLogger<MainWindow>().Error(ex);
            }
        }
        #endregion

        private void LightAndDarkButton_Click(object sender, RoutedEventArgs e) {
            LightAndDarkButton.IsEnabled = false;
            try {
                var nxTheme = GetNextTheme(ArcThemeUtil.MainWindowAppTheme);
                UpdateThemeFromThemeBtnClick(nxTheme);
                _userSettings.Settings.ApplicationTheme = nxTheme;
                _userSettings.SaveAsync<ISettings>();
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
        private readonly ICommandsClient _commandsClient;
    }
}
