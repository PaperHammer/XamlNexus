using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using XamlNexus.Gallery.Data.Models;
using XamlNexus.Gallery.Data.Persistence;
using Xunit;

namespace XamlNexus.TemplateTests;

public sealed class GalleryBusinessStoreTests : IDisposable {
    private readonly string root = Path.Combine(AppContext.BaseDirectory, "gallery-business", Guid.NewGuid().ToString("N"));
    private ServiceProvider Open() => new ServiceCollection().AddXamlNexusSqlite(Path.Combine(root, "app.db")).BuildServiceProvider();

    [Fact]
    public async Task CreateEditAndReopen_PreserveIdentityAndBusinessData() {
        string id;
        using (var services = Open()) {
            await services.GetRequiredService<SqliteDatabaseInitializer>().InitializeAsync();
            var store = new GalleryBusinessStore(services.GetRequiredService<IDbContextFactory<AppDbContext>>());
            var created = await store.SaveAsync(null, "  First record  ", "Notes");
            id = created.Id;
            Assert.Equal("First record", created.Name);
            Assert.Equal(created, Assert.Single(await store.ListAsync()));
            Assert.Equal(created, await store.GetAsync(id));
            var updated = await store.SaveAsync(id, "Renamed record", "Changed notes");
            Assert.Equal(id, updated.Id);
            Assert.Equal(updated, Assert.Single(await store.ListAsync()));
            // Duplicate names must remain distinct records, including when the UI filters by name.
            var duplicate = await store.SaveAsync(null, "Renamed record", "Changed notes");
            Assert.NotEqual(id, duplicate.Id);
        }
        using (var reopened = Open()) {
            await reopened.GetRequiredService<SqliteDatabaseInitializer>().InitializeAsync();
            var store = new GalleryBusinessStore(reopened.GetRequiredService<IDbContextFactory<AppDbContext>>());
            Assert.Equal(2, (await store.ListAsync()).Count);
            Assert.Equal(new GalleryBusinessRecord(id, "Renamed record", "Changed notes"), await store.GetAsync(id));
        }
    }

    [Fact]
    public async Task BusinessRecords_DoNotConsumeOrOverwriteOtherShowcaseData() {
        using var services = Open();
        await services.GetRequiredService<SqliteDatabaseInitializer>().InitializeAsync();
        var factory = services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using (var db = await factory.CreateDbContextAsync()) {
            db.AppState.Add(new AppStateEntry { Key = "user.record", Value = "keep" });
            await db.SaveChangesAsync();
        }
        var store = new GalleryBusinessStore(factory);
        Assert.Empty(await store.ListAsync());
        Assert.Null(await store.GetAsync(Guid.NewGuid().ToString("N")));
        await store.SaveAsync(null, "A record", "Business notes");
        await using var after = await factory.CreateDbContextAsync();
        Assert.Equal("keep", (await after.AppState.SingleAsync(row => row.Key == "user.record")).Value);
    }

    [Fact]
    public async Task CanceledOrInvalidWrites_DoNotChangeSavedRecords() {
        using var services = Open();
        await services.GetRequiredService<SqliteDatabaseInitializer>().InitializeAsync();
        var store = new GalleryBusinessStore(services.GetRequiredService<IDbContextFactory<AppDbContext>>());
        var original = await store.SaveAsync(null, "Keep", "Original");
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(original.Id, "Changed", "", canceled.Token));
        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(original.Id, " ", ""));
        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync("../other", "Invalid", ""));
        Assert.Equal(original, await store.GetAsync(original.Id));
        Assert.Single(await store.ListAsync());
    }

    public void Dispose() {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
