using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Winui3_XamlNexus.Common;
using Winui3_XamlNexus.Common.Logging;
using Winui3_XamlNexus.Common.Utils.ThreadContext;
using Winui3_XamlNexus.UIComponent.Utils;
using WinUIEx;
using System.Threading.Tasks;
using WInui3_XamlNexus.Models.Datas.Interfaces;
using WInui3_XamlNexus.Models.Datas;
using Winui3_XamlNexus.Common.Utils.DI;
using Winui3_XamlNexus.Common.Utils;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Winui3_XamlNexus.UI {
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

            #region 初始化核心组件
            // 依赖注入
            AppServiceLocator.Services = ConfigureServices();
            #endregion

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

                .BuildServiceProvider();

            return provider;
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
