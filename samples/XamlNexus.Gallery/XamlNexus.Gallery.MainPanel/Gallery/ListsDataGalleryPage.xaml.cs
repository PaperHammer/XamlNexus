using System;
using XamlNexus.Gallery.UIComponent.Templates;

namespace XamlNexus.Gallery.MainPanel.Gallery;

public sealed partial class ListsDataGalleryPage : ArcPage
{
    public override Type ArcType => typeof(ListsDataGalleryPage);

    public ListsDataGalleryPage()
    {
        InitializeComponent();
        Section.Configure("NavigationCapabilities", "NavigationCapabilitiesDescription", ["list", "sqlite"]);
    }
}
