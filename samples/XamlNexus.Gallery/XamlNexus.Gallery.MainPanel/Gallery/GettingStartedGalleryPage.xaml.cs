using System;
using Microsoft.UI.Xaml;
using XamlNexus.Gallery.UIComponent.Templates;
namespace XamlNexus.Gallery.MainPanel.Gallery;

public sealed partial class GettingStartedGalleryPage : ArcPage
{
    public override Type ArcType => typeof(GettingStartedGalleryPage);
    public GettingStartedGalleryPage()
    {
        InitializeComponent();
        InstallCode.SetSource("dotnet tool install --global XamlNexus\nxamlnexus doctor --environment", "commands");
        CreateCode.SetSource("xamlnexus new MyApp\ncd MyApp\nxamlnexus run", "commands");
        AddCode.SetSource("xamlnexus page add Projects --kind list --dry-run\nxamlnexus page add Projects --kind list\nxamlnexus run", "commands");
        MaintainCode.SetSource("xamlnexus add sqlite --dry-run\nxamlnexus add sqlite\nxamlnexus validate\nxamlnexus doctor\nxamlnexus status", "commands");
    }
    private void Guide_Click(object sender, RoutedEventArgs e) => GalleryCatalog.Navigate("tool-guide");
}
