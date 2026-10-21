using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using XamlNexus.Gallery.UIComponent.Templates;

namespace XamlNexus.Gallery.UI;

[ContentProperty(Name = nameof(RootContent))]
public sealed partial class GalleryShell : UserControl, IWindowSurface {
    public GalleryShell() => InitializeComponent();
    public Grid AppRoot => Root;
    public Grid AppTitleBar => TitleBar;
    public Image AppThemeTransitionImage => ThemeTransition;
    public IReadOnlyList<FrameworkElement> TitleBarChildren => new FrameworkElement[] { AppTitle };
    public Button Back => BackButton;
    public Button Menu => MenuButton;
    public object RootContent { get => GetValue(RootContentProperty); set => SetValue(RootContentProperty, value); }
    public static readonly DependencyProperty RootContentProperty = DependencyProperty.Register(
        nameof(RootContent), typeof(object), typeof(GalleryShell), new PropertyMetadata(null));
    public object SearchContent { get => GetValue(SearchContentProperty); set => SetValue(SearchContentProperty, value); }
    public static readonly DependencyProperty SearchContentProperty = DependencyProperty.Register(
        nameof(SearchContent), typeof(object), typeof(GalleryShell), new PropertyMetadata(null));
    public void SetCaptionInsets(double left, double right) {
        LeftInset.Width = new GridLength(left);
        RightInset.Width = new GridLength(right);
    }
}
