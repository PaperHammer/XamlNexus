using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace XamlNexus.Gallery.UIComponent.Templates;

/// <summary>窗口生命周期需要的表面契约；Gallery 可拥有独立的视觉外壳。</summary>
public interface IWindowSurface {
    Grid AppRoot { get; }
    Grid AppTitleBar { get; }
    Image AppThemeTransitionImage { get; }
    ElementTheme RequestedTheme { get; set; }
    IReadOnlyList<FrameworkElement> TitleBarChildren { get; }
}
