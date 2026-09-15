using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Winui3_XamlNexus.UI.Modules;

internal interface IXamlNexusModule {
    void ConfigureServices(IServiceCollection services);

    Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default);
}

internal sealed class XamlNexusModuleCatalog {
    private static readonly object RegistrationLock = new();
    private static readonly List<Func<IXamlNexusModule>> Registrations = [];
    private readonly IReadOnlyList<IXamlNexusModule> _modules;

    private XamlNexusModuleCatalog(IReadOnlyList<IXamlNexusModule> modules) {
        _modules = modules;
    }

    public static XamlNexusModuleCatalog Discover() {
        lock (RegistrationLock) {
            IXamlNexusModule[] modules = Registrations
                .Select(factory => factory())
                .OrderBy(module => module.GetType().FullName, StringComparer.Ordinal)
                .ToArray();
            return new XamlNexusModuleCatalog(modules);
        }
    }

    internal static void Register(Func<IXamlNexusModule> factory) {
        ArgumentNullException.ThrowIfNull(factory);
        lock (RegistrationLock)
            Registrations.Add(factory);
    }

    public void ConfigureServices(IServiceCollection services) {
        ArgumentNullException.ThrowIfNull(services);
        foreach (IXamlNexusModule module in _modules)
            module.ConfigureServices(services);
    }

    public async Task InitializeAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(services);
        foreach (IXamlNexusModule module in _modules)
            await module.InitializeAsync(services, cancellationToken);
    }
}
