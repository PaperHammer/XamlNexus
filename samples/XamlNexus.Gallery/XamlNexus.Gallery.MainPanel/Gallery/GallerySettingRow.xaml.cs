using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
namespace XamlNexus.Gallery.MainPanel.Gallery;

[ContentProperty(Name = nameof(EditorContent))]
public sealed partial class GallerySettingRow : UserControl
{
    public GallerySettingRow() => InitializeComponent();
    public string Glyph { get => (string)GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(nameof(Glyph), typeof(string), typeof(GallerySettingRow), new PropertyMetadata(""));
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(GallerySettingRow), new PropertyMetadata(""));
    public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(GallerySettingRow), new PropertyMetadata(""));
    public object EditorContent { get => (object)GetValue(EditorContentProperty); set => SetValue(EditorContentProperty, value); }
    public static readonly DependencyProperty EditorContentProperty = DependencyProperty.Register(nameof(EditorContent), typeof(object), typeof(GallerySettingRow), new PropertyMetadata(null));
}
