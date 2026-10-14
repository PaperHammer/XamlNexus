using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using XamlNexus.Gallery.UIComponent.Utils;
namespace XamlNexus.Gallery.MainPanel.Gallery;

[Microsoft.UI.Xaml.Data.Bindable]
public sealed class GalleryEntry(string id, string titleKey, string descriptionKey, string glyph, Type pageType, string category, string keywords = "", string? navigationTitleKey = null) : INotifyPropertyChanged {
    public string Id { get; } = id;
    public string TitleKey { get; } = titleKey;
    public string DescriptionKey { get; } = descriptionKey;
    public string Glyph { get; } = glyph;
    public Type PageType { get; } = pageType;
    public string Category { get; } = category;
    public string Keywords { get; } = keywords;
    public string Title => GalleryStrings.Get(TitleKey);
    public string NavigationTitle => GalleryStrings.Get(navigationTitleKey ?? TitleKey);
    public string Description => GalleryStrings.Get(DescriptionKey);
    public event PropertyChangedEventHandler? PropertyChanged;
    public void UpdateLanguage() {
        PropertyChanged?.Invoke(this, new(nameof(Title)));
        PropertyChanged?.Invoke(this, new(nameof(Description)));
        PropertyChanged?.Invoke(this, new(nameof(NavigationTitle)));
    }
}

public static class GalleryCatalog {
    static GalleryCatalog() => LanguageUtil.LanguageUpdated += (_, _) => {
        foreach (var entry in Entries) entry.UpdateLanguage();
    };
    public static IReadOnlyList<GalleryEntry> Entries { get; } = new[] {
        new GalleryEntry("getting-started", "GettingStarted", "GettingStartedDescription", "\uE768", typeof(GettingStartedGalleryPage), "reference", "new run install 创建 运行 安装", navigationTitleKey: "NavigationGettingStarted"),
        new GalleryEntry("business-pages", "BusinessPagesTitle", "BusinessPagesDescription", "\uE8A5", typeof(BusinessPagesGalleryPage), "workflow", "blank list details form activation route payload JSON 未保存 激活 参数 页面 详情 表单", navigationTitleKey: "NavigationBusinessPages"),
        new GalleryEntry("components", "ComponentsTitle", "ComponentsDescription", "\uE74C", typeof(ComponentsGalleryPage), "workflow", "recipes settings sqlite tray updater editorconfig 组件", navigationTitleKey: "NavigationComponents"),
        new GalleryEntry("evolution", "EvolutionTitle", "EvolutionDescription", "\uE9F9", typeof(ProjectEvolutionGalleryPage), "workflow", "status update upgrade merge conflict 演进 升级 合并 冲突", navigationTitleKey: "NavigationEvolution"),
        new GalleryEntry("sqlite", "DataTitle", "DataDescription", "\uE8F1", typeof(MainPage), "data", "sqlite backup restore 数据 备份 恢复", navigationTitleKey: "NavigationData"),
        new GalleryEntry("list", "ListTitle", "ListDescription", "\uE8FD", typeof(ListGalleryPage), "mechanics", "list async cancel filter 异步 列表 取消", navigationTitleKey: "NavigationList"),
        new GalleryEntry("arc-window", "WindowCard", "WindowDescription", "\uE737", typeof(ArcWindowGalleryPage), "mechanics", "ArcWindow titlebar window 窗口 标题栏", navigationTitleKey: "NavigationWindow"),
        new GalleryEntry("arc-page", "PageCard", "PageDescription", "\uE8A5", typeof(ArcPageGalleryPage), "mechanics", "ArcPage payload loading lifecycle 导航 加载 生命周期", navigationTitleKey: "NavigationPage"),
        new GalleryEntry("keepalive", "LifetimeTitle", "LifetimeDescription", "\uE81C", typeof(KeepAliveGalleryPage), "mechanics", "KeepAlive retention draft 草稿 保活", navigationTitleKey: "NavigationRetention"),
        new GalleryEntry("tool-guide", "ToolGuide", "ToolGuideDescription", "\uE82D", typeof(ToolGuidePage), "reference", "cli commands hybrid profile activation single instance close 单实例 激活 关闭 参数 命令 架构", navigationTitleKey: "NavigationCommands"),
    };
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
