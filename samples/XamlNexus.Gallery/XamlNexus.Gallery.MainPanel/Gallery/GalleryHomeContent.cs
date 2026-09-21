using System;
using System.Collections.Generic;
using System.ComponentModel;
using XamlNexus.Gallery.UIComponent.Utils;
namespace XamlNexus.Gallery.MainPanel.Gallery;

[Microsoft.UI.Xaml.Data.Bindable]
public sealed class GallerySpotlight(string titleKey, string descriptionKey, string glyph, string id) : INotifyPropertyChanged
{
    public string Title => GalleryStrings.Get(titleKey);
    public string Description => GalleryStrings.Get(descriptionKey);
    public string Glyph { get; } = glyph;
    public string Id { get; } = id;
    public event PropertyChangedEventHandler? PropertyChanged;
    internal void UpdateLanguage()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Description)));
    }
}

public static class GalleryHomeContent
{
    public static IReadOnlyList<GallerySpotlight> Highlights { get; } = new[] {
        new GallerySpotlight("Highlight1Title", "Highlight1Body", "\uE80F", "getting-started"),
        new GallerySpotlight("Highlight2Title", "Highlight2Body", "\uE74C", "tool-guide"),
        new GallerySpotlight("Highlight3Title", "Highlight3Body", "\uE9F9", "tool-guide"),
    };
    static GalleryHomeContent() => LanguageUtil.LanguageUpdated += (_, _) => {
        foreach (var item in Highlights) item.UpdateLanguage();
    };
}
