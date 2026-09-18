using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XamlNexus.Gallery.MainPanel.Services;
using XamlNexus.Gallery.MainPanel.ViewModels;
using XamlNexus.Gallery.UIComponent.Utils;
namespace XamlNexus.Gallery.MainPanel.Gallery;

public sealed partial class ListExampleView : UserControl
{
    private readonly DemoDataSource source = new();
    private readonly GalleryItemsViewModel model;
    private readonly string instance = Guid.NewGuid().ToString("N")[..8];
    private readonly string modelInstance = Guid.NewGuid().ToString("N")[..8];
    private bool keepAlive;
    public ListExampleView()
    {
        model = new GalleryItemsViewModel(source);
        InitializeComponent(); DataContext = model;
        model.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(model.SearchText)) Trace("SearchText changed"); };
        Loaded += (_, _) => { LanguageUtil.LanguageUpdated += LanguageChanged; UpdateLabels(); };
        Unloaded += (_, _) => LanguageUtil.LanguageUpdated -= LanguageChanged;
    }
    public void Configure(bool cached) { keepAlive = cached; Slow.Visibility = Fail.Visibility = cached ? Visibility.Collapsed : Visibility.Visible; LifetimeSection.IsExpanded = false; LifetimeSection.Visibility = PersistenceTip.Visibility = InstanceLabel.Visibility = cached ? Visibility.Visible : Visibility.Collapsed; Integration.Kind = cached ? "KeepAlive" : "List"; Trace("Created"); UpdateLabels(); }
    public async Task ActivateAsync() { model.SetLanguage(LanguageUtil.CurrentLanguage); await model.ActivateAsync(); }
    public void Deactivate() => model.Deactivate();
    public void Trace(string action) => GalleryLifetime.Record(keepAlive, instance, modelInstance, action, model.SearchText);
    private void LanguageChanged(object? sender, EventArgs e) { model.SetLanguage(LanguageUtil.CurrentLanguage); UpdateLabels(); }
    private void UpdateLabels() { OptionsHeading.Text = GalleryStrings.Get(keepAlive ? "RetainedState" : "Options"); ExampleTitle.Text = GalleryStrings.Get(keepAlive ? "RetentionExample" : "ListExampleTitle"); Header.Description = GalleryStrings.Get(keepAlive ? "LifetimeDescription" : "ListDescription"); Header.Title = GalleryStrings.Get(keepAlive ? "LifetimeTitle" : "ListTitle"); TryDescription.Text = GalleryStrings.Get(keepAlive ? "TryCached" : "TryRegular"); InstanceLabel.Text = $"{GalleryStrings.Get("PageId")}{instance}\nKeepAlive = {keepAlive}"; }
    private void Slow_Click(object sender, RoutedEventArgs e) => source.Slow = Slow.IsChecked == true;
    private void Fail_Click(object sender, RoutedEventArgs e) => source.Fail = Fail.IsChecked == true;
    private sealed class DemoDataSource : IGalleryItemsDataSource
    {
        public bool Slow { get; set; }
        public bool Fail { get; set; }
        public async Task<IReadOnlyList<GalleryItemsItem>> LoadAsync(CancellationToken cancellationToken)
        {
            bool fail = Fail;
            await Task.Delay(Slow ? 3000 : 150, cancellationToken);
            if (fail) throw new InvalidOperationException(GalleryCatalog.Text("Simulated failure. Uncheck the option and refresh to retry.", "这是模拟失败。取消勾选后刷新即可重试。"));
            return new[] { new GalleryItemsItem(GalleryCatalog.Text("WinUI desktop", "WinUI 桌面"), GalleryCatalog.Text("Navigation and pages", "导航与页面")), new GalleryItemsItem(GalleryCatalog.Text("SQLite storage", "SQLite 存储"), GalleryCatalog.Text("Local data and recovery", "本地数据与恢复")), new GalleryItemsItem(GalleryCatalog.Text("Hybrid host", "混合宿主"), GalleryCatalog.Text("Background services", "后台服务")) };
        }
    }

}
