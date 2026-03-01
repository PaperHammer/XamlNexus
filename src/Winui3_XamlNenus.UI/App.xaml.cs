using System;
using System.Threading;
using Microsoft.UI.Xaml;
using Winui3_XamlNexus.Common;
using Winui3_XamlNexus.Common.Logging;
using XamlNexus.Common.Utils;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Winui3_XamlNenus.UI {
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

            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args) {
            m_window = new MainWindow();
            m_window.Activate();
        }

        private Window? m_window;
        private readonly Mutex _mutex = new(false, Consts.CoreField.UniqueAppUIUid);
    }
}
