using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Winui3_XamlNexus.AppSettingsPanel;
using Winui3_XamlNexus.AppSettingsPanel.ViewModels;
using Winui3_XamlNexus.UIComponent.Navigation;

namespace Winui3_XamlNexus.UI.Modules;

internal sealed class SettingsModule : IXamlNexusModule {
    [ModuleInitializer]
    internal static void Register() {
        XamlNexusModuleCatalog.Register(static () => new SettingsModule());
        NavigationRegistry.Default.Register(new NavigationEntry(
            "settings", typeof(AppSettings), "Settings", "SidebarSettings", "\uE713", IsFooter: true));
    }
    public void ConfigureServices(IServiceCollection services) {
        services.AddSingleton<GeneralSettingViewModel>();
        services.AddSingleton<SystemSettingViewModel>();
    }
    public Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default) => Task.CompletedTask;
}