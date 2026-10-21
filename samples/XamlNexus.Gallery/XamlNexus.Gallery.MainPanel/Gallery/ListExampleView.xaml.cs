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
    public ListExampleView()
    {
        model = new GalleryItemsViewModel(source);
        InitializeComponent(); DataContext = model;
        Loaded += (_, _) => { LanguageUtil.LanguageUpdated += LanguageChanged; UpdateLabels(); };
        Unloaded += (_, _) => LanguageUtil.LanguageUpdated -= LanguageChanged;
    }
    public async Task ActivateAsync() { model.SetLanguage(LanguageUtil.CurrentLanguage); await model.ActivateAsync(); }
    public void Deactivate() => model.Deactivate();
    private void LanguageChanged(object? sender, EventArgs e) { model.SetLanguage(LanguageUtil.CurrentLanguage); UpdateLabels(); }
    private void UpdateLabels() { OptionsHeading.Text = GalleryStrings.Get("Options"); ExampleTitle.Text = GalleryStrings.Get("ListExampleTitle"); Header.Description = GalleryStrings.Get("ListDescription"); Header.Title = GalleryStrings.Get("ListTitle"); TryDescription.Text = GalleryStrings.Get("ListTrySteps"); }
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
            if (fail) throw new InvalidOperationException(GalleryStrings.Get("DemoFailureMessage"));
            return new[] {
                new GalleryItemsItem(GalleryStrings.Get("DemoWinuiTitle"), GalleryStrings.Get("DemoWinuiDescription")),
                new GalleryItemsItem(GalleryStrings.Get("DemoSqliteTitle"), GalleryStrings.Get("DemoSqliteDescription")),
                new GalleryItemsItem(GalleryStrings.Get("DemoHybridTitle"), GalleryStrings.Get("DemoHybridDescription")),
            };
        }
    }

}
