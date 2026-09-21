# XamlNexus Gallery

公共 UI 维护方式见 [项目架构](../../docs/architecture.md)。Gallery 从 WinUI 模板复用公共源码，专用页面和窗口独立维护。

See [Architecture](../../docs/architecture.md) for shared UI maintenance. Gallery reuses common WinUI template sources while keeping its own pages and window behavior.

这是独立的 XamlNexus Gallery 应用，提供可交互示例、Tips 和源码模板浏览。
Gallery 与 CLI 默认项目模板分开维护，不会随 `xamlnexus new` 生成到用户项目。包含纯 WinUI 3、SQLite Recipe、
EF Core Migration、WAL、数据增删改查、托盘及用户触发通知。
默认运行路径为 x64 非打包应用。

## 内容与导航 / Content and navigation

- **概览**：产品定位、基础设施 / Recipe / 可审查变更三组亮点、按“窗口与导航 / 列表与数据”筛选的交互示例，以及创建 → 扩展 → 维护的学习路径。
- **工具指南**：工程起点（WinUI / hybrid、standard / basic）、五种 Recipe（settings、sqlite、tray、updater、editorconfig）和项目维护（doctor、validate、update、upgrade）。每项说明适用条件，并提供可复制命令。
- **快速开始**：安装检查 → 创建运行 → 修改与生成列表页 → 添加存储与检查。Gallery 只展示和复制命令，不执行命令。
- **窗口与导航 / 列表与数据**：保留真实交互、源码浏览与接入步骤。SQLite Recipe 提供数据层，Gallery 的业务 CRUD、备份与恢复界面属于定制演示。

The overview introduces the product, highlights its foundations, and links to filtered interactive samples. The tool guide covers architecture/profile choices, all five built-in Recipes and project maintenance. Getting started follows four ordered steps from installation to storage. Copyable commands are examples only and are never executed by the Gallery.

The guide describes the source version shipped with the Gallery. Keep its commands and capability descriptions aligned with `src/XamlNexus.Tooling/CommandLine/`, `src/XamlNexus.Recipes.BuiltIn/` and the user guide when changing the CLI. Both language files under `Gallery/Strings/` must be updated together. Theme colors and shared typography live in `GalleryStyles.xaml`; homepage highlights and learning links live in `GalleryHomeContent.cs`.

## 运行

安装正式工具包后，可直接执行以下命令，不需要克隆源码或安装 WinUI 构建环境：

```powershell
dotnet tool update --global XamlNexus
xamlnexus gallery
```

首次使用工具时，将 `update` 改为 `install`。Gallery 版本由已安装工具包中的清单固定，首次启动下载对应 x64 ZIP 并验证 SHA-256，随后可离线启动。升级 tool 后，下次执行 `gallery` 获取对应新版；请先关闭正在运行的 Gallery。

程序缓存位于 `%LOCALAPPDATA%/XamlNexus/Gallery`，与下文的 Gallery 用户数据目录分开。旧版缓存保留，关闭应用后可手动清理无需保留的缓存版本。失败下载不会作为可用版本启动。

Official tool packages provide `xamlnexus gallery`. The first launch downloads and verifies the pinned x64 release; cached launches work offline. Updating the tool selects its matching Gallery on the next launch. Close the running Gallery first. Executables are cached under `%LOCALAPPDATA%/XamlNexus/Gallery`, separately from user data. Source-only tool builds without a release manifest report this explicitly.

在 Visual Studio 中，可直接打开主解决方案 `src/XamlNexus.sln`，在 `Samples` 下将 `XamlNexus.Gallery.UI` 设为启动项目，选择 `Debug / x64` 和 `XamlNexus.Gallery.UI (Unpackaged)` 后按 F5。独立的 Gallery 解决方案仍可使用。

For Visual Studio debugging, open `src/XamlNexus.sln`, set `Samples > XamlNexus.Gallery.UI` as the startup project, select `Debug / x64` and the `Unpackaged` launch profile, then press F5. The standalone Gallery solution remains available.

以下为源码运行方式：

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

发布文件名为 `XamlNexus Gallery v{版本号}.zip`，版本号与 NuGet Tool 包一致。
Release assets use `XamlNexus Gallery v{version}.zip`, with the same version as the NuGet Tool package.

在仓库根目录运行：

```powershell
./eng/Publish-Gallery.ps1
```

产物位于 `.artifacts/gallery/`，解压 ZIP 后运行 `XamlNexus.Gallery.exe`。
包内包含 .NET 与 Windows App SDK 运行依赖；请保留完整目录，不要只复制 exe。
数据和设置写入 `%LOCALAPPDATA%/XamlNexus.Gallery`。它与旧 SqliteShowcase 数据目录隔离，不自动迁移或删除旧数据。

独立的 `Gallery portable build` 工作流在相关 PR 和手动触发时生成 x64 ZIP，
上传为 Actions artifact，不会自动发布 GitHub Release，正式发布时 ZIP 上传至同版本 GitHub Release，CLI 的 NuGet 包仅包含下载清单。
发布前需在没有开发环境的 Windows 设备上验证启动、源码查看和 SQLite 写入。

Build from the repository root with `./eng/Publish-Gallery.ps1`. Extract the complete ZIP and run
`XamlNexus.Gallery.exe`. The portable package includes .NET and Windows App SDK dependencies;
settings and demo data live under LocalAppData. A clean Windows machine smoke test is required before release.

## 源码来源 / Source provenance

- “源码模板 · CLI”直接嵌入 `src/XamlNexus.Common/Assets/ListPage/` 中的实际模板，不维护展示副本。
- “源码 · Gallery 示例”嵌入当前运行示例的页面、ViewModel 和数据服务。
- 文件选择器支持完整源码浏览、文本选择与复制。XAML / C# / CLI 提供轻量语法着色，随浅色和深色主题更新；高对比度使用系统文本色。
- 复制成功会显示提示；剪贴板不可用时提示重试或手动选择复制。复制内容始终是原始完整文本。
- 列表与 SQLite 页面提供“在你的项目中使用”；保活页面提供独立导航用法说明与实际示例源码。SQLite 组件不会自动生成 Gallery 的业务页。
- 发布包可离线查看源码，无需下载仓库。
- `__APP__` / `__NAME__` 是 CLI 生成时替换的占位符；Gallery 的演示开关属于示例代码。

The source browser embeds the actual CLI template files and Gallery example sources at build time.
Choose a file to read or copy its complete source offline. XAML, C# and CLI examples use theme-aware lexical highlighting with copy feedback; clipboard failures are recoverable. Each example includes integration commands, edit locations and a result checklist. Template placeholders are resolved by the CLI.

## 参数预览与生命周期对比 / Parameter preview and lifetime comparison

实际使用时，页面继承 `ArcPage` 并添加 `[KeepAlive]`，通过 XamlNexus 导航进入和离开，触发页面保留与复用。页面持有的 UI 控件、动画对象和 ViewModel 数据可随实例一起保留；动画播放、暂停和恢复仍由动画逻辑及生命周期回调控制。进程退出后的恢复需要另行持久化。

In an application, derive the page from `ArcPage`, add `[KeepAlive]`, and navigate through XamlNexus to trigger retention and reuse. The page retains its UI controls, animation objects and ViewModel data. Animation playback, pause and resume remain controlled by application logic and lifecycle callbacks. Restoring state after exit requires separate persistence.

展开列表页的“源码模板 · CLI”，填写项目名称和页面名称，即可预览替换后的完整源码与目标文件路径；复制按钮复制当前显示的内容。关闭“替换模板参数”可查看原始模板。名称以大写英文字母开头，后接字母或数字；无效输入会显示提示并退回原始模板，不写入磁盘。这里只预览四个列表模板文件，导航接入仍由 CLI 完成。

“页面保活”使用独立的 ArcNavigationContentView，在普通页面、KeepAlive 页面和导航离开页之间导航。输入文字并增加计数后，必须通过导航离开再返回：普通页面实例和 ViewModel 重建，KeepAlive 页面复用实例并保留状态。两列记录真实回调与实例编号，每类最多 20 条。仅添加特性或切换 Content / Visibility 不会自动获得导航缓存。外部持有的 ViewModel 可能独立保留数据，因此应结合实例编号判断。OnDestroy 是框架清理回调，不代表 GC，可能重复触发。

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
- **页面保活 / Page retention**：独立的输入框和计数器示例，使用真实导航对比普通页面与 `[KeepAlive]` 页面；无需跳转到列表示例。
- **数据与恢复 / Data & recovery**：使用 SQLite 演示数据编辑、备份恢复及通知，附使用提示与代码。
- **快速开始 / Getting started**：环境诊断、创建项目、添加组件及维护命令。
- **设置 / Settings**：Gallery 专用的主题、窗口材质、语言与关于页面。

示例页统一提供可操作演示、Tips 和可展开/复制的代码。切换应用语言时，Gallery 导航与说明同步更新。默认生成项目仍保持简洁；本次 Gallery 布局仅属于示例应用。

The retention example uses a dedicated ArcNavigationContentView with ordinary, cached and away pages. Enter text and increment the counter, navigate away, then return to compare instance IDs and state. Use XamlNexus navigation: the attribute alone does not cache manually replaced content. Lists & async loading remains independent and focuses on loading, cancellation and recovery.

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

## 窗口与页面基础 / Window and page fundamentals

“基础用法”新增 ArcWindow 与 ArcPage 介绍：标题栏、主题过渡、窗口跟踪、导航参数、生命周期和状态保留。页面提供体验入口，并嵌入 CLI WinUI 模板真实源码供离线查看。保活页采用宽屏双栏、窄屏纵向布局，事件记录和能力边界默认折叠。

Fundamentals includes ArcWindow and ArcPage introductions covering title bars, theme transitions, window tracking, navigation payloads, lifecycle hooks and retention. Each links to an interactive feature and embeds actual CLI WinUI template sources for offline reading. The retention page uses a responsive demo and guidance layout with collapsible lifecycle details.
