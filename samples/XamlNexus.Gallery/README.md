# XamlNexus Gallery

这是独立的 XamlNexus Gallery 应用，提供可交互示例、Tips 和源码模板浏览。
Gallery 与 CLI 默认项目模板分开维护，不会随 `xamlnexus new` 生成到用户项目。包含纯 WinUI 3、SQLite Recipe、
EF Core Migration、WAL、数据增删改查、托盘及用户触发通知。
默认运行路径为 x64 非打包应用。

## 运行

```powershell
dotnet restore XamlNexus.Gallery.sln -p:NuGetAudit=false
dotnet build XamlNexus.Gallery.sln -m:1 -p:Platform=x64 `
  -p:UseSharedCompilation=false -p:NuGetAudit=false
dotnet run --project XamlNexus.Gallery.UI/XamlNexus.Gallery.UI.csproj -p:Platform=x64
```

应用首次启动会执行数据库迁移。数据文件位于：

```text
%LOCALAPPDATA%\XamlNexus.Gallery\Data\app.db
```

SQLite 示例页可以保存、查看、删除键值数据，并执行 SQLite 完整性检查。
选中记录后可编辑；相同键保存为更新，新键保存为新增。
“最近更新优先”会保存到用户设置并在重启后恢复；失败时恢复原选择并显示错误。
操作期间禁止重复提交，成功及异常日志位于 `%LOCALAPPDATA%\XamlNexus.Gallery\logs\UI`。

业务实现和接入步骤见 [第一个业务页面](../../docs/user-guide/business-page.zh-CN.md)。
设置页切换中英文立即生效，业务页和托盘菜单跟随选择；重启恢复保存的语言。
主题、窗口材质和语言设置在保存失败时回退；Gallery 不提供开机启动或存储目录切换入口。

## 数据备份与恢复

点击“创建备份”保存当前完整数据库；在下拉列表选择备份后点击“恢复备份”，
确认替换后恢复数据并刷新页面。恢复前自动保留当前数据，选择该安全备份可以撤销恢复。
备份位于 `%LOCALAPPDATA%\XamlNexus.Gallery\Data\Backups`，手动备份和安全备份不会自动清理。
将已完成的 `.db` 备份复制到其他位置即可导出；导入时放入该目录并点击“刷新”。
恢复仅接受与当前应用迁移版本匹配的备份。操作会替换当前数据库内容，不影响主题和语言设置。
本入口要求应用能够正常启动，不承担无法启动时的离线数据库修复。

## 托盘与提醒

- 点击托盘图标可隐藏或恢复窗口；右键菜单提供“显示 / 隐藏”和“退出”。
- 点击 SQLite 示例页“发送提醒”，通过托盘请求显示当前列表记录数；应用不会在启动时主动弹通知。
- 页面提示“已请求显示提醒”只代表调用完成。Windows 通知权限、勿扰模式等会影响最终显示。
- 关闭主窗口会退出应用；需要常驻时使用托盘隐藏窗口。

页面依赖 Models 中的 `INotificationService`，由 UI 的 SystemTrayService 实现，
避免 MainPanel 反向引用 UI 工程。服务在按钮操作时取得，此时托盘已经初始化。
该示例对 Recipe 生成的托盘模块增加了本地化与接口适配；移除或更新 Recipe 时需保留这些自定义内容。

## 便携发布 / Portable build

在仓库根目录运行：

```powershell
./eng/Publish-Gallery.ps1
# ARM64 可选；必须在对应设备上验证运行
./eng/Publish-Gallery.ps1 -Architecture arm64
```

产物位于 `.artifacts/gallery/`，解压 ZIP 后运行 `XamlNexus.Gallery.exe`。
包内包含 .NET 与 Windows App SDK 运行依赖；请保留完整目录，不要只复制 exe。
数据和设置写入 `%LOCALAPPDATA%/XamlNexus.Gallery`。它与旧 SqliteShowcase 数据目录隔离，不自动迁移或删除旧数据。

独立的 `Gallery portable build` 工作流在相关 PR 和手动触发时生成 x64 ZIP，
上传为 Actions artifact，不会自动发布 GitHub Release，也不参与 CLI 的 NuGet 打包。
发布前需在没有开发环境的 Windows 设备上验证启动、源码查看和 SQLite 写入。

Build from the repository root with `./eng/Publish-Gallery.ps1`. Extract the complete ZIP and run
`XamlNexus.Gallery.exe`. The portable package includes .NET and Windows App SDK dependencies;
settings and demo data live under LocalAppData. A clean Windows machine smoke test is required before release.

## 源码来源 / Source provenance

- “源码模板 · CLI”直接嵌入 `src/XamlNexus.Common/Assets/ListPage/` 中的实际模板，不维护展示副本。
- “源码 · Gallery 示例”嵌入当前运行示例的页面、ViewModel 和数据服务。
- 文件选择器支持完整源码浏览、文本选择与复制。XAML / C# / CLI 提供轻量语法着色，随浅色和深色主题更新；高对比度使用系统文本色。
- 复制成功会显示提示；剪贴板不可用时提示重试或手动选择复制。复制内容始终是原始完整文本。
- 列表、KeepAlive 与 SQLite 页面提供“在你的项目中使用”：项目准备入口、真实 CLI 命令、修改位置和验证步骤。SQLite 组件不会自动生成 Gallery 的业务页。
- 发布包可离线查看源码，无需下载仓库。
- `__APP__` / `__NAME__` 是 CLI 生成时替换的占位符；Gallery 的演示开关属于示例代码。

The source browser embeds the actual CLI template files and Gallery example sources at build time.
Choose a file to read or copy its complete source offline. XAML, C# and CLI examples use theme-aware lexical highlighting with copy feedback; clipboard failures are recoverable. Each example includes integration commands, edit locations and a result checklist. Template placeholders are resolved by the CLI.

## 参数预览与生命周期对比 / Parameter preview and lifetime comparison

展开列表页的“源码模板 · CLI”，填写项目名称和页面名称，即可预览替换后的完整源码与目标文件路径；复制按钮复制当前显示的内容。关闭“替换模板参数”可查看原始模板。名称以大写英文字母开头，后接字母或数字；无效输入会显示提示并退回原始模板，不写入磁盘。这里只预览四个列表模板文件，导航接入仍由 CLI 完成。

在列表中输入搜索条件，通过“生命周期对比”的两个入口来回切换。两列记录真实的创建、进入、离开和卸载回调，显示页面与 ViewModel 编号以及搜索条件，每类仅保留最近 20 条。普通页面返回时创建新实例；KeepAlive 返回时复用原实例。本例的 ViewModel 由页面创建，应用若通过外部服务持有状态，其生命周期可以不同。OnDestroy 是框架清理回调，并不表示 GC 已回收；当前框架可能从导航清理和 Unloaded 两条路径触发它，因此记录不会去重。

Expand “Source template · CLI” to preview project/page parameter replacement and output paths. Copy uses the displayed source. Invalid names fall back to the raw template; no files are written. The preview covers four list templates; CLI generation also handles navigation integration.

Use the two lifetime comparison links after entering a query. Each column records actual callbacks, page/ViewModel IDs and the query (latest 20 entries). These examples own their ViewModels; externally owned state may have a different lifetime. OnDestroy indicates framework cleanup, not garbage collection, and may appear from both navigation cleanup and Unloaded.

## 维护边界

Gallery 源自原 SqliteShowcase 示例，现拥有独立 solution、应用标识、数据目录与版本文件。
修改 Gallery 界面不应改动 `src/Templates/`；修改 CLI 列表模板后，重新构建 Gallery 即可同步源码展示。
`xamlnexus.json` 保留生成来源记录；Gallery 本身作为手工维护的应用开发，不通过 CLI 重新生成。

## Gallery 导览 / Gallery tour

整体布局参考 [WinUI Gallery](https://github.com/microsoft/WinUI-Gallery) 的分类导航、卡片入口与示例/代码组合；页面与代码为本项目实现。

- **首页 / Home**：功能卡片、快速开始与示例入口。
- **搜索 / Search**：搜索中英文功能名称或描述，选择建议进入对应示例。
- **列表与异步加载 / Lists & async loading**：搜索、刷新、慢请求（3 秒）、模拟失败与重试。离开后重新创建页面。
- **页面保活 / Page retention**：同样的列表，使用 `[KeepAlive]`。切走再返回，实例编号、数据和搜索条件保持不变。
- **数据与恢复 / Data & recovery**：使用 SQLite 演示数据编辑、备份恢复及通知，附使用提示与代码。
- **快速开始 / Getting started**：环境诊断、创建项目、添加组件及维护命令。
- **设置 / Settings**：Gallery 专用的主题、窗口材质、语言与关于页面。

示例页统一提供可操作演示、Tips 和可展开/复制的代码。切换应用语言时，Gallery 导航与说明同步更新。默认生成项目仍保持简洁；本次 Gallery 布局仅属于示例应用。

The Gallery uses category navigation, feature cards, interactive examples, tips and expandable/copyable code. Compare the regular list with the KeepAlive page by searching, navigating away and returning. The instance ID makes the lifetime visible. Enable the slow-request/failure switches before refreshing to exercise cancellation and recovery. This sample redesign does not change the default generated application's shell.

KeepAlive 只保留当前进程内的页面实例，不会自动保存数据库编辑，也不提供退出后的状态恢复。

## 窗口与布局 / Window layout

Gallery 的视觉设计以 WinUI Gallery 为参考，使用独立的 `GalleryShell`。标题栏整合返回、侧栏开关和搜索；基础用法与功能示例是可展开的导航分组，主题仅在设置中切换。快速开始使用启动图标，页面保活使用历史时钟图标。首页、示例页、设置页及源码/提示控件的布局均在 XAML 中定义，C# 仅处理数据、事件、生命周期和源码着色。

Gallery 默认使用系统 Mica 材质，可在设置中选择 Acrylic（重启生效）。窗口交互区域按 DPI 和布局更新。列表页使用演示区、右侧选项和底部源码组合，窄窗口时选项移到演示下方；使用说明与生命周期记录可折叠。保留底层导航生命周期以运行 KeepAlive 示例，但这些基础设施不决定 Gallery 外观，`src/Templates/` 不随 Gallery 设计变化。

The visual reference is WinUI Gallery. GalleryShell provides title-bar back/menu/search controls and expandable navigation groups. Theme selection lives only in Settings. Home, examples, settings and shared source/guidance controls use XAML layouts; code-behind handles behavior. Navigation lifetimes remain available for KeepAlive examples; generated project templates are unchanged. Mica is the default, with Acrylic available after restart.

## XAML 与双语资源 / XAML and bilingual resources

`MainPanel/Gallery/` 中的页面与控件使用 `.xaml` / `.xaml.cs` 配对维护，共用 `GalleryStyles.xaml`。`Strings/en-US.json` 和 `Strings/zh-CN.json` 提供相同键的文案，语言切换通过绑定更新，不重建可操作示例。原有数据页继续使用本地化 `.resw` 资源。源码显示区为横纵滚动条预留空间，复制内容仍是未加工的完整源码；示例源码浏览器同时展示实际 XAML 和后台代码。

Gallery pages and controls use paired XAML/code-behind files with shared styles. English and Chinese JSON resources share identical keys and update through bindings without rebuilding interactive examples. The data page retains its existing localized RESW resources. The source viewer reserves space for both scrollbars and copies the unmodified source, including the actual XAML and code-behind files.

CLI 命令逐条展示，每条提供独立复制按钮；接入指南按创建、业务接入与验证分区。列表与异步加载只展示数据交互，页面保活展示实例保留，生命周期日志按需展开。设置按模板式标题分组，同组行间距为 6px，行高至少 90px。

CLI commands are separate rows with individual copy actions. Integration guidance separates creation, data integration and verification. Async lists focus on data interactions; page retention focuses on retained instances with optional lifecycle logs. Settings use grouped headers, 6px row gaps and a 90px minimum row height.
