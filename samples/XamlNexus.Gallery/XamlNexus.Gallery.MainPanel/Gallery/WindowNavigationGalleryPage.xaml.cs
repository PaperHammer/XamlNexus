using System;
using XamlNexus.Gallery.UIComponent.Templates;

namespace XamlNexus.Gallery.MainPanel.Gallery;

public sealed partial class WindowNavigationGalleryPage : ArcPage
{
    public override Type ArcType => typeof(WindowNavigationGalleryPage);

    public WindowNavigationGalleryPage()
    {
        InitializeComponent();
        Section.Configure("NavigationFundamentals", "NavigationFundamentalsDescription", ["arc-window", "arc-page", "keepalive"]);
    }
}
