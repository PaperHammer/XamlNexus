using Microsoft.Extensions.DependencyInjection;
using Winui3_XamlNexus.Common.Utils.DI;
using Xunit;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using XamlNexus.Gallery.Data.Persistence;
using XamlNexus.Gallery.Data.Models;

namespace XamlNexus.TemplateTests;

public sealed class AppObjectFactoryTests {
    public sealed class SharedService;
    public sealed class ViewModel(SharedService service) : IDisposable {
        public SharedService Service { get; } = service;
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }
    public sealed class SimplePage;

    public sealed class DatabaseViewModel(IDbContextFactory<AppDbContext> database) {
        public async Task WriteAsync() {
            await using var context = await database.CreateDbContextAsync();
            await context.Database.EnsureCreatedAsync();
            context.AppState.Add(new AppStateEntry { Key = "orders", Value = "injected" });
            await context.SaveChangesAsync();
        }
        public async Task<string> ReadAsync() {
            await using var context = await database.CreateDbContextAsync();
            return (await context.AppState.SingleAsync(entry => entry.Key == "orders")).Value;
        }
    }

    [Fact]
    public async Task InjectedDatabaseFactorySupportsIndependentViewModelsAndContexts() {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(options => options.UseSqlite(connection));
        await using var provider = services.BuildServiceProvider();
        var writer = (DatabaseViewModel)AppObjectFactory.Create(provider, typeof(DatabaseViewModel));
        var reader = (DatabaseViewModel)AppObjectFactory.Create(provider, typeof(DatabaseViewModel));
        await writer.WriteAsync();
        Assert.Equal("injected", await reader.ReadAsync());
    }

    [Fact]
    public void CreatesUnregisteredObjectsWithSharedDependenciesAndCallerOwnership() {
        var services = new ServiceCollection();
        services.AddSingleton<SharedService>();
        var provider = services.BuildServiceProvider();
        var first = (ViewModel)AppObjectFactory.Create(provider, typeof(ViewModel));
        var second = (ViewModel)AppObjectFactory.Create(provider, typeof(ViewModel));
        Assert.NotSame(first, second);
        Assert.Same(first.Service, second.Service);
        provider.Dispose();
        Assert.False(first.Disposed);
        first.Dispose();
        second.Dispose();
        Assert.True(first.Disposed);
    }

    [Fact]
    public void ParameterlessObjectsWorkAndMissingDependenciesFailClearly() {
        using var provider = new ServiceCollection().BuildServiceProvider();
        Assert.IsType<SimplePage>(AppObjectFactory.Create(provider, typeof(SimplePage)));
        Assert.Throws<InvalidOperationException>(() => AppObjectFactory.Create(provider, typeof(ViewModel)));
    }
}
