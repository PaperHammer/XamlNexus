using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace XamlNexus.Gallery.MainPanel.Gallery;

public sealed partial class GalleryLifetimeView : UserControl
{
    public GalleryLifetimeView() => InitializeComponent();
    private void Navigate_Click(object sender, RoutedEventArgs e) => GalleryCatalog.Navigate((string)((Button)sender).Tag);
    private void Clear_Click(object sender, RoutedEventArgs e) => GalleryLifetime.Clear();
}
