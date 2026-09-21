using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using XamlNexus.Gallery.UIComponent.Templates;
using XamlNexus.Gallery.UIComponent.Utils;

namespace XamlNexus.Gallery.MainPanel.Gallery;

public sealed partial class ArcPageGalleryPage : ArcPage
{
    public override Type ArcType => typeof(ArcPageGalleryPage);
    private bool loadingDemoRunning;

    public ArcPageGalleryPage()
    {
        InitializeComponent();
        ArcContext?.AttachLoadingComponent(LoadingHost.LoadingControlHost);
        LoadingXaml.SetSource(ReadExample("Loading.xaml.txt"), "LoadingPage.xaml");
        LoadingCode.SetSource(ReadExample("Loading.cs.txt"), "LoadingPage.xaml.cs");
        XamlExample.SetSource(ReadExample("Page.xaml.txt"), "DetailsPage.xaml");
        CodeExample.SetSource(ReadExample("Page.cs.txt"), "DetailsPage.xaml.cs");
        LifecycleCode.SetSource(
            "protected override void OnEnter(FrameworkPayload? payload)\n" +
            "{\n" +
            "    base.OnEnter(payload);\n" +
            "    // Read payload and refresh page state.\n" +
            "}\n\n" +
            "protected override async Task OnPreLeaveAsync()\n" +
            "{\n" +
            "    _pageRequest?.Cancel();\n" +
            "    await base.OnPreLeaveAsync(); // Waits registered blockers.\n" +
            "}\n\n" +
            "protected override async Task OnLeaveAsync()\n" +
            "{\n" +
            "    _subscription?.Dispose();\n" +
            "    await base.OnLeaveAsync();\n" +
            "}\n\n" +
            "protected override void OnDestroy()\n" +
            "{\n" +
            "    try { _pageResource?.Dispose(); }\n" +
            "    finally { base.OnDestroy(); }\n" +
            "}",
            "PageLifecycle.cs");
        BlockingCode.SetSource(
            "private async Task SaveDraftAsync()\n" +
            "{\n" +
            "    if (ArcContext is null) return;\n\n" +
            "    // Navigation waits until this handle is disposed.\n" +
            "    using IDisposable blocker = ArcContext.KeepAliveBlocking.Block();\n" +
            "    await SaveDraftCoreAsync();\n" +
            "}\n\n" +
            "// LoadingContext.RunAsync and RunWithProgressAsync\n" +
            "// register and release the same blocker automatically.",
            "DelayedPageCleanup.cs");
    }

    private static string ReadExample(string name)
    {
        using var stream = typeof(ArcPageGalleryPage).Assembly.GetManifestResourceStream("Gallery.Example." + name) ?? throw new InvalidOperationException(name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private void NavigateMessage_Click(object sender, RoutedEventArgs e) => NavigateMessage();

    private void NavigateMessage()
    {
        var payload = new FrameworkPayload();
        payload.Set("message", MessageInput.Text);
        MessagePlaceholder.Visibility = Visibility.Collapsed;

        if (MessageNavigation.PageMap.TryGetValue(typeof(MessageExamplePage), out var target))
            target.NavigateEnter(payload);
        else
            MessageNavigation.Navigate(typeof(MessageExamplePage), payload);
    }

    private async void RunLoading_Click(object sender, RoutedEventArgs e) => await RunLoadingDemoAsync(showProgress: false);
    private async void RunProgress_Click(object sender, RoutedEventArgs e) => await RunLoadingDemoAsync(showProgress: true);

    private async Task RunLoadingDemoAsync(bool showProgress)
    {
        if (loadingDemoRunning || ArcContext?.LoadingContext is null)
            return;

        loadingDemoRunning = true;
        LoadingStatus.Text = GalleryStrings.Get("LoadingRunning");
        using var cancellation = new CancellationTokenSource();
        bool cancelled = false;

        if (showProgress)
        {
            await ArcContext.LoadingContext.RunWithProgressAsync(async (token, report) =>
            {
                try
                {
                    for (int step = 1; step <= 10; step++)
                    {
                        await Task.Delay(250, token);
                        report(step, 10);
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    cancelled = true;
                }
            }, 10, cancellation);
        }
        else
        {
            await ArcContext.LoadingContext.RunAsync(async token =>
            {
                try
                {
                    await Task.Delay(2500, token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    cancelled = true;
                }
            }, cancellation);
        }

        LoadingStatus.Text = GalleryStrings.Get(cancelled ? "LoadingCancelled" : "LoadingCompleted");
        loadingDemoRunning = false;
    }

    private void Try_Click(object sender, RoutedEventArgs e) => GalleryCatalog.Navigate("keepalive");
}
