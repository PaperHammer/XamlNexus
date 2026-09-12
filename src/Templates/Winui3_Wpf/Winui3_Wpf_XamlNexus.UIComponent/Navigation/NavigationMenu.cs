using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winui3_Wpf_XamlNexus.UIComponent.Utils;

namespace Winui3_Wpf_XamlNexus.UIComponent.Navigation;

/// <summary>Projects startup registrations into a WinUI menu. Dispose with the host window.</summary>
public sealed class NavigationMenu : IDisposable {
    private readonly NavigationView _view;
    private readonly List<(NavigationEntry Entry, NavigationViewItem Item)> _items = [];

    public NavigationMenu(NavigationView view, INavigationRegistry registry) {
        _view = view;
        foreach (var entry in registry.Entries) {
            var item = new NavigationViewItem { Tag = entry.Route };
            if (entry.Glyph is not null)
                item.Icon = new FontIcon { Glyph = entry.Glyph };
            _items.Add((entry, item));
            if (entry.IsFooter) view.FooterMenuItems.Add(item);
            else view.MenuItems.Add(item);
        }
        UpdateTitles(null, EventArgs.Empty);
        LanguageUtil.LanguageUpdated += UpdateTitles;
    }

    public void Select(string route) {
        foreach (var pair in _items)
            if (pair.Entry.Route == route) {
                _view.SelectedItem = pair.Item;
                return;
            }
    }

    private void UpdateTitles(object? sender, EventArgs args) {
        foreach (var pair in _items) {
            string title = pair.Entry.TitleResourceKey is null
                ? pair.Entry.Title : LanguageUtil.GetI18n(pair.Entry.TitleResourceKey);
            if (string.IsNullOrEmpty(title) || title == pair.Entry.TitleResourceKey)
                title = pair.Entry.Title;
            pair.Item.Content = title;
            ToolTipService.SetToolTip(pair.Item, title);
        }
    }

    public void Dispose() {
        LanguageUtil.LanguageUpdated -= UpdateTitles;
        foreach (var pair in _items) {
            if (pair.Entry.IsFooter) _view.FooterMenuItems.Remove(pair.Item);
            else _view.MenuItems.Remove(pair.Item);
        }
        _items.Clear();
    }
}
