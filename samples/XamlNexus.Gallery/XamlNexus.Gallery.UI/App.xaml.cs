using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.Core;
using XamlNexus.Gallery.Common;
using XamlNexus.Gallery.Common.Logging;
using XamlNexus.Gallery.Common.Utils;
using XamlNexus.Gallery.Common.Utils.DI;
using XamlNexus.Gallery.Common.Utils.Files;
using XamlNexus.Gallery.Common.Utils.ThreadContext;
using XamlNexus.Gallery.Common.Updates;
using XamlNexus.Gallery.UIComponent.Utils;
using XamlNexus.Gallery.Models.Datas;
using XamlNexus.Gallery.Models.Datas.Interfaces;
using XamlNexus.Gallery.UI.Modules;
using WinUIEx;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace XamlNexus.Gallery.UI {
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
                _ = e;
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
                Application.Current.Exit();
                return;
            }
            #endregion

            ReportUpdateStartup();

            #region 初始化核心组件
            AppServiceLocator.Services = ConfigureServices();
            AppServiceLocator.Services.GetRequiredService<IAppUpdaterClient>().InstallerLaunched += AppUpdater_InstallerLaunched;
            #endregion

            ArcLog.GetLogger<App>().Info("Starting UI...");
            _userSettings = AppServiceLocator.Services.GetRequiredService<IUserSettingsClient>();

            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args) {
            if (_userSettings is null) {
                Application.Current.Exit();
                return;
            }

            try {
                CrossThreadInvoker.Initialize(new UiSynchronizationContext());

                if (Consts.ApplicationType.IsMSIX) {
                    await LanguageUtil.InitializeLocalizerForPackaged(_userSettings.Settings.Language);
                }
                else {
                    await LanguageUtil.InitializeLocalizerForUnpackaged(_userSettings.Settings.Language);
                }

                await _moduleCatalog.InitializeAsync(AppServiceLocator.Services);

                var window = AppServiceLocator.Services.GetRequiredService<MainWindow>();
                window.Show();
            }
            catch (Exception exception) {
                ArcLog.GetLogger<App>().Error("Application startup failed", exception);
                try {
                    // Startup may fail before a XamlRoot exists, so use a native dialog.
                    bool chinese = _userSettings.Settings.Language.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
                    string message = chinese
                        ? $"应用启动失败：{exception.Message}\n\n日志目录：{Consts.CommonPaths.LogDirUI}"
                        : $"The application could not start: {exception.Message}\n\nLogs: {Consts.CommonPaths.LogDirUI}";
                    System.Windows.MessageBox.Show(message, Consts.CoreField.AppName,
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
                finally {
                    ShutDown();
                }
            }
        }

        private ServiceProvider ConfigureServices() {
            var services = new ServiceCollection()
                .AddSingleton<MainWindow>()

                .AddSingleton<IUserSettingsClient, UserSettingsClient>()
                .AddSingleton<IAppUpdaterClient, AppUpdaterClient>();

            _moduleCatalog.ConfigureServices(services);
            return services.BuildServiceProvider();
        }

        private static void LogUnhandledException(Exception exception) => ArcLog.GetLogger<App>().Error(exception);

        private static void ReportUpdateStartup() {
            var currentVersion = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0, 0);
            var result = AppUpdateLifecycle.CompleteStartup(
                Path.Combine(Consts.CommonPaths.CommonDataDir, "updates"),
                currentVersion,
                Path.Combine(Consts.CommonPaths.TempDir, "updates"));
            if (result.Status == AppUpdateStartupStatus.Updated) {
                ArcLog.GetLogger<App>().Info($"Application update to {currentVersion} completed successfully.");
            }
            else if (result.Status == AppUpdateStartupStatus.Pending) {
                ArcLog.GetLogger<App>().Warn($"The installer for update {result.TargetVersion} did not update the application.");
            }
            else if (result.Status == AppUpdateStartupStatus.InvalidState) {
                ArcLog.GetLogger<App>().Warn("An invalid or stale pending-update state was removed.");
            }
        }

        private static void AppUpdater_InstallerLaunched(object? sender, EventArgs e) {
            CrossThreadInvoker.InvokeOnUIThread(ShutDown);
        }

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
            if (_isShuttingDown) return;
            _isShuttingDown = true;
            try {
                ((ServiceProvider)AppServiceLocator.Services)?.Dispose();
            }
            catch (Exception exception) {
                ArcLog.GetLogger<App>().Error("Application cleanup failed", exception);
            }
            finally {
                ArcLog.GetLogger<App>().Info("UI was closed");
                Application.Current.Exit();
            }
        }

        private static bool _isShuttingDown;
        private readonly IUserSettingsClient? _userSettings;
        private readonly XamlNexusModuleCatalog _moduleCatalog = XamlNexusModuleCatalog.Discover();
        private readonly Mutex _mutex = new(false, Consts.CoreField.UniqueAppUIUid);
    }
}
