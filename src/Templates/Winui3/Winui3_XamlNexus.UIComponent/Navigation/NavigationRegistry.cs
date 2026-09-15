using System;
using System.Collections.Generic;
using System.Linq;

namespace Winui3_XamlNexus.UIComponent.Navigation;

public sealed class NavigationRegistry : INavigationRegistry {
    public static INavigationRegistry Default { get; } = new NavigationRegistry();
    private readonly Dictionary<string, NavigationEntry> _entries = new(StringComparer.Ordinal);
    private readonly object _lock = new();

    public void Register(NavigationEntry entry) {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.Route);
        ArgumentNullException.ThrowIfNull(entry.PageType);
        lock (_lock) {
            if (!_entries.TryAdd(entry.Route, entry))
                throw new InvalidOperationException($"Navigation route '{entry.Route}' is already registered.");
        }
    }

    public IReadOnlyList<NavigationEntry> Entries {
        get {
            lock (_lock)
                return _entries.Values.OrderBy(entry => entry.Order)
                    .ThenBy(entry => entry.Route, StringComparer.Ordinal).ToArray();
        }
    }

    public Type? Resolve(string route) {
        lock (_lock)
            return _entries.TryGetValue(route, out var entry) ? entry.PageType : null;
    }
}
