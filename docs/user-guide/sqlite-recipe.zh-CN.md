# SQLite Recipe

[English](sqlite-recipe.md) | [简体中文](sqlite-recipe.zh-CN.md)

向生成项目安装内置模块：

```powershell
xamlnexus add sqlite --project <project-directory>
```

按事务移除：

```powershell
xamlnexus remove sqlite --project <project-directory>
```

已安装工具的组件目录提供新版本时，执行：

```powershell
xamlnexus update sqlite --project <project-directory>
```

移除只删除未修改的 SQLite 组件文件，再移除 Data 工程引用、解决方案条目及混合项目生成的 Protobuf 项。任何组件文件被编辑都会阻止移除。确保存在的 DI 包保留兼容版本，因为应用或其他模块可能共用。

Recipe 创建 `<App>.Data` 并加入解决方案，由数据库所属进程引用：

- winui：UI 直接访问 Data。
- hybrid：只有 WPF 后台宿主访问 Data；WinUI 使用生成的业务级命名管道 gRPC 客户端，不打开 app.db。

生成工程使用面向 .NET 8 的 [Microsoft.EntityFrameworkCore.Sqlite 8.0.30](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Sqlite/8.0.30)。所属宿主的 Microsoft.Extensions.DependencyInjection 至少为 8.0.1，避免包降级。

## 运行默认值

- 数据库：`%LOCALAPPDATA%\<App>\Data\app.db`。
- 使用 EF Core migrations，不使用 EnsureCreated。
- WAL 日志模式，SQLite busy timeout 为 5 秒。
- 从 `IDbContextFactory<AppDbContext>` 创建短生命周期上下文。
- 有待执行迁移时先做 SQLite 在线备份。
- 保留最近 3 份迁移备份。
- 可调用 `PRAGMA integrity_check`。

数据库所属宿主生成 Modules/SqliteModule.cs，通过兼容裁剪的初始化器注册到模块目录。它配置服务，并在首个窗口显示前等待数据库初始化；混合架构则在 gRPC 管道服务启动前完成。

混合架构额外生成：

- app_state.proto：有界的 Get、Put、Delete、List 操作；
- WPF 宿主 AppStateServer：每次使用短生命周期 EF 上下文；
- WinUI 的 IAppStateClient 和 AppStateClient；
- SqliteClientModule：自动向前端服务提供器注册客户端。

在 WinUI 服务提供器中解析 IAppStateClient。写操作在宿主中串行化；值最大 1 MiB，键最长 200 字符，列表最多返回 200 项。协议暴露业务操作，不提供原始 SQL 或数据库连接。

测试和特殊部署可定制模块，向 `AddXamlNexusSqlite(path)` 传显式路径。生产应用通常保持每用户本地应用数据默认目录。

在解决方案根目录创建后续迁移：

```powershell
dotnet ef migrations add <Name> --project <App>.Data
```

SQLite 适合单机、低写并发桌面应用。不要将数据库放在网络共享或云同步目录。多个计算机或大量并发写入需要直接访问时，使用 PostgreSQL、SQL Server 等客户端／服务端数据库。

## 手动备份与恢复

在数据库所属进程中解析 SqliteDatabaseInitializer：

```csharp
string backup = await initializer.CreateBackupAsync();
string[] available = initializer.ListBackups();
// 用户确认后，暂停全部数据库工作并释放旧上下文。
string previousData = await initializer.RestoreAsync(backup);
// 使用新上下文恢复工作；可选择 previousData 撤销这次恢复。
```

备份使用 [SQLite 在线备份 API](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/backup)，形成包含已提交 WAL 数据的完整快照，保存在 app.db 旁的 Backups 中。连接关闭后，完成的备份才进入列表。手动和恢复前备份不自动清理；保留 3 份仅适用于 `app-*.db` 迁移备份。

复制已完成备份到其他位置即可导出。Showcase 导入时，将文件放入 Backups，点击刷新后从列表选择。

恢复先在内存快照上运行 integrity_check，要求迁移序列与当前应用完全一致，并检查 AppState 列。缺失、无效、不兼容或指向活动数据库的来源会被拒绝。写入前创建 `before-restore-*.db` 安全备份，再使用 SQLite 备份 API 恢复，不用文件复制替换活动数据库。安全备份失败则不恢复；源备份保留。

宿主必须协调应用级独占使用：停止后台写入、完成事务、释放上下文，恢复后重新创建上下文。Showcase 先确认替换并暂停页面操作。混合应用需在 WPF 宿主协调 IPC 请求；当前未提供恢复 RPC 或对应混合前端界面。

源快照保存在内存，因此该入口面向小型本地数据库。SQLite 备份是同步调用，取消令牌不能在复制中途打断；Showcase 在 UI 线程之外执行。它不负责修复导致启动失败的数据库，也不提供跨版本迁移或在迁移 ID 不变时修复任意结构变动。

doctor 的 XD5002 只查 WAL 相关源码文本，不验证运行中日志模式。文本甚至可能在注释里，不能证明初始化执行。缺失文本只产生非阻断警告，允许自定义初始化和其他日志模式。
