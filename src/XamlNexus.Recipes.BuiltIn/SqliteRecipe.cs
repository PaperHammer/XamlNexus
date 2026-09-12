using XamlNexus.Common.Recipes;

namespace XamlNexus.Recipes.BuiltIn;

public sealed class SqliteRecipe : IXamlNexusRecipe, IXamlNexusRecipeRemovalPlanProvider {
    private const string EfCoreVersion = "8.0.30";

    public XamlNexusRecipeDescriptor Descriptor { get; } = new() {
        Id = "sqlite",
        Version = "1.0.0",
        DisplayName = "SQLite with EF Core",
        Description = "Adds an EF Core SQLite data project with migrations, WAL, backup, and integrity checks.",
        SupportedPresets = ["winui", "hybrid"],
    };

    public XamlNexusRecipePlan CreatePlan(XamlNexusRecipeContext context) {
        string projectName = context.Manifest.Project.Name;
        string dataDirectory = $"{projectName}.Data";
        string dataProject = $"{dataDirectory}/{projectName}.Data.csproj";
        string hostProject = context.Manifest.Project.Preset == "hybrid"
            ? $"{projectName}/{projectName}.csproj"
            : $"{projectName}.UI/{projectName}.UI.csproj";
        string hostDirectory = context.Manifest.Project.Preset == "hybrid"
            ? projectName
            : $"{projectName}.UI";

        var changes = new List<XamlNexusRecipeFileChange> {
            XamlNexusRecipeFileChange.CreateText(dataProject, CreateProjectFile()),
            XamlNexusRecipeFileChange.CreateText(
                $"{dataDirectory}/Models/AppStateEntry.cs",
                CreateEntity(projectName)),
            XamlNexusRecipeFileChange.CreateText(
                $"{dataDirectory}/Persistence/AppDbContext.cs",
                CreateDbContext(projectName)),
            XamlNexusRecipeFileChange.CreateText(
                $"{dataDirectory}/Persistence/SqliteDatabase.cs",
                CreateDatabaseService(projectName)),
            XamlNexusRecipeFileChange.CreateText(
                $"{dataDirectory}/Migrations/20260831000000_InitialCreate.cs",
                CreateInitialMigration(projectName)),
            XamlNexusRecipeFileChange.CreateText(
                $"{dataDirectory}/Migrations/AppDbContextModelSnapshot.cs",
                CreateModelSnapshot(projectName)),
            XamlNexusRecipeFileChange.CreateText(
                $"{dataDirectory}/README.md",
                CreateReadme(projectName, context.Manifest.Project.Preset)),
            XamlNexusRecipeFileChange.CreateText(
                $"{hostDirectory}/Modules/SqliteModule.cs",
                CreateHostModule(projectName, context.Manifest.Project.Preset)),
        };
        var projectOperations = new List<XamlNexusRecipeProjectOperation> {
            new EnsurePackageReferenceOperation(
                hostProject,
                "Microsoft.Extensions.DependencyInjection",
                "8.0.1"),
            new AddProjectReferenceOperation(hostProject, dataProject),
            new AddProjectToSolutionOperation($"{projectName}.{context.Manifest.Project.SolutionFormat}", dataProject),
        };

        // Pure WinUI business ViewModels can directly inject the database factory.
        string mainPanelProject = $"{projectName}.MainPanel/{projectName}.MainPanel.csproj";
        if (context.Manifest.Project.Preset == "winui" && File.Exists(Path.Combine(context.RootDirectory, mainPanelProject)))
            projectOperations.Add(new AddProjectReferenceOperation(mainPanelProject, dataProject));

        if (context.Manifest.Project.Preset == "hybrid") {
            string protoPath = $"{projectName}.Grpc.Service/Protos/app_state.proto";
            changes.AddRange([
                XamlNexusRecipeFileChange.CreateText(protoPath, CreateAppStateProto(projectName)),
                XamlNexusRecipeFileChange.CreateText(
                    $"{projectName}/GrpcServers/AppStateServer.cs",
                    CreateAppStateServer(projectName)),
                XamlNexusRecipeFileChange.CreateText(
                    $"{projectName}.Grpc.Client/Interfaces/IAppStateClient.cs",
                    CreateAppStateClientInterface(projectName)),
                XamlNexusRecipeFileChange.CreateText(
                    $"{projectName}.Grpc.Client/AppStateClient.cs",
                    CreateAppStateClient(projectName)),
                XamlNexusRecipeFileChange.CreateText(
                    $"{projectName}.UI/Modules/SqliteClientModule.cs",
                    CreateSqliteClientModule(projectName)),
            ]);
            projectOperations.Add(new AddProtobufOperation(
                $"{projectName}.Grpc.Service/{projectName}.Grpc.Service.csproj",
                protoPath));
        }

        return new XamlNexusRecipePlan {
            Changes = changes,
            ProjectOperations = projectOperations,
        };
    }

    public IReadOnlyList<XamlNexusRecipeProjectOperation> CreateRemovalOperations(
        XamlNexusRecipeContext context) {
        string projectName = context.Manifest.Project.Name;
        string dataProject = $"{projectName}.Data/{projectName}.Data.csproj";
        string hostProject = context.Manifest.Project.Preset == "hybrid"
            ? $"{projectName}/{projectName}.csproj"
            : $"{projectName}.UI/{projectName}.UI.csproj";
        var operations = new List<XamlNexusRecipeProjectOperation> {
            new RemoveProjectReferenceOperation(hostProject, dataProject),
            new RemoveProjectFromSolutionOperation($"{projectName}.{context.Manifest.Project.SolutionFormat}", dataProject),
        };
        string mainPanelProject = $"{projectName}.MainPanel/{projectName}.MainPanel.csproj";
        if (context.Manifest.Project.Preset == "winui" && File.Exists(Path.Combine(context.RootDirectory, mainPanelProject)))
            operations.Add(new RemoveProjectReferenceOperation(mainPanelProject, dataProject));
        if (context.Manifest.Project.Preset == "hybrid") {
            operations.Add(new RemoveProtobufOperation(
                $"{projectName}.Grpc.Service/{projectName}.Grpc.Service.csproj",
                $"{projectName}.Grpc.Service/Protos/app_state.proto"));
        }
        return operations;
    }

    private static string CreateProjectFile() => $$"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net8.0</TargetFramework>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="{{EfCoreVersion}}" />
            <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="{{EfCoreVersion}}">
              <PrivateAssets>all</PrivateAssets>
              <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
            </PackageReference>
          </ItemGroup>
        </Project>
        """;

    private static string CreateEntity(string projectName) => $$"""
        namespace {{projectName}}.Data.Models;

        public sealed class AppStateEntry {
            public required string Key { get; set; }

            public required string Value { get; set; }

            public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
        }
        """;

    private static string CreateDbContext(string projectName) => $$"""
        using Microsoft.EntityFrameworkCore;
        using {{projectName}}.Data.Models;

        namespace {{projectName}}.Data.Persistence;

        public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options) {
            public DbSet<AppStateEntry> AppState => Set<AppStateEntry>();

            protected override void OnModelCreating(ModelBuilder modelBuilder) {
                modelBuilder.Entity<AppStateEntry>(entity => {
                    entity.ToTable("AppState");
                    entity.HasKey(value => value.Key);
                    entity.Property(value => value.Key).HasMaxLength(200);
                    entity.Property(value => value.Value).IsRequired();
                });
            }
        }
        """;

    private static string CreateDatabaseService(string projectName) => $$"""
        using Microsoft.Data.Sqlite;
        using Microsoft.EntityFrameworkCore;
        using Microsoft.Extensions.DependencyInjection;

        namespace {{projectName}}.Data.Persistence;

        public sealed record SqliteDatabaseOptions(string DatabasePath);

        public static class SqliteDatabase {
            internal const int BackupRetentionCount = 3;

            public static string DataDirectory { get; } = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "{{projectName}}",
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
        """;

    private static string CreateInitialMigration(string projectName) => $$"""
        using Microsoft.EntityFrameworkCore.Infrastructure;
        using Microsoft.EntityFrameworkCore.Migrations;
        using {{projectName}}.Data.Persistence;

        #nullable disable

        namespace {{projectName}}.Data.Migrations;

        [DbContext(typeof(AppDbContext))]
        [Migration("20260831000000_InitialCreate")]
        public sealed class InitialCreate : Migration {
            protected override void Up(MigrationBuilder migrationBuilder) {
                migrationBuilder.CreateTable(
                    name: "AppState",
                    columns: table => new {
                        Key = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                        Value = table.Column<string>(type: "TEXT", nullable: false),
                        UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    },
                    constraints: table => table.PrimaryKey("PK_AppState", value => value.Key));
            }

            protected override void Down(MigrationBuilder migrationBuilder) =>
                migrationBuilder.DropTable(name: "AppState");
        }
        """;

    private static string CreateModelSnapshot(string projectName) => $$"""
        using Microsoft.EntityFrameworkCore;
        using Microsoft.EntityFrameworkCore.Infrastructure;
        using Microsoft.EntityFrameworkCore.Metadata;
        using Microsoft.EntityFrameworkCore.Migrations;
        using {{projectName}}.Data.Persistence;

        #nullable disable

        namespace {{projectName}}.Data.Migrations;

        [DbContext(typeof(AppDbContext))]
        public sealed class AppDbContextModelSnapshot : ModelSnapshot {
            protected override void BuildModel(ModelBuilder modelBuilder) {
                modelBuilder.HasAnnotation("ProductVersion", "{{EfCoreVersion}}");

                modelBuilder.Entity("{{projectName}}.Data.Models.AppStateEntry", entity => {
                    entity.Property<string>("Key")
                        .HasMaxLength(200)
                        .HasColumnType("TEXT");
                    entity.Property<DateTime>("UpdatedAtUtc")
                        .HasColumnType("TEXT");
                    entity.Property<string>("Value")
                        .IsRequired()
                        .HasColumnType("TEXT");
                    entity.HasKey("Key");
                    entity.ToTable("AppState");
                });
            }
        }
        """;

    private static string CreateReadme(string projectName, string preset) {
        string hostDescription = preset == "hybrid"
            ? $"the `{projectName}` WPF background host"
            : $"the `{projectName}.UI` WinUI application";
        return $$"""
            # SQLite data module

            This project is owned by the XamlNexus `sqlite` Recipe. It uses EF Core
            {{EfCoreVersion}}, keeps `app.db` under `%LOCALAPPDATA%\{{projectName}}\Data`,
            enables WAL and a five-second busy timeout, and creates an online backup
            before applying pending migrations.

            `SqliteDatabaseInitializer` exposes `CreateBackupAsync`, `ListBackups` and
            `RestoreAsync`. Restore validates a snapshot, requires matching migrations and
            keeps a pre-restore backup. Pause all database work and dispose contexts first;
            resume with fresh contexts. Hybrid calls belong in the WPF host. Manual and
            pre-restore backups are retained in `Data/Backups`; only migration backups are pruned.

            XamlNexus automatically registers and initializes the module in
            {{hostDescription}}. Database migrations finish before the first window is
            shown or, for the hybrid Preset, before the gRPC server starts accepting
            requests.

            Create later migrations from the solution root:

            ```powershell
            dotnet ef migrations add <Name> --project {{projectName}}.Data
            ```

            Do not call `EnsureCreated`; this module uses migrations. Keep write
            transactions short. For the hybrid Preset, only the WPF background host
            may use this project; expose business operations to WinUI through gRPC.
            SQLite is not suitable for database files shared across computers or for
            workloads with many concurrent writers.
            """;
    }

    private static string CreateHostModule(string projectName, string preset) {
        string hostNamespace = preset == "hybrid" ? projectName : $"{projectName}.UI";
        if (preset == "hybrid") {
            return $$"""
                using System;
                using System.Runtime.CompilerServices;
                using System.Threading;
                using System.Threading.Tasks;
                using Grpc.Core;
                using Microsoft.Extensions.DependencyInjection;
                using {{projectName}}.Data.Persistence;
                using {{projectName}}.Grpc.Service.AppState;
                using {{projectName}}.GrpcServers;

                namespace {{hostNamespace}}.Modules;

                internal sealed class SqliteModule : IXamlNexusModule, IXamlNexusGrpcModule {
                    [ModuleInitializer]
                    internal static void Register() =>
                        XamlNexusModuleCatalog.Register(static () => new SqliteModule());

                    public void ConfigureServices(IServiceCollection services) {
                        services.AddXamlNexusSqlite();
                        services.AddSingleton<AppStateServer>();
                    }

                    public Task InitializeAsync(
                        IServiceProvider services,
                        CancellationToken cancellationToken = default) =>
                        services.GetRequiredService<SqliteDatabaseInitializer>()
                            .InitializeAsync(cancellationToken);

                    public void BindGrpcServices(
                        ServiceBinderBase serviceBinder,
                        IServiceProvider services) =>
                        Grpc_AppStateService.BindService(
                            serviceBinder,
                            services.GetRequiredService<AppStateServer>());
                }
                """;
        }
        return $$"""
            using System;
            using System.Runtime.CompilerServices;
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using {{projectName}}.Data.Persistence;

            namespace {{hostNamespace}}.Modules;

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
            """;
    }

    private static string CreateAppStateProto(string projectName) => $$"""
        syntax = "proto3";
        package {{projectName}}.Grpc.Service.AppState;
        option csharp_namespace = "{{projectName}}.Grpc.Service.AppState";

        service Grpc_AppStateService {
          rpc Get (Grpc_AppStateKeyRequest) returns (Grpc_AppStateGetResponse);
          rpc Put (Grpc_AppStatePutRequest) returns (Grpc_AppStateEntry);
          rpc Delete (Grpc_AppStateKeyRequest) returns (Grpc_AppStateDeleteResponse);
          rpc List (Grpc_AppStateListRequest) returns (Grpc_AppStateListResponse);
        }

        message Grpc_AppStateKeyRequest {
          string key = 1;
        }

        message Grpc_AppStatePutRequest {
          string key = 1;
          string value = 2;
        }

        message Grpc_AppStateEntry {
          string key = 1;
          string value = 2;
          int64 updated_at_unix_ms = 3;
        }

        message Grpc_AppStateGetResponse {
          bool found = 1;
          Grpc_AppStateEntry entry = 2;
        }

        message Grpc_AppStateDeleteResponse {
          bool deleted = 1;
        }

        message Grpc_AppStateListRequest {
          string key_prefix = 1;
          int32 limit = 2;
        }

        message Grpc_AppStateListResponse {
          repeated Grpc_AppStateEntry entries = 1;
        }
        """;

    private static string CreateAppStateServer(string projectName) => $$"""
        using Grpc.Core;
        using Microsoft.EntityFrameworkCore;
        using {{projectName}}.Data.Models;
        using {{projectName}}.Data.Persistence;
        using {{projectName}}.Grpc.Service.AppState;

        namespace {{projectName}}.GrpcServers;

        public sealed class AppStateServer(
            IDbContextFactory<AppDbContext> contextFactory)
            : Grpc_AppStateService.Grpc_AppStateServiceBase {
            private const int MaximumValueLength = 1024 * 1024;
            private readonly SemaphoreSlim _writeLock = new(1, 1);

            public override async Task<Grpc_AppStateGetResponse> Get(
                Grpc_AppStateKeyRequest request,
                ServerCallContext context) {
                ValidateKey(request.Key);
                await using AppDbContext database = await contextFactory
                    .CreateDbContextAsync(context.CancellationToken);
                AppStateEntry? entry = await database.AppState
                    .AsNoTracking()
                    .SingleOrDefaultAsync(value => value.Key == request.Key, context.CancellationToken);
                return entry is null
                    ? new Grpc_AppStateGetResponse { Found = false }
                    : new Grpc_AppStateGetResponse { Found = true, Entry = ToGrpc(entry) };
            }

            public override async Task<Grpc_AppStateEntry> Put(
                Grpc_AppStatePutRequest request,
                ServerCallContext context) {
                ValidateKey(request.Key);
                if (request.Value.Length > MaximumValueLength)
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "Value exceeds one MiB."));

                await _writeLock.WaitAsync(context.CancellationToken);
                try {
                    await using AppDbContext database = await contextFactory
                        .CreateDbContextAsync(context.CancellationToken);
                    AppStateEntry? entry = await database.AppState
                        .SingleOrDefaultAsync(value => value.Key == request.Key, context.CancellationToken);
                    if (entry is null) {
                        entry = new AppStateEntry { Key = request.Key, Value = request.Value };
                        database.AppState.Add(entry);
                    }
                    else {
                        entry.Value = request.Value;
                        entry.UpdatedAtUtc = DateTime.UtcNow;
                    }
                    await database.SaveChangesAsync(context.CancellationToken);
                    return ToGrpc(entry);
                }
                finally {
                    _writeLock.Release();
                }
            }

            public override async Task<Grpc_AppStateDeleteResponse> Delete(
                Grpc_AppStateKeyRequest request,
                ServerCallContext context) {
                ValidateKey(request.Key);
                await _writeLock.WaitAsync(context.CancellationToken);
                try {
                    await using AppDbContext database = await contextFactory
                        .CreateDbContextAsync(context.CancellationToken);
                    int deleted = await database.AppState
                        .Where(value => value.Key == request.Key)
                        .ExecuteDeleteAsync(context.CancellationToken);
                    return new Grpc_AppStateDeleteResponse { Deleted = deleted > 0 };
                }
                finally {
                    _writeLock.Release();
                }
            }

            public override async Task<Grpc_AppStateListResponse> List(
                Grpc_AppStateListRequest request,
                ServerCallContext context) {
                int limit = request.Limit <= 0 ? 100 : Math.Min(request.Limit, 200);
                await using AppDbContext database = await contextFactory
                    .CreateDbContextAsync(context.CancellationToken);
                IQueryable<AppStateEntry> query = database.AppState.AsNoTracking();
                if (!string.IsNullOrEmpty(request.KeyPrefix))
                    query = query.Where(value => value.Key.StartsWith(request.KeyPrefix));
                AppStateEntry[] entries = await query
                    .OrderBy(value => value.Key)
                    .Take(limit)
                    .ToArrayAsync(context.CancellationToken);
                var response = new Grpc_AppStateListResponse();
                response.Entries.AddRange(entries.Select(ToGrpc));
                return response;
            }

            private static Grpc_AppStateEntry ToGrpc(AppStateEntry entry) => new() {
                Key = entry.Key,
                Value = entry.Value,
                UpdatedAtUnixMs = new DateTimeOffset(
                    DateTime.SpecifyKind(entry.UpdatedAtUtc, DateTimeKind.Utc)).ToUnixTimeMilliseconds(),
            };

            private static void ValidateKey(string key) {
                if (string.IsNullOrWhiteSpace(key) || key.Length > 200)
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "Key must contain 1 to 200 characters."));
            }
        }
        """;

    private static string CreateAppStateClientInterface(string projectName) => $$"""
        namespace {{projectName}}.Grpc.Client.Interfaces;

        public sealed record AppStateValue(string Key, string Value, DateTime UpdatedAtUtc);

        public interface IAppStateClient {
            Task<AppStateValue?> GetAsync(string key, CancellationToken cancellationToken = default);
            Task<AppStateValue> PutAsync(string key, string value, CancellationToken cancellationToken = default);
            Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default);
            Task<IReadOnlyList<AppStateValue>> ListAsync(
                string keyPrefix = "",
                int limit = 100,
                CancellationToken cancellationToken = default);
        }
        """;

    private static string CreateAppStateClient(string projectName) => $$"""
        using Grpc.Core;
        using GrpcDotNetNamedPipes;
        using {{projectName}}.Common;
        using {{projectName}}.Grpc.Client.Interfaces;
        using {{projectName}}.Grpc.Service.AppState;

        namespace {{projectName}}.Grpc.Client;

        public sealed class AppStateClient : IAppStateClient {
            private readonly Grpc_AppStateService.Grpc_AppStateServiceClient _client = new(
                new NamedPipeChannel(".", Consts.CoreField.GrpcPipeServerName));

            public async Task<AppStateValue?> GetAsync(
                string key,
                CancellationToken cancellationToken = default) {
                Grpc_AppStateGetResponse response = await _client.GetAsync(
                    new Grpc_AppStateKeyRequest { Key = key },
                    cancellationToken: cancellationToken);
                return response.Found ? FromGrpc(response.Entry) : null;
            }

            public async Task<AppStateValue> PutAsync(
                string key,
                string value,
                CancellationToken cancellationToken = default) =>
                FromGrpc(await _client.PutAsync(
                    new Grpc_AppStatePutRequest { Key = key, Value = value },
                    cancellationToken: cancellationToken));

            public async Task<bool> DeleteAsync(
                string key,
                CancellationToken cancellationToken = default) =>
                (await _client.DeleteAsync(
                    new Grpc_AppStateKeyRequest { Key = key },
                    cancellationToken: cancellationToken)).Deleted;

            public async Task<IReadOnlyList<AppStateValue>> ListAsync(
                string keyPrefix = "",
                int limit = 100,
                CancellationToken cancellationToken = default) {
                Grpc_AppStateListResponse response = await _client.ListAsync(
                    new Grpc_AppStateListRequest { KeyPrefix = keyPrefix, Limit = limit },
                    cancellationToken: cancellationToken);
                return response.Entries.Select(FromGrpc).ToArray();
            }

            private static AppStateValue FromGrpc(Grpc_AppStateEntry entry) => new(
                entry.Key,
                entry.Value,
                DateTimeOffset.FromUnixTimeMilliseconds(entry.UpdatedAtUnixMs).UtcDateTime);
        }
        """;

    private static string CreateSqliteClientModule(string projectName) => $$"""
        using System;
        using System.Runtime.CompilerServices;
        using System.Threading;
        using System.Threading.Tasks;
        using Microsoft.Extensions.DependencyInjection;
        using {{projectName}}.Grpc.Client;
        using {{projectName}}.Grpc.Client.Interfaces;

        namespace {{projectName}}.UI.Modules;

        internal sealed class SqliteClientModule : IXamlNexusModule {
            [ModuleInitializer]
            internal static void Register() =>
                XamlNexusModuleCatalog.Register(static () => new SqliteClientModule());

            public void ConfigureServices(IServiceCollection services) =>
                services.AddSingleton<IAppStateClient, AppStateClient>();

            public Task InitializeAsync(
                IServiceProvider services,
                CancellationToken cancellationToken = default) => Task.CompletedTask;
        }
        """;
}
