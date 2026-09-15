# SQLite 数据模块

[English](README.md) | [简体中文](README.zh-CN.md)

此项目由 XamlNexus 的 `sqlite` Recipe 管理，使用 EF Core {{EfCoreVersion}}。
数据库 `app.db` 位于 `%LOCALAPPDATA%\{{ProjectName}}\Data`，启用 WAL 和 5 秒忙等待超时，
并在应用待执行的迁移前创建在线备份。

`SqliteDatabaseInitializer` 提供 `CreateBackupAsync`、`ListBackups` 和 `RestoreAsync`。
恢复时会校验快照，要求迁移匹配，并保留恢复前备份。恢复前请暂停所有数据库操作并释放上下文，
恢复后使用新的上下文。hybrid 架构下应在 WPF 宿主中调用这些方法。
手动备份和恢复前备份保留在 `Data/Backups`；仅迁移备份会按保留策略清理。

XamlNexus 会在 {{HostDescription}} 中自动注册并初始化模块。
数据库迁移在第一个窗口显示前完成；hybrid 架构下则在 gRPC 服务开始接收请求前完成。

后续新增迁移时，在解决方案根目录执行：

```powershell
dotnet ef migrations add <Name> --project {{ProjectName}}.Data
```

本模块使用迁移，请勿调用 `EnsureCreated`。写事务应尽量短。
hybrid 架构下仅 WPF 后台宿主可以使用此项目，WinUI 通过 gRPC 调用业务操作。
SQLite 不适合多台计算机共享数据库文件或大量并发写入的场景。
