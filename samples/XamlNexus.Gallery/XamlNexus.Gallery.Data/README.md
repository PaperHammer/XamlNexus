# SQLite data module

This project is owned by the XamlNexus `sqlite` Recipe. It uses EF Core
8.0.30, keeps `app.db` under `%LOCALAPPDATA%\XamlNexus.Gallery\Data`,
enables WAL and a five-second busy timeout, and creates an online backup
before applying pending migrations.

`SqliteDatabaseInitializer` exposes `CreateBackupAsync`, `ListBackups` and
`RestoreAsync`. Restore validates a snapshot, requires matching migrations and
keeps a pre-restore backup. Pause all database work and dispose contexts first;
resume with fresh contexts. Hybrid calls belong in the WPF host. Manual and
pre-restore backups are retained in `Data/Backups`; only migration backups are pruned.

XamlNexus automatically registers and initializes the module in
the `XamlNexus.Gallery.UI` WinUI application. Database migrations finish before the first window is
shown or, for the hybrid Preset, before the gRPC server starts accepting
requests.

Create later migrations from the solution root:

```powershell
dotnet ef migrations add <Name> --project XamlNexus.Gallery.Data
```

Do not call `EnsureCreated`; this module uses migrations. Keep write
transactions short. For the hybrid Preset, only the WPF background host
may use this project; expose business operations to WinUI through gRPC.
SQLite is not suitable for database files shared across computers or for
workloads with many concurrent writers.