using Winui3_XamlNexus.UIComponent.Navigation;
using Xunit;

namespace XamlNexus.TemplateTests;

public sealed class NavigationRegistryTests {
    [Fact]
    public void RoutesResolveIndependentlyOfTitlesAndMenuOrder() {
        INavigationRegistry registry = new NavigationRegistry();
        registry.Register(new("orders", typeof(string), "Same title", Order: 20));
        registry.Register(new("home", typeof(object), "Same title", Order: -100));
        Assert.Equal(typeof(string), registry.Resolve("orders"));
        Assert.Null(registry.Resolve("missing"));
        Assert.Equal(new[] { "home", "orders" }, registry.Entries.Select(entry => entry.Route));
    }

    [Fact]
    public void DuplicateRouteFailsWithoutReplacingOriginal() {
        var registry = new NavigationRegistry();
        registry.Register(new("home", typeof(object), "Home"));
        Assert.Throws<InvalidOperationException>(() => registry.Register(new("home", typeof(string), "Other")));
        Assert.Equal(typeof(object), registry.Resolve("home"));
    }

    [Fact]
    public void SnapshotDoesNotExposeMutableRegistryState() {
        var registry = new NavigationRegistry();
        registry.Register(new("home", typeof(object), "Home"));
        var snapshot = registry.Entries;
        registry.Register(new("settings", typeof(string), "Settings", IsFooter: true));
        Assert.Single(snapshot);
        Assert.Equal(2, registry.Entries.Count);
        Assert.True(registry.Entries.Single(entry => entry.Route == "settings").IsFooter);
    }
}
