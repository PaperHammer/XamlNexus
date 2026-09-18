using System;
using XamlNexus.Gallery.UIComponent.Templates;
namespace XamlNexus.Gallery.MainPanel.Gallery;

public sealed partial class GettingStartedGalleryPage : ArcPage
{
    public override Type ArcType => typeof(GettingStartedGalleryPage);
    public GettingStartedGalleryPage()
    {
        InitializeComponent();
        CreateCode.SetSource("dotnet tool install --global XamlNexus\nxamlnexus doctor --environment\nxamlnexus new MyApp\ncd MyApp\nxamlnexus run", "commands");
        AddCode.SetSource("xamlnexus page add Projects --kind list --dry-run\nxamlnexus page add Projects --kind list\nxamlnexus add sqlite\nxamlnexus run", "commands");
        MaintainCode.SetSource("xamlnexus doctor\nxamlnexus validate\nxamlnexus upgrade --dry-run", "commands");
    }
}
