using System.Runtime.CompilerServices;
using Winui3_Wpf_XamlNexus.MainPanel;

using Winui3_Wpf_XamlNexus.UIComponent.Navigation;

namespace Winui3_Wpf_XamlNexus.UI.Navigation;

internal static class BuiltInNavigation {
    [ModuleInitializer]
    internal static void Register() {
        NavigationRegistry.Default.Register(new NavigationEntry(
            "home", typeof(MainPage), "Home", "SidebarHomePage", "\uE80F", Order: -100));
    }
}
