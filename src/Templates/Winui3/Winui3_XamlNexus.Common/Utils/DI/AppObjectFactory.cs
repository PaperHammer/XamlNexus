using System;
using Microsoft.Extensions.DependencyInjection;

namespace Winui3_XamlNexus.Common.Utils.DI;

/// <summary>
/// Creates a new caller-owned object using application services for constructor arguments.
/// The object itself does not need registration and is not owned by the service provider.
/// </summary>
public static class AppObjectFactory {
    public static T Create<T>() where T : class => (T)Create(typeof(T));

    public static object Create(Type type) => Create(AppServiceLocator.Services, type);

    public static object Create(IServiceProvider services, Type type) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(type);
        return ActivatorUtilities.CreateInstance(services, type);
    }
}
