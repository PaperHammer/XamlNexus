using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using XamlNexus.Gallery.UIComponent.Templates;
namespace XamlNexus.Gallery.MainPanel.Gallery;

public sealed partial class GalleryHomePage : ArcPage
{
    public override Type ArcType => typeof(GalleryHomePage);
    public GalleryHomePage()
    {
        InitializeComponent();
        Commands.SetSource("xamlnexus new MyApp\ncd MyApp\nxamlnexus run", "commands");
        UpdateSamples();
        UpdateRecent();
    }
    // 根据实际内容宽度重排，窄窗口仍保留可复制命令。 / Reflow using content width; keep commands available in narrow windows.
    private void Hero_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        bool wide = e.NewSize.Width >= 900;
        ConsoleColumn.Width = new GridLength(wide ? 340 : 0);
        Grid.SetColumn(HeroConsole, wide ? 1 : 0);
        Grid.SetRow(HeroConsole, wide ? 0 : 1);
    }
    private void UpdateRecent() => RecentSection.Visibility = GalleryCatalog.Recent.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    private void SampleFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateSamples();
    private void UpdateSamples()
    {
        if (SampleItems is null) return;
        string? filter = (SampleFilter.SelectedItem as ComboBoxItem)?.Tag as string;
        SampleItems.ItemsSource = GalleryCatalog.Entries.Where(entry => entry.Id is not ("getting-started" or "tool-guide"))
            .Where(entry => filter switch {
                "framework" => entry.Id is "arc-window" or "arc-page" or "keepalive",
                "data" => entry.Id is "list" or "sqlite",
                _ => true,
            }).ToArray();
    }
    private void Navigate_Click(object sender, RoutedEventArgs e) => GalleryCatalog.Navigate((string)((ButtonBase)sender).Tag);
}
