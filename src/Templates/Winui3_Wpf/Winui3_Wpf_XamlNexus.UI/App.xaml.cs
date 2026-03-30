using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.Core;
using Winui3_Wpf_XamlNexus.AppSettingsPanel.ViewModels;
using Winui3_Wpf_XamlNexus.Common;
using Winui3_Wpf_XamlNexus.Common.Logging;
using Winui3_Wpf_XamlNexus.Common.Utils;
using Winui3_Wpf_XamlNexus.Common.Utils.DI;
using Winui3_Wpf_XamlNexus.Common.Utils.Files;
using Winui3_Wpf_XamlNexus.Common.Utils.PInvoke;
using Winui3_Wpf_XamlNexus.Common.Utils.ThreadContext;
using Winui3_Wpf_XamlNexus.Grpc.Client;
using Winui3_Wpf_XamlNexus.Grpc.Client.Interfaces;
using Winui3_Wpf_XamlNexus.UIComponent.Utils;
using WinUIEx;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Winui3_Wpf_XamlNexus.UI {
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App() {
            #region 唯一实例检查
            try {
                if (!_mutex.WaitOne(TimeSpan.FromSeconds(1), false)) {
                    ArcLog.GetLogger<App>().Warn("UI has been running.");
                    Environment.Exit(0);
                    return;
                }
            }
            catch (AbandonedMutexException e) {
#if DEBUG
                //unexpected app termination.
                DebugUtil.Output(e.Message);
#endif
            }
            #endregion

            SetupUnhandledExceptionLogging(); // 初始化异常处理机制
            ArcLog.GetLogger<App>().Info(LogUtil.GetHardwareInfo()); // 记录硬件信息

            #region 必要路径处理
            try {
                // 清空缓存
                FileUtil.EmptyDirectory(Consts.CommonPaths.TempDir);
            }
            catch { }

            try {
                // 创建必要目录, eg: C:\Users\<User>\AppData\Local
                Directory.CreateDirectory(Consts.CommonPaths.AppDataDir);
                Directory.CreateDirectory(Consts.CommonPaths.CommonDataDir);
                Directory.CreateDirectory(Consts.CommonPaths.LogDir);
                Directory.CreateDirectory(Consts.CommonPaths.LogDirUI);
                Directory.CreateDirectory(Consts.CommonPaths.TempDir);
            }
            catch (Exception ex) {
                System.Windows.MessageBox.Show(ex.Message, "AppData directory creation failed, exiting..", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                ShutDown();
                return;
            }
            #endregion

            #region 初始化核心组件
            AppServiceLocator.Services = ConfigureServices();
            #endregion

            if (!SingleInstanceUtil.IsAppMutexRunning(Consts.CoreField.UniqueAppUid)) {
                _ = Native.MessageBox(IntPtr.Zero, "Winui3_Wpf_XamlNexus core is not running, run \"Winui3_Wpf_XamlNexus.exe\" first before opening UI.", "Winui3_Wpf_XamlNexus", 16);
                //Sad dev noises.. this.Exit() does not work without Window: https://github.com/microsoft/microsoft-ui-xaml/issues/5931
                Process.GetCurrentProcess().Kill();
            }

            ArcLog.GetLogger<App>().Info("Starting UI...");
            _userSettings = AppServiceLocator.Services.GetRequiredService<IUserSettingsClient>();

            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args) {
            CrossThreadInvoker.Initialize(new UiSynchronizationContext());

            // ref: https://github.com/microsoft/WindowsAppSDK/issues/1687
            //ApplicationLanguages.PrimaryLanguageOverride = _userSettings.Settings.Language;

            // ref: https://github.com/AndrewKeepCoding/WinUI3Localizer
            if (Consts.ApplicationType.IsMSIX) {
                await LanguageUtil.InitializeLocalizerForPackaged(_userSettings.Settings.Language);
            }
            else {
                await LanguageUtil.InitializeLocalizerForUnpackaged(_userSettings.Settings.Language);
            }

            var m_window = AppServiceLocator.Services.GetRequiredService<MainWindow>();
            m_window.Show();
            m_window.Activate();
        }

        private ServiceProvider ConfigureServices() {
            var provider = new ServiceCollection()
                .AddSingleton<MainWindow>()

                .AddSingleton<IUserSettingsClient, UserSettingsClient>()
                .AddSingleton<ICommandsClient, CommandsClient>()
                .AddSingleton<IAppUpdaterClient, AppUpdaterClient>()

                .AddSingleton<GeneralSettingViewModel>()
                .AddSingleton<SystemSettingViewModel>()

                .BuildServiceProvider();

            return provider;
        }

        private static void LogUnhandledException(Exception exception) => ArcLog.GetLogger<App>().Error(exception);

        private static void LogUnhandledException(UnhandledError exception) => ArcLog.GetLogger<App>().Error(exception);

        //Not working ugh..
        //Issue: https://github.com/microsoft/microsoft-ui-xaml/issues/5221
        private void SetupUnhandledExceptionLogging() {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                LogUnhandledException((Exception)e.ExceptionObject);

            TaskScheduler.UnobservedTaskException += (s, e) =>
                LogUnhandledException(e.Exception);

            this.UnhandledException += (s, e) =>
                LogUnhandledException(e.Exception);

            CoreApplication.UnhandledErrorDetected += (s, e) =>
                LogUnhandledException(e.UnhandledError);
        }

        public static void ShutDown() {
            Application.Current.Exit();
            _ = Task.Run(() => {
                ((ServiceProvider)AppServiceLocator.Services)?.Dispose();
                ArcLog.GetLogger<App>().Info("UI was closed");
            });
        }

        private readonly IUserSettingsClient _userSettings;
        private readonly Mutex _mutex = new(false, Consts.CoreField.UniqueAppUIUid);
    }
}
