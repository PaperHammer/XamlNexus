# SqliteShowcase

这是 XamlNexus 当前黄金路径的最小可运行示例：纯 WinUI 3、SQLite Recipe、
EF Core Migration、WAL、数据增删改查、托盘及用户触发通知。
默认运行路径为 x64 非打包应用。

## 运行

```powershell
dotnet restore SqliteShowcase.sln -p:NuGetAudit=false
dotnet build SqliteShowcase.sln -m:1 -p:Platform=x64 `
  -p:UseSharedCompilation=false -p:NuGetAudit=false
dotnet run --project SqliteShowcase.UI/SqliteShowcase.UI.csproj -p:Platform=x64
```

应用首次启动会执行数据库迁移。数据文件位于：

```text
%LOCALAPPDATA%\SqliteShowcase\Data\app.db
```

主页可以保存、查看、删除键值数据，并执行 SQLite 完整性检查。
选中记录后可编辑；相同键保存为更新，新键保存为新增。
“最近更新优先”会保存到用户设置并在重启后恢复；失败时恢复原选择并显示错误。
操作期间禁止重复提交，成功及异常日志位于 `%LOCALAPPDATA%\SqliteShowcase\logs\UI`。

业务实现和接入步骤见 [第一个业务页面](../../docs/user-guide/business-page.zh-CN.md)。
设置页切换中英文立即生效，业务页和托盘菜单跟随选择；重启恢复保存的语言。
主题、背景和开机启动已同步模板的保存失败回退，存储目录切换保留原文件。

## 数据备份与恢复

点击“创建备份”保存当前完整数据库；在下拉列表选择备份后点击“恢复备份”，
确认替换后恢复数据并刷新页面。恢复前自动保留当前数据，选择该安全备份可以撤销恢复。
备份位于 `%LOCALAPPDATA%\SqliteShowcase\Data\Backups`，手动备份和安全备份不会自动清理。
将已完成的 `.db` 备份复制到其他位置即可导出；导入时放入该目录并点击“刷新”。
恢复仅接受与当前应用迁移版本匹配的备份。操作会替换当前数据库内容，不影响主题和语言设置。
本入口要求应用能够正常启动，不承担无法启动时的离线数据库修复。

## 托盘与提醒

- 点击托盘图标可隐藏或恢复窗口；右键菜单提供“显示 / 隐藏”和“退出”。
- 点击主页“发送提醒”，通过托盘请求显示当前列表记录数；应用不会在启动时主动弹通知。
- 页面提示“已请求显示提醒”只代表调用完成。Windows 通知权限、勿扰模式等会影响最终显示。
- 关闭主窗口会退出应用；需要常驻时使用托盘隐藏窗口。

页面依赖 Models 中的 `INotificationService`，由 UI 的 SystemTrayService 实现，
避免 MainPanel 反向引用 UI 工程。服务在按钮操作时取得，此时托盘已经初始化。
该示例对 Recipe 生成的托盘模块增加了本地化与接口适配；移除或更新 Recipe 时需保留这些自定义内容。

## 发布

```powershell
dotnet publish SqliteShowcase.UI/SqliteShowcase.UI.csproj `
  -c Release -r win-x64 --self-contained true -m:1 `
  -o artifacts/publish -p:Platform=x64 -p:PublishProfile= `
  -p:PublishTrimmed=false -p:PublishReadyToRun=false `
  -p:UseSharedCompilation=false -p:NuGetAudit=false
```

发布成功后直接运行：

```text
artifacts\publish\SqliteShowcase.UI.exe
```

## 生成过程

此示例以以下命令生成，再将默认主页替换为 SQLite 操作界面：

```powershell
xamlnexus new SqliteShowcase --preset winui --language zh-CN
xamlnexus add sqlite --project .\SqliteShowcase
xamlnexus add system-tray --project .\SqliteShowcase
```
