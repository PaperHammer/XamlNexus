using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XamlNexus.Gallery.UIComponent.Templates;
using XamlNexus.Gallery.UIComponent.Utils;
namespace XamlNexus.Gallery.MainPanel.Gallery;

public sealed partial class GalleryHomePage : ArcPage
{
    public override Type ArcType => typeof(GalleryHomePage);
    public GalleryHomePage() { InitializeComponent(); Commands.SetSource("xamlnexus doctor --environment\nxamlnexus new MyApp\ncd MyApp\nxamlnexus page add Projects --kind list\nxamlnexus run", "commands"); }
    private void Navigate_Click(object sender, RoutedEventArgs e) => GalleryCatalog.Navigate((string)((Button)sender).Tag);
    private async void Link_Click(object sender, RoutedEventArgs e) { try { await Windows.System.Launcher.LaunchUriAsync(new Uri((string)((Button)sender).Tag)); } catch (Exception error) { GlobalMessageUtil.ShowException(error); } }
}
