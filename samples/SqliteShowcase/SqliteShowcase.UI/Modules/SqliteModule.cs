using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SqliteShowcase.Data.Persistence;

namespace SqliteShowcase.UI.Modules;

internal sealed class SqliteModule : IXamlNexusModule {
    [ModuleInitializer]
    internal static void Register() =>
        XamlNexusModuleCatalog.Register(static () => new SqliteModule());

    public void ConfigureServices(IServiceCollection services) =>
        services.AddXamlNexusSqlite();

    public Task InitializeAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default) =>
        services.GetRequiredService<SqliteDatabaseInitializer>()
            .InitializeAsync(cancellationToken);
}