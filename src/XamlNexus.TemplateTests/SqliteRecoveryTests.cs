using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using XamlNexus.Gallery.Data.Models;
using XamlNexus.Gallery.Data.Persistence;
using Xunit;

namespace XamlNexus.TemplateTests;

public sealed class SqliteRecoveryTests : IDisposable {
    private readonly string root = Path.Combine(AppContext.BaseDirectory, "sqlite-recovery", Guid.NewGuid().ToString("N"));
    private readonly ServiceProvider services;
    private readonly SqliteDatabaseInitializer database;
    private readonly IDbContextFactory<AppDbContext> factory;

    public SqliteRecoveryTests() {
        services = new ServiceCollection().AddXamlNexusSqlite(Path.Combine(root, "app.db")).BuildServiceProvider();
        database = services.GetRequiredService<SqliteDatabaseInitializer>();
        factory = services.GetRequiredService<IDbContextFactory<AppDbContext>>();
    }

    private async Task Put(string value) {
        await using var context = await factory.CreateDbContextAsync();
        await context.AppState.ExecuteDeleteAsync();
        context.AppState.Add(new AppStateEntry { Key = "test", Value = value });
        await context.SaveChangesAsync();
    }

    private async Task<string> Read() {
        await using var context = await factory.CreateDbContextAsync();
        return (await context.AppState.SingleAsync()).Value;
    }

    [Fact]
    public async Task BackupAndRestore_PreserveWalDataAndAllowUndo() {
        await database.InitializeAsync();
        await Put("original");
        string backup = await database.CreateBackupAsync();
        await Put("changed");
        string safety = await database.RestoreAsync(backup);
        Assert.Equal("original", await Read());
        Assert.Equal("ok", await database.CheckIntegrityAsync());
        await database.RestoreAsync(safety);
        Assert.Equal("changed", await Read());
        Assert.Contains(backup, database.ListBackups());
        Assert.Contains(safety, database.ListBackups());
    }

    [Theory]
    [InlineData("corrupt")]
    [InlineData("missing")]
    [InlineData("foreign")]
    [InlineData("future")]
    [InlineData("self")]
    public async Task InvalidSource_DoesNotChangeCurrentData(string kind) {
        await database.InitializeAsync();
        await Put("keep");
        string path = Path.Combine(root, "invalid.db");
        if (kind == "corrupt") await File.WriteAllTextAsync(path, "not sqlite");
        if (kind == "self") path = Path.Combine(root, "app.db");
        if (kind is "foreign" or "future") {
            if (kind == "future") path = await database.CreateBackupAsync();
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = kind == "foreign" ? "CREATE TABLE Other(Id INTEGER);"
                : "INSERT INTO __EFMigrationsHistory VALUES ('99999999999999_Future', '8.0.30');";
            command.ExecuteNonQuery();
        }
        Assert.NotNull(await Record.ExceptionAsync(() => database.RestoreAsync(path)));
        Assert.Equal("keep", await Read());
        Assert.DoesNotContain(database.ListBackups(), item => Path.GetFileName(item).StartsWith("before-restore-"));
    }

    [Fact]
    public async Task BackupFailure_DoesNotChangeCurrentData() {
        await database.InitializeAsync();
        await Put("keep");
        await File.WriteAllTextAsync(database.BackupDirectory, "blocks backup directory");
        Assert.NotNull(await Record.ExceptionAsync(() => database.CreateBackupAsync()));
        Assert.Equal("keep", await Read());
    }

    [Fact]
    public async Task LockedBackup_DoesNotChangeCurrentData() {
        await database.InitializeAsync();
        await Put("original");
        string backup = await database.CreateBackupAsync();
        await Put("keep");
        using (var held = new FileStream(backup, FileMode.Open, FileAccess.Read, FileShare.None)) {
            Assert.NotNull(await Record.ExceptionAsync(() => database.RestoreAsync(backup)));
        }
        Assert.Equal("keep", await Read());
    }

    [Fact]
    public async Task SafetyBackupFailure_PreventsRestore() {
        await database.InitializeAsync();
        await Put("original");
        string backup = await database.CreateBackupAsync();
        string external = Path.Combine(root, "source.db");
        File.Copy(backup, external);
        File.Delete(backup);
        Directory.Delete(database.BackupDirectory);
        await File.WriteAllTextAsync(database.BackupDirectory, "blocked");
        await Put("keep");
        Assert.NotNull(await Record.ExceptionAsync(() => database.RestoreAsync(external)));
        Assert.Equal("keep", await Read());
    }

    [Fact]
    public async Task CanceledRestore_DoesNotChangeCurrentData() {
        await database.InitializeAsync();
        await Put("original");
        string backup = await database.CreateBackupAsync();
        await Put("keep");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => database.RestoreAsync(backup, cancellation.Token));
        Assert.Equal("keep", await Read());
    }

    public void Dispose() {
        services.Dispose();
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
