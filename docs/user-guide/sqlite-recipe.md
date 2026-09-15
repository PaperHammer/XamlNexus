# SQLite Recipe

[English](sqlite-recipe.md) | [简体中文](sqlite-recipe.zh-CN.md)

Install the built-in module into a generated project:

```powershell
xamlnexus add sqlite --project <project-directory>
```

Remove it transactionally with:

```powershell
xamlnexus remove sqlite --project <project-directory>
```

When a newer SQLite Recipe is present in the installed XamlNexus Catalog, run:

```powershell
xamlnexus update sqlite --project <project-directory>
```

Removal deletes only unchanged SQLite-owned files, then removes the Data
project reference, solution entry, and (for hybrid projects) the generated
Protobuf item. It refuses to proceed if an owned file was edited. The ensured
dependency-injection package is retained at its compatible version because it
may be shared by the application or another module.

The Recipe creates `<App>.Data`, adds it to the solution, and references it
from the process that owns the database:

- `winui`: `<App>.UI` accesses the Data project directly;
- `hybrid`: only the `<App>` WPF background host accesses it. The WinUI
  process uses the generated business-level named-pipe gRPC client and never
  opens `app.db`.

The generated project uses
[`Microsoft.EntityFrameworkCore.Sqlite` 8.0.30](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Sqlite/8.0.30),
which targets .NET 8. The Recipe also ensures the owning host uses
`Microsoft.Extensions.DependencyInjection` 8.0.1 or newer to avoid a NuGet
package downgrade.

## Runtime defaults

- Database: `%LOCALAPPDATA%\<App>\Data\app.db`
- EF Core migrations; `EnsureCreated` must not be used
- WAL journal mode
- five-second SQLite busy timeout
- short-lived contexts from `IDbContextFactory<AppDbContext>`
- online SQLite backup before pending migrations
- retention of the three newest migration backups
- callable `PRAGMA integrity_check`

The Recipe generates `Modules/SqliteModule.cs` in the owning host. A
trim-safe module initializer registers it with the template module catalog,
which configures its services and awaits
database initialization before showing the first window. In the hybrid Preset,
initialization completes before the named-pipe gRPC server starts.

For the hybrid Preset, the Recipe additionally generates:

- `app_state.proto` with bounded Get, Put, Delete, and List operations;
- `AppStateServer` in the WPF host, backed by short-lived EF contexts;
- `IAppStateClient` and `AppStateClient` for WinUI;
- `SqliteClientModule` to register the client automatically in the WinUI
  service provider.

Resolve `IAppStateClient` from the WinUI service provider. Writes are
serialized in the background host, values are limited to one MiB, keys to 200
characters, and list requests return at most 200 entries. The contract exposes
business operations rather than raw SQL or a database connection.

Tests and specialized deployments can customize the generated module to pass an
explicit database path to `AddXamlNexusSqlite(path)`. Production applications
should normally retain the per-user local application-data default.

Create subsequent migrations from the solution root:

```powershell
dotnet ef migrations add <Name> --project <App>.Data
```

SQLite is intended for a single-machine, low-write-concurrency desktop
application. Do not place its database on a network share or cloud-synchronized
directory. Use a client/server database such as PostgreSQL or SQL Server when
multiple computers or many concurrent writers need direct database access.


## Manual backup and restore

Resolve `SqliteDatabaseInitializer` in the database-owning process:

```csharp
string backup = await initializer.CreateBackupAsync();
string[] available = initializer.ListBackups();
// Ask the user to confirm, then pause all database work and dispose old contexts.
string previousData = await initializer.RestoreAsync(backup);
// Resume with fresh contexts; previousData can be selected to undo the restore.
```

Backups are full SQLite snapshots, including committed WAL data, created with the
[SQLite online backup API](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/backup).
They are stored beside `app.db` in `Backups`. Completed files appear in the list only
after the backup connection closes. Manual and pre-restore backups are not pruned;
the three-file retention rule applies only to migration backups (`app-*.db`).
Copy a completed backup elsewhere to export it. To import one in the Showcase, copy
it into `Backups` and click Refresh, then select it in the list.

Restore validates an in-memory snapshot with `integrity_check`, requires the exact
migration sequence supported by this application, and checks the recipe's AppState
columns. Missing, invalid, incompatible or active-database sources are rejected.
It creates a `before-restore-*.db` safety backup before writing through SQLite's
backup API; it never replaces a live `.db` file using filesystem copy operations.
A failed safety backup prevents restoration. The source backup is retained.

The host must coordinate exclusive application-level use: stop background writes,
finish transactions and dispose contexts before calling RestoreAsync, then create
fresh contexts afterward. The Showcase pauses its page operations and confirms the
replacement first. Hybrid applications must do this in the WPF host and coordinate
IPC requests; this change does not add a restore RPC or hybrid frontend UI.

The source snapshot is held in memory, so this entry point targets small local
application databases. SQLite backup calls are synchronous and cannot be interrupted
mid-copy by a cancellation token; the Showcase runs them off the UI thread. This
is not repair of a database that prevents application startup, nor cross-version
migration or recovery from arbitrary schema changes with unchanged migration IDs.

The `doctor` check `XD5002` only looks for WAL-related source text; it does not
verify the running database's journal mode. Finding text (even in a comment) is
not proof that initialization executes. Missing text produces a non-blocking
warning: custom initialization and other journal modes are allowed.
