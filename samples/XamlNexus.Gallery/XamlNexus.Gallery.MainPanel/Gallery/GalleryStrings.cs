using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using XamlNexus.Gallery.UIComponent.Utils;
namespace XamlNexus.Gallery.MainPanel.Gallery;

/// <summary>中英文资源通过索引绑定原位更新；弱引用避免资源字典订阅延长页面保活。</summary>
[Microsoft.UI.Xaml.Data.Bindable]
public sealed class GalleryStrings : INotifyPropertyChanged
{
    private static readonly Dictionary<string, string> English = Read("en-US");
    private static readonly Dictionary<string, string> Chinese = Read("zh-CN");
    private static readonly List<WeakReference<GalleryStrings>> instances = new();
    static GalleryStrings()
    {
        LanguageUtil.LanguageUpdated += (_, _) =>
        {
            for (int i = instances.Count - 1; i >= 0; i--)
            {
                if (instances[i].TryGetTarget(out var value)) value.PropertyChanged?.Invoke(value, new PropertyChangedEventArgs("Item[]"));
                else instances.RemoveAt(i);
            }
        };
    }
    public GalleryStrings() => instances.Add(new WeakReference<GalleryStrings>(this));
    public string this[string key] => Get(key);
    public static string Get(string key)
    {
        var values = LanguageUtil.CurrentLanguage?.StartsWith("zh", StringComparison.OrdinalIgnoreCase) == true ? Chinese : English;
        return values.TryGetValue(key, out var value) ? value : key;
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private static Dictionary<string, string> Read(string language)
    {
        using var stream = typeof(GalleryStrings).Assembly.GetManifestResourceStream("Gallery.Strings." + language + ".json")
            ?? throw new InvalidOperationException("Missing Gallery language: " + language);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
    }
}
public sealed class BooleanVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value is true ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
