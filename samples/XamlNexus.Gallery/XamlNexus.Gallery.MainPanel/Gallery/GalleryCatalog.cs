using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using XamlNexus.Gallery.UIComponent.Utils;

namespace XamlNexus.Gallery.MainPanel.Gallery;

[Microsoft.UI.Xaml.Data.Bindable]
public sealed partial class GalleryEntry(string id, string titleKey, string descriptionKey, string glyph, Type pageType) : INotifyPropertyChanged {
    public string Id { get; } = id;
    public string TitleKey { get; } = titleKey;
    public string DescriptionKey { get; } = descriptionKey;
    public string Glyph { get; } = glyph;
    public Type PageType { get; } = pageType;
    public event PropertyChangedEventHandler? PropertyChanged;
    public void UpdateLanguage() { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title))); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Description))); }
    public string Title => GalleryStrings.Get(TitleKey);
    public string Description => GalleryStrings.Get(DescriptionKey);
}

/// <summary>首页卡片、导航与搜索共用目录，新增示例只需维护一处元数据。 / Home cards, navigation and search share one catalog; maintain sample metadata in one place.</summary>
public static class GalleryCatalog {
    static GalleryCatalog() { LanguageUtil.LanguageUpdated += (_, _) => { foreach (var entry in Entries) entry.UpdateLanguage(); }; }

    public static IReadOnlyList<GalleryEntry> Entries { get; } = new[] {
        new GalleryEntry("getting-started", "GettingStarted", "GettingStartedDescription", "\uE768", typeof(GettingStartedGalleryPage)),
        new GalleryEntry("tool-guide", "ToolGuide", "ToolGuideDescription", "\uE82D", typeof(ToolGuidePage)),
        new GalleryEntry("arc-window", "WindowCard", "WindowDescription", "\uE737", typeof(ArcWindowGalleryPage)),
        new GalleryEntry("arc-page", "PageCard", "PageDescription", "\uE8A5", typeof(ArcPageGalleryPage)),
        new GalleryEntry("keepalive", "LifetimeTitle", "LifetimeDescription", "\uE81C", typeof(KeepAliveGalleryPage)),
        new GalleryEntry("list", "ListTitle", "ListDescription", "\uE8FD", typeof(ListGalleryPage)),
        new GalleryEntry("sqlite", "DataTitle", "DataDescription", "\uE8F1", typeof(MainPage)),
    };

    // 首页只在重新进入时读取快照；避免 ObservableCollection 在页面卸载期间回调已销毁的 WinUI 元素。
    // The home page reads this snapshot when entered; avoid callbacks into WinUI elements while they are unloading.
    private static readonly List<GalleryEntry> recent = new();
    public static IReadOnlyList<GalleryEntry> Recent => recent;
    public static void RecordVisit(string id) {
        var entry = Entries.FirstOrDefault(item => item.Id == id);
        if (entry is null) return;
        recent.Remove(entry);
        recent.Insert(0, entry);
        while (recent.Count > 3) recent.RemoveAt(recent.Count - 1);
    }
    public static event Action<string>? NavigationRequested;
    public static void Navigate(string id) => NavigationRequested?.Invoke(id);
}
