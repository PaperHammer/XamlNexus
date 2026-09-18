using System;
using System.Collections.ObjectModel;
namespace XamlNexus.Gallery.MainPanel.Gallery;

[Microsoft.UI.Xaml.Data.Bindable]
public sealed class LifetimeEntry(string time, string action, string pageId, string viewModelId, string query)
{
    public string Time { get; } = time;
    public string Action { get; } = action;
    public string PageId { get; } = pageId;
    public string ViewModelId { get; } = viewModelId;
    public string Query { get; } = query;
}
/// <summary>只保存文本数据，不持有被观察的页面或 ViewModel。</summary>
public static class GalleryLifetime
{
    public static ObservableCollection<LifetimeEntry> Regular { get; } = new();
    public static ObservableCollection<LifetimeEntry> Cached { get; } = new();
    public static void Record(bool keepAlive, string page, string model, string action, string query)
    {
        var entries = keepAlive ? Cached : Regular;
        entries.Insert(0, new LifetimeEntry(DateTime.Now.ToString("HH:mm:ss.fff"), action, page, model, query));
        while (entries.Count > 20) entries.RemoveAt(entries.Count - 1);
    }
    public static void Clear() { Regular.Clear(); Cached.Clear(); }
}
