using System;
using System.Collections.Generic;

namespace Winui3_Wpf_XamlNexus.UIComponent.Navigation;

/// <summary>Startup registrations shared by standard and custom navigation hosts.</summary>
public interface INavigationRegistry {
    void Register(NavigationEntry entry);
    IReadOnlyList<NavigationEntry> Entries { get; }
    Type? Resolve(string route);
}

public sealed record NavigationEntry(
    string Route,
    Type PageType,
    string Title,
    string? TitleResourceKey = null,
    string? Glyph = null,
    int Order = 0,
    bool IsFooter = false);
