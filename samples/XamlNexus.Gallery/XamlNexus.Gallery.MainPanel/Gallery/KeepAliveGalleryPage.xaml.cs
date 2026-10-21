using System;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using XamlNexus.Gallery.UIComponent.Attributes;
using XamlNexus.Gallery.UIComponent.Templates;

namespace XamlNexus.Gallery.MainPanel.Gallery;

[KeepAlive]
public sealed partial class KeepAliveGalleryPage : ArcPage
{
    public override Type ArcType => typeof(KeepAliveGalleryPage);
    private Type selectedPage = typeof(RegularRetentionPage);
    private Type? currentPage;

    public KeepAliveGalleryPage()
    {
        InitializeComponent();
        RetentionCode.SetSource(
            "[KeepAlive]\n" +
            "public sealed partial class DraftPage : ArcPage\n" +
            "{\n" +
            "    public override Type ArcType => typeof(DraftPage);\n" +
            "}\n\n" +
            "// Return through the same ArcNavigationContentView.\n" +
            "Pages.Navigate(typeof(DraftPage));",
            "DraftPage.cs");
        Loaded += (_, _) =>
        {
            if (currentPage is null)
                OpenSelectedDraft();
        };
    }

    private void ModeSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (IsLoaded)
            OpenSelectedDraft();
    }

    private void OpenSelectedDraft()
    {
        selectedPage = ModeSwitch.IsOn ? typeof(CachedRetentionPage) : typeof(RegularRetentionPage);
        Navigate(selectedPage);
        LeaveButton.IsEnabled = true;
        ReturnButton.IsEnabled = false;
    }

    private void LeaveDraft_Click(object sender, RoutedEventArgs e)
    {
        Navigate(typeof(DepartureRetentionPage));
        LeaveButton.IsEnabled = false;
        ReturnButton.IsEnabled = true;
    }

    private void ReturnDraft_Click(object sender, RoutedEventArgs e)
    {
        Navigate(selectedPage);
        LeaveButton.IsEnabled = true;
        ReturnButton.IsEnabled = false;
    }

    private void Navigate(Type page)
    {
        if (currentPage == page)
            return;

        double verticalOffset = PageScroll.VerticalOffset;
        currentPage = page;
        DemoNavigation.Navigate(page);
        PageScroll.ChangeView(null, verticalOffset, null, disableAnimation: true);
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
            PageScroll.ChangeView(null, verticalOffset, null, disableAnimation: true));
    }
}
