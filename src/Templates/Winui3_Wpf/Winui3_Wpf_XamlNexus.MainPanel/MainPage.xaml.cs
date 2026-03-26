using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Winui3_Wpf_XamlNexus.Common.Logging;
using Winui3_Wpf_XamlNexus.UIComponent.Attributes;
using Winui3_Wpf_XamlNexus.UIComponent.Templates;
using Winui3_Wpf_XamlNexus.UIComponent.Utils;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Winui3_Wpf_XamlNexus.MainPanel {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    [KeepAlive]
    public sealed partial class MainPage : ArcPage {
        public override Type ArcType => typeof(MainPage);

        public MainPage() {
            this.InitializeComponent();
            ArcContext.AttachLoadingComponent(this.MainHost.LoadingControlHost);
        }

        private async void MyButton1_Click(object sender, RoutedEventArgs e) {
            var ctx = ArcPageContextManager.GetContext<MainPage>();
            var loadingCtx = ctx?.LoadingContext;
            if (loadingCtx == null) return;

            await loadingCtx.RunAsync(
                operation: async token => {
                    try {
                        await Task.Delay(10000, token);
                    }
                    catch (Exception ex) {
                        GlobalMessageUtil.ShowException(ex);
                        ArcLog.GetLogger<MainPage>().Error(ex);
                    }
                });
        }
        
        private async void MyButton2_Click(object sender, RoutedEventArgs e) {
            var ctx = ArcPageContextManager.GetContext<MainPage>();
            var loadingCtx = ctx?.LoadingContext;
            if (loadingCtx == null) return;

            var cts = new CancellationTokenSource();
            await loadingCtx.RunAsync(
                operation: async token => {
                    try {
                        await Task.Delay(10000, token);
                    }
                    catch (Exception ex) when (
                            ex is OperationCanceledException) {
                        GlobalMessageUtil.ShowCanceled();
                        return;
                    }
                    catch (Exception ex) {
                        GlobalMessageUtil.ShowException(ex);
                        ArcLog.GetLogger<MainPage>().Error(ex);
                    }
                }, cts: cts);
        }
        
        private async void MyButton3_Click(object sender, RoutedEventArgs e) {
            var ctx = ArcPageContextManager.GetContext<MainPage>();
            var loadingCtx = ctx?.LoadingContext;
            if (loadingCtx == null) return;

            var cts = new CancellationTokenSource();
            int finishedCnt = 0;
            int total = 10;
            await loadingCtx.RunWithProgressAsync(
                operation: async (token, reportProgress) => {
                    try {
                        for (int i = 0; i < total; i++) {
                            await Task.Delay(1000, token);
                            reportProgress(++finishedCnt, total);
                        }
                    }
                    catch (Exception ex) when (
                            ex is OperationCanceledException) {
                        GlobalMessageUtil.ShowCanceled();
                        return;
                    }
                    catch (Exception ex) {
                        GlobalMessageUtil.ShowException(ex);
                        ArcLog.GetLogger<MainPage>().Error(ex);
                    }
                }, total: total, cts: cts);
        }
    }
}
