using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace SqliteShowcase.Data.Persistence;

public sealed record SqliteDatabaseOptions(string DatabasePath);

public static class SqliteDatabase {
    internal const int BackupRetentionCount = 3;

    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SqliteShowcase",
        "Data");

    public static string DatabasePath { get; } = Path.Combine(DataDirectory, "app.db");

    public static IServiceCollection AddXamlNexusSqlite(
        this IServiceCollection services,
        string? databasePath = null) {
        ArgumentNullException.ThrowIfNull(services);
        string resolvedPath = Path.GetFullPath(databasePath ?? DatabasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(resolvedPath)!);
        var connectionString = new SqliteConnectionStringBuilder {
            DataSource = resolvedPath,
            Cache = SqliteCacheMode.Shared,
            DefaultTimeout = 5,
        }.ToString();
        services.AddSingleton(new SqliteDatabaseOptions(resolvedPath));
        services.AddDbContextFactory<AppDbContext>(options => options.UseSqlite(connectionString));
        services.AddSingleton<SqliteDatabaseInitializer>();
        return services;
    }
}

public sealed class SqliteDatabaseInitializer(
    IDbContextFactory<AppDbContext> contextFactory,
    SqliteDatabaseOptions databaseOptions) {
    public async Task InitializeAsync(CancellationToken cancellationToken = default) {
        Directory.CreateDirectory(Path.GetDirectoryName(databaseOptions.DatabasePath)!);
        bool databaseExisted = File.Exists(databaseOptions.DatabasePath);
        await using AppDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        string[] pendingMigrations = (await context.Database
            .GetPendingMigrationsAsync(cancellationToken)).ToArray();
        if (pendingMigrations.Length > 0 && databaseExisted)
            await CreateBackupAsync(context, cancellationToken);

        await context.Database.MigrateAsync(cancellationToken);
        await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);
        await context.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=5000;", cancellationToken);
    }

    public async Task<string> CheckIntegrityAsync(CancellationToken cancellationToken = default) {
        await using AppDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await context.Database.OpenConnectionAsync(cancellationToken);
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        object? result = await command.ExecuteScalarAsync(cancellationToken);
        return result?.ToString() ?? "unknown";
    }

    public string BackupDirectory => Path.Combine(
        Path.GetDirectoryName(databaseOptions.DatabasePath)!, "Backups");

    public string[] ListBackups() => Directory.Exists(BackupDirectory)
        ? Directory.GetFiles(BackupDirectory, "*.db").OrderByDescending(File.GetLastWriteTimeUtc).ToArray()
        : [];

    public async Task<string> CreateBackupAsync(CancellationToken cancellationToken = default) {
        await using AppDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await WriteBackupAsync(context, "manual", cancellationToken);
    }

    // The host must pause its database operations and dispose existing contexts before restoring.
    public async Task<string> RestoreAsync(string backupPath, CancellationToken cancellationToken = default) {
        string resolved = Path.GetFullPath(backupPath);
        if (string.Equals(resolved, Path.GetFullPath(databaseOptions.DatabasePath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The active database cannot be its own restore source.");

        await using var source = new SqliteConnection(new SqliteConnectionStringBuilder {
            DataSource = resolved, Mode = SqliteOpenMode.ReadOnly, Pooling = false,
        }.ToString());
        await source.OpenAsync(cancellationToken);
        // Validate the exact snapshot that will be restored, even if the source file changes later.
        await using var snapshot = new SqliteConnection("Data Source=:memory:");
        await snapshot.OpenAsync(cancellationToken);
        source.BackupDatabase(snapshot);
        await using (var command = snapshot.CreateCommand()) {
            command.CommandText = "PRAGMA integrity_check;";
            if (!string.Equals((await command.ExecuteScalarAsync(cancellationToken))?.ToString(), "ok", StringComparison.Ordinal))
                throw new InvalidDataException("Backup integrity check failed.");
        }
        await using var candidate = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(snapshot).Options);
        string[] applied = (await candidate.Database.GetAppliedMigrationsAsync(cancellationToken)).ToArray();
        string[] supported = candidate.Database.GetMigrations().ToArray();
        if (applied.Length == 0 || !applied.SequenceEqual(supported))
            throw new InvalidDataException("Backup migrations do not match this application version.");
        // Verify the recipe's required table and columns before touching the current database.
        _ = await candidate.AppState.AsNoTracking().Take(1).ToArrayAsync(cancellationToken);

        await using AppDbContext current = await contextFactory.CreateDbContextAsync(cancellationToken);
        string safetyBackup = await WriteBackupAsync(current, "before-restore", cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await current.Database.OpenConnectionAsync(cancellationToken);
        snapshot.BackupDatabase((SqliteConnection)current.Database.GetDbConnection());
        return safetyBackup;
    }

    private async Task CreateBackupAsync(AppDbContext context, CancellationToken cancellationToken) {
        await WriteBackupAsync(context, "app", cancellationToken);
        foreach (FileInfo stale in new DirectoryInfo(BackupDirectory)
                     .EnumerateFiles("app-*.db")
                     .OrderByDescending(file => file.CreationTimeUtc)
                     .Skip(SqliteDatabase.BackupRetentionCount)) {
            stale.Delete();
        }
    }

    private async Task<string> WriteBackupAsync(AppDbContext context, string prefix,
        CancellationToken cancellationToken) {
        Directory.CreateDirectory(BackupDirectory);
        string backupPath = Path.Combine(BackupDirectory,
            $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.db");
        string temporary = backupPath + ".tmp";
        try {
            await context.Database.OpenConnectionAsync(cancellationToken);
            await using (var destination = new SqliteConnection(new SqliteConnectionStringBuilder {
                DataSource = temporary, Pooling = false,
            }.ToString())) {
                await destination.OpenAsync(cancellationToken);
                ((SqliteConnection)context.Database.GetDbConnection()).BackupDatabase(destination);
            }
            File.Move(temporary, backupPath);
            return backupPath;
        }
        finally {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
