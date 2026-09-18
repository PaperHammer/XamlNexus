using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.UI.Xaml.Data;
using XamlNexus.Gallery.UIComponent.Utils;

namespace XamlNexus.Gallery.MainPanel.Gallery;

[Microsoft.UI.Xaml.Data.Bindable]
public sealed class GalleryEntry(string id, string englishTitle, string chineseTitle, string englishDescription, string chineseDescription, string glyph, Type pageType) : INotifyPropertyChanged
{
    public string Id { get; } = id;
    public string EnglishTitle { get; } = englishTitle;
    public string ChineseTitle { get; } = chineseTitle;
    public string EnglishDescription { get; } = englishDescription;
    public string ChineseDescription { get; } = chineseDescription;
    public string Glyph { get; } = glyph;
    public Type PageType { get; } = pageType;
    public event PropertyChangedEventHandler? PropertyChanged;
    public void UpdateLanguage() { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title))); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Description))); }
    public string Title => GalleryCatalog.Text(EnglishTitle, ChineseTitle);
    public string Description => GalleryCatalog.Text(EnglishDescription, ChineseDescription);
}

/// <summary>首页卡片、导航与搜索共用目录，新增示例只需维护一处元数据。</summary>
public static class GalleryCatalog
{
    static GalleryCatalog() { LanguageUtil.LanguageUpdated += (_, _) => { foreach (var entry in Entries) entry.UpdateLanguage(); }; }
    public static string Text(string english, string chinese) =>
        LanguageUtil.CurrentLanguage?.StartsWith("zh", StringComparison.OrdinalIgnoreCase) == true ? chinese : english;

    public static IReadOnlyList<GalleryEntry> Entries { get; } = new[] {
        new GalleryEntry("list", "Lists & async loading", "列表与异步加载", "Search, refresh, loading and failure recovery.", "体验搜索、刷新、加载与失败重试。", "\uE8FD", typeof(ListGalleryPage)),
        new GalleryEntry("keepalive", "Page retention", "页面保活", "Keep search and loaded data with [KeepAlive].", "使用 [KeepAlive] 保留搜索和已加载数据。", "\uE81C", typeof(KeepAliveGalleryPage)),
        new GalleryEntry("sqlite", "Data & recovery", "数据与恢复", "Edit records, back up data and restore a database.", "编辑记录、备份数据并恢复数据库。", "\uE8F1", typeof(MainPage)),
        new GalleryEntry("getting-started", "Getting started", "快速开始", "Create, diagnose and grow a desktop application.", "创建、诊断并逐步扩展桌面应用。", "\uE768", typeof(GettingStartedGalleryPage)),
    };

    public static event Action<string>? NavigationRequested;
    public static void Navigate(string id) => NavigationRequested?.Invoke(id);
}
