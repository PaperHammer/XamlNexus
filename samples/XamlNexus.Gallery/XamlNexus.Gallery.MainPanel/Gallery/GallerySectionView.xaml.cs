using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XamlNexus.Gallery.UIComponent.Utils;

namespace XamlNexus.Gallery.MainPanel.Gallery;

public sealed partial class GallerySectionView : UserControl
{
    private string titleKey = string.Empty;
    private string descriptionKey = string.Empty;
    private bool isSubscribed;

    public GallerySectionView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public void Configure(string titleResourceKey, string descriptionResourceKey, IReadOnlyCollection<string> entryIds)
    {
        titleKey = titleResourceKey;
        descriptionKey = descriptionResourceKey;
        SectionItems.ItemsSource = GalleryCatalog.Entries.Where(entry => entryIds.Contains(entry.Id)).ToArray();
        UpdateText();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!isSubscribed)
        {
            LanguageUtil.LanguageUpdated += LanguageUpdated;
            isSubscribed = true;
        }
        UpdateText();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (!isSubscribed) return;
        LanguageUtil.LanguageUpdated -= LanguageUpdated;
        isSubscribed = false;
    }

    private void LanguageUpdated(object? sender, EventArgs e) => UpdateText();

    private void UpdateText()
    {
        SectionTitleText.Text = GalleryStrings.Get(titleKey);
        SectionDescriptionText.Text = GalleryStrings.Get(descriptionKey);
        BrowseTitle.Text = GalleryStrings.Get("SectionBrowseTitle");
    }

    private void Navigate_Click(object sender, RoutedEventArgs e) => GalleryCatalog.Navigate((string)((Button)sender).Tag);
}
