using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using XamlNexus.Gallery.UIComponent.Templates;
namespace XamlNexus.Gallery.MainPanel.Gallery;
public sealed partial class GalleryHomePage : ArcPage {
    public override Type ArcType => typeof(GalleryHomePage);
    public GalleryHomePage() {
        InitializeComponent();
        Commands.SetSource("xamlnexus new MyApp\ncd MyApp\nxamlnexus run", "PowerShell");
        RecentSection.Visibility = GalleryCatalog.Recent.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }
    private void Hero_SizeChanged(object sender, SizeChangedEventArgs e) {
        bool wide = e.NewSize.Width >= 850;
        PreviewColumn.Width = new GridLength(wide ? 320 : 0);
        Grid.SetColumn(HeroPreview, wide ? 1 : 0);
        Grid.SetRow(HeroPreview, wide ? 0 : 1);
    }
    private void Navigate_Click(object sender, RoutedEventArgs e) => GalleryCatalog.Navigate((string)((ButtonBase)sender).Tag);
}
