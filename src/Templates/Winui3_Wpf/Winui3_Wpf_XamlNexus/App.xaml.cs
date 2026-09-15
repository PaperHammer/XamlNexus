using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using GrpcDotNetNamedPipes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Win32;
using Winui3_Wpf_XamlNexus.Common;
using Winui3_Wpf_XamlNexus.Common.Events;
using Winui3_Wpf_XamlNexus.Common.Logging;
using Winui3_Wpf_XamlNexus.Common.Utils;
using Winui3_Wpf_XamlNexus.Common.Utils.Files;
using Winui3_Wpf_XamlNexus.Common.Updates;
using Winui3_Wpf_XamlNexus.Core;
using Winui3_Wpf_XamlNexus.Core.AppUpdate;
using Winui3_Wpf_XamlNexus.Core.Monitor;
using Winui3_Wpf_XamlNexus.GrpcServers;
using Winui3_Wpf_XamlNexus.Lang;
using Winui3_Wpf_XamlNexus.Models.Cores.Interfaces;
using Winui3_Wpf_XamlNexus.Modules;
using Winui3_Wpf_XamlNexus.Services;
using Winui3_Wpf_XamlNexus.Services.Interfaces;
using Winui3_Wpf_XamlNexus.Utils;
using Winui3_Wpf_XamlNexus.Grpc.Service.Commands;
using Winui3_Wpf_XamlNexus.Grpc.Service.Update;
using Winui3_Wpf_XamlNexus.Grpc.Service.UserSettings;
using Wpf.Ui;
using Wpf.Ui.Appearance;

namespace Winui3_Wpf_XamlNexus;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application {
    internal static JobService Jobs => Services.GetRequiredService<JobService>();
    internal static IUserSettingsService UserSettings => Services.GetRequiredService<IUserSettingsService>();

    public static IServiceProvider Services {
        get {
            IServiceProvider? serviceProvider = ((App)Current)._serviceProvider;
            return serviceProvider ?? throw new InvalidOperationException("The service provider is not initialized");
        }
    }

    public App() {
        #region 唯一实例检查
        try {
            // 保证全局只有一个实例
            if (!_mutex.WaitOne(TimeSpan.FromSeconds(1), false)) {
                if (Environment.GetCommandLineArgs().Contains("--xamlnexus-run")) {
                    Console.Error.WriteLine("The background host is already running. Exit it before starting a development run.");
                    Environment.Exit(1);
                }
                MessageBox.Show("已存在正在运行的程序，请检查托盘或任务管理器\nThere are already running programs, check the tray or Task Manager", "Winui3_Wpf_XamlNexus", MessageBoxButton.OK, MessageBoxImage.Information);
                ShutDown();
                return;
            }
        }
        catch (AbandonedMutexException e) {
            _ = e;
#if DEBUG
            //unexpected app termination.
            Debug.WriteLine(e.Message);
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
            MessageBox.Show(ex.Message, "AppData directory creation failed, exiting..", MessageBoxButton.OK, MessageBoxImage.Error);
            ShutDown();
            return;
        }
        #endregion

        ReportUpdateStartup();

        #region 初始化核心组件
        // 依赖注入
        _serviceProvider = ConfigureServices();
        // 模块必须在 gRPC 服务开放前完成初始化（例如数据库迁移）。
        _moduleCatalog.InitializeAsync(_serviceProvider).GetAwaiter().GetResult();
        // 将方法绑定到 Grpc 服务上
        _grpcServer = ConfigureGrpcServer(_serviceProvider);
        #endregion

        #region 用户配置
        if (UserSettings.Settings.IsUpdated || UserSettings.Settings.IsFirstRun) {
            UserSettings.Settings.IsFirstRun = false;
            UserSettings.Save<ISettings>();
        }

        // 初始化语言包
        ChangeLanguage(UserSettings.Settings.Language);

        UserSettings.Save<ISettings>();
        #endregion

        #region 启动相关后台服务
        try {
            // 启动托盘（后台）服务
            Services.GetRequiredService<MainWindow>().Show();
            var appUpdater = Services.GetRequiredService<IAppUpdaterService>();
            appUpdater.UpdateChecked += AppUpdateChecked;
            appUpdater.InstallerLaunched += AppUpdater_InstallerLaunched;
            appUpdater.Start();
        }
        catch (Exception ex) {
            ArcLog.GetLogger<App>().Error(ex);
            if (Environment.GetCommandLineArgs().Contains("--xamlnexus-run")) {
                Console.Error.WriteLine($"Could not initialize the background host: {ex}");
                ShutDown();
                Environment.Exit(1);
            }
            MessageBox.Show("Cores runtime Error, please restart or reinstall.\n" + ex.Message);
            return;
        }
        #endregion

        try {
            //first run Setup-Wizard show..
            if (UserSettings.Settings.IsFirstRun || Environment.GetCommandLineArgs().Contains("--xamlnexus-run")) {
                Services.GetRequiredService<IUIRunnerService>().ShowUI();
            }
        }
        catch (Exception ex) {
            ArcLog.GetLogger<App>().Error(ex);
            if (Environment.GetCommandLineArgs().Contains("--xamlnexus-run")) {
                Console.Error.WriteLine($"Could not start the UI: {ex}");
                ShutDown();
                Environment.Exit(1);
            }
            MessageBox.Show("Cores runtime Error, please restart or reinstall.\n" + ex.Message);
            return;
        }

        #region 事件绑定
        //need to load theme later stage of startu to update..
        this.Startup += (s, e) => {
            ChangeTheme(UserSettings.Settings.ApplicationTheme);
        };

        //Ref: https://github.com/Kinnara/ModernWpf/blob/master/ModernWpf/Helpers/ColorsHelper.cs
        SystemEvents.UserPreferenceChanged += (s, e) => {
            if (e.Category == UserPreferenceCategory.General) {
                if (UserSettings.Settings.ApplicationTheme == AppTheme.Auto) {
                    ChangeTheme(AppTheme.Auto);
                }
            }
        };

        this.SessionEnding += (s, e) => {
            if (e.ReasonSessionEnding == ReasonSessionEnding.Shutdown || e.ReasonSessionEnding == ReasonSessionEnding.Logoff) {
                e.Cancel = true;
                ShutDown();
            }
        };
        #endregion
    }

    private ServiceProvider ConfigureServices() {
        var services = new ServiceCollection()
            .AddSingleton<IContentDialogService, ContentDialogService>()
            .AddSingleton<IMonitorManager, MonitorManager>()
            .AddSingleton<JobService>()
            .AddSingleton<IUIRunnerService, UIRunnerService>()
            .AddSingleton<IUserSettingsService, UserSettingsService>()
            .AddSingleton<IAppUpdaterService, AppUpdaterService>()
            .AddSingleton<IWindowService, WindowService>()

            .AddSingleton<UserSettingServer>()
            .AddSingleton<AppUpdateServer>()
            .AddSingleton<CommandsServer>()
            .AddSingleton<MainWindow>();

        _moduleCatalog.ConfigureServices(services);
        return services.BuildServiceProvider();
    }

    private NamedPipeServer ConfigureGrpcServer(IServiceProvider serviceProvider) {
        var server = new NamedPipeServer(Consts.CoreField.GrpcPipeServerName);

        Grpc_UserSettingsService.BindService(server.ServiceBinder, serviceProvider.GetRequiredService<UserSettingServer>());
        Grpc_UpdateService.BindService(server.ServiceBinder, serviceProvider.GetRequiredService<AppUpdateServer>());
        Grpc_CommandsService.BindService(server.ServiceBinder, serviceProvider.GetRequiredService<CommandsServer>());
        _moduleCatalog.BindGrpcServices(server.ServiceBinder, serviceProvider);
        server.Start();

        return server;
    }

    private static void LogUnhandledException(Exception exception, string source)
        => ArcLog.GetLogger<App>().Error(source, exception);

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

    private void SetupUnhandledExceptionLogging() {
        // 当.NET应用程序域中的任何线程抛出了未捕获的异常时，会触发此事件。
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            LogUnhandledException((Exception)e.ExceptionObject, "AppDomain.CurrentDomain.UnhandledException");

        // 对于WPF应用程序，如果UI线程（Dispatcher线程）上发生的未捕获异常，会触发此事件。
        Dispatcher.UnhandledException += (s, e) =>
            LogUnhandledException(e.Exception, "Application.Current.DispatcherUnhandledException");

        // 在异步编程中，如果一个Task（任务）完成了但其结果（无论是成功还是失败）未被观察（即没有使用await关键字等待或订阅Result属性），那么UnobservedTaskException事件会在垃圾回收时触发。
        //ref: https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.taskscheduler.unobservedtaskexception?redirectedfrom=MSDN&view=net-6.0
        TaskScheduler.UnobservedTaskException += (s, e) =>
            LogUnhandledException(e.Exception, "TaskScheduler.UnobservedTaskException");
    }

    public static void ChangeTheme(AppTheme theme) {
        try {
            theme = theme == AppTheme.Auto ? ThemeUtil.GetWindowsTheme() : theme;
            ApplicationTheme applicationTheme = theme == AppTheme.Light
                ? ApplicationTheme.Light : ApplicationTheme.Dark;

            Application.Current.Dispatcher.Invoke(() => {
                ApplicationThemeManager.Apply(applicationTheme, updateAccent: false);
            });
        }
        catch (Exception e) {
            ArcLog.GetLogger<App>().Error(e);
        }
        ArcLog.GetLogger<App>().Info($"Theme changed: {theme}");
    }

    public static void ChangeLanguage(string lang) {
        if (lang == string.Empty) return;

        Application.Current.Dispatcher.Invoke(() => {
            LanguageManager.Instance.ChangeLanguage(new(lang));
        });
    }

    public static void AppUpdateDialog(AppUpdaterEventArgs e) {
        if (e.UpdateUri is null || e.UpdateSHAUri is null) return;

        _updateNotify = false;
        var windowService = Services.GetRequiredService<IWindowService>();
        var info = new AppUpdateInfo(e.UpdateUri, e.UpdateSHAUri, e.UpdateVersion.ToString(), e.ChangeLog);
        // open updater at here
        //e.g. windowService.Show<AppUpdaterWindow>(info);
    }

    private static int _updateNotifyAmt = 1;
    private static bool _updateNotify = false;
    private void AppUpdateChecked(object? sender, AppUpdaterEventArgs e) {
        _ = Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new ThreadStart(delegate {
            if (e.UpdateStatus == AppUpdateStatus.Available) {
                if (_updateNotifyAmt > 0) {
                    _updateNotifyAmt--;
                    _updateNotify = true;
                    new ToastContentBuilder()
                        .AddText(LanguageManager.Instance["Find_New_Verison"])
                        .Show();
                }

                //If UI program already running then notification is displayed withing the it.
                if (!Services.GetRequiredService<IUIRunnerService>().IsVisibleUI && _updateNotify) {
                    AppUpdateDialog(e);
                }
            }
            ArcLog.GetLogger<App>().Info($"AppUpdate status: {e.UpdateStatus}");
        }));
    }

    private static void AppUpdater_InstallerLaunched(object? sender, EventArgs e) {
        _ = Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(ShutDown));
    }

    public static void ShutDown() {
        try {
            _ctsPlayback.Cancel();
            Jobs?.Close();
            Jobs?.Dispose();
            ((ServiceProvider)Services)?.Dispose();
            ((App)Current)._grpcServer?.Kill();
            ((App)Current)._grpcServer?.Dispose();
            ToastNotificationManagerCompat.Uninstall();

            if (UserSettings.Settings.IsUpdated) {
                UserSettings.Settings.IsUpdated = false;
                UserSettings.Save<ISettings>();
            }
        }
        catch (InvalidOperationException) { /* not initialised */ }

        Application.Current.Dispatcher.Invoke(Application.Current.Shutdown);
    }

    private readonly IServiceProvider? _serviceProvider;
    private readonly XamlNexusModuleCatalog _moduleCatalog = XamlNexusModuleCatalog.Discover();
    private readonly Mutex _mutex = new(false, Consts.CoreField.UniqueAppUid);
    private readonly NamedPipeServer? _grpcServer;
    private static readonly CancellationTokenSource _ctsPlayback = new();
}

