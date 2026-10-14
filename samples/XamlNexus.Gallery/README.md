# XamlNexus Gallery

Gallery 是 XamlNexus 的交互式应用开发工作台。三个主要能力位于首页与导航首层：**生成页面、组合组件、持续演进项目**。窗口与页面机制作为进阶示例收纳，源码和接入说明按需展开。

Gallery is an interactive workspace for building applications with XamlNexus. Its main workflows are **page generation, component composition and project evolution**. Framework examples and source inspection remain available alongside these workflows.

## 内容与导航 / Content and navigation

- **首页 / Home**：页面模板预览、三条开发流程、可运行示例与最近访问。所有入口直接打开对应体验。
- **快速开始 / Getting started**：环境检查、创建运行、添加页面和存储。
- **页面模板 / Page templates**：切换 list、details、form、blank。体验真实模板的列表加载与筛选、详情失败与重试、表单校验与 SQLite 保存；列表记录可打开详情并编辑，保存后返回列表，重启后仍保留；新增路由 / JSON 参数导航预览，打开所选记录的详情或编辑页，取消未保存提示后保留草稿；展开查看生成文件、调用流程和 CLI 模板源码。
- **组件组合 / Component composition**：选择 WinUI / hybrid、basic / standard 和功能，实时查看实际依赖顺序、自动补充的依赖、已有组件和对应创建命令。
- **项目演进 / Project evolution**：编辑本地源码，切换独立修改与重叠修改，查看真实三方合并的结果或冲突文档。查看基线与维护命令。
- **数据与恢复 / Data & recovery**：SQLite 的增删改查、迁移、WAL、完整性检查、备份与恢复。
- **窗口与页面机制 / Windows and pages**：列表异步加载、窗口打开与管理、导航传参与加载、返回后继续编辑。
- **命令参考 / Command reference**：可复制的创建、运行、页面生成与维护命令；同时说明单实例激活、混合宿主安全关闭及当前支持边界；框架源码和无界面验收命令按需展开。
- **设置 / Settings**：Gallery 的主题、窗口材质、语言与关于页面。

搜索支持中英文场景名称、描述和 API / CLI 关键词，例如 KeepAlive、form、activation、route、单实例、关闭、merge、sqlite。切换语言立即更新导航与说明；浅色、深色及高对比度使用共享主题资源。宽窗口的组件配置与结果、合并输入并排显示，窄窗口自动堆叠。

Search supports scenario names, descriptions and API / CLI keywords in both languages. Configuration/results and merge inputs stack on narrower windows.

## 演示与真实能力 / What the demonstrations use

页面模板的 ViewModel 和数据接口由 `Gallery/GalleryPageModels.targets` 在构建时直接从 `src/XamlNexus.Common/Assets/{ListPage,DetailsPage,FormPage}/` 转换命名空间并编译。演示使用这些真实模型，Gallery 注入专用 SQLite 业务数据服务及本地化内容。修改模板后重新构建即可同步运行行为与离线源码。

组件页调用 `CompositionPlanner.ResolveForCreation` 和 `BuiltInRecipeCatalog.Create`；项目演进页调用 `XamlNexusThreeWayTextMerge`。这些页面只在内存中预览，不创建项目、不执行 CLI，也不改写用户文件。编辑框的换行在进入合并前统一到示例基线的 LF 格式。

业务参数演示通过 SharedSources.props 直接编译纯 WinUI 模板的 BusinessActivationRequest、BusinessActivationNavigator、ActivationCommandLine 和导航注册表。Gallery 将示例路由 OrderDetails、OrderEditor 适配到现有详情/表单模型，路由允许后才更新记录与标签；同页切换同样经过保存/放弃/取消确认。输入路由与 JSON 可生成可复制的 PowerShell 7 命令，Gallery 不执行它。

This preview compiles the production parser and router and adapts example routes to its existing business models. Confirmation precedes record and tab changes. Generated launch commands are copyable text and never execute inside Gallery.

Gallery 自身仍使用 Mutex 与独立前台窗口，没有托盘、SDK 激活队列或 WPF 宿主。此演示证明页内业务参数接入；不能替代生成应用的跨进程激活、焦点、MSIX 或混合宿主退出验收。命令参考可离线浏览实际模板的单实例入口、激活队列、兼容处理和关闭协调器，验收命令在 XamlNexus 仓库根目录运行。详见[应用激活](../../docs/technical/app-activation.zh-CN.md)与[混合关闭保护](../../docs/technical/hybrid-close.zh-CN.md)。

Gallery retains its own foreground window and Mutex entry point, without a tray, SDK activation queue or WPF host. Its page preview does not prove cross-process activation or hybrid shutdown. The command guide embeds the actual template lifecycle sources for offline inspection and lists repository acceptance commands.

业务模板提供可运行的页面起点，仍需接入自己的应用服务与导航。CLI 默认模板表单的保存仍是示例服务行为；Gallery 通过 `GalleryBusinessDataSource` 和 `GalleryBusinessStore` 显式接入 SQLite，记录使用 `gallery.business/` 键前缀，与普通数据演示记录分开。SQLite Recipe 提供数据层；Gallery 的 CRUD、备份和恢复界面是独立实现，不会由组件安装自动生成。

The Gallery compiles the actual CLI template ViewModels and service interfaces at build time. Its composition and evolution workflows call the production planner and merge engine. Previews are in memory; they never run commands or write projects. Generated template forms use a sample service; Gallery connects these ViewModels to its own SQLite adapter for persistent business records. The SQLite business interface is a custom demonstration rather than generated Recipe output.

## 运行 / Run

安装正式工具包后：

~~~powershell
dotnet tool update --global XamlNexus
xamlnexus gallery
~~~

首次安装将 update 改为 install。工具包固定对应的 x64 Gallery 版本；首次启动下载 ZIP 并验证 SHA-256，缓存后可离线启动。升级工具后关闭旧 Gallery，再运行上述命令获取对应版本。开发分支的本地修改需要从源码构建。

程序缓存位于 `%LOCALAPPDATA%/XamlNexus/Gallery`；用户设置、日志和数据库位于 `%LOCALAPPDATA%/XamlNexus.Gallery`，二者分开管理。

Official tool packages download and verify the pinned x64 Gallery release, then launch cached copies offline. Local development changes require a source build.

在 Visual Studio 打开 `src/XamlNexus.sln`，将 Samples > XamlNexus.Gallery.UI 设为启动项目，选择 Debug / x64 与 Unpackaged 启动配置。也可从本目录运行：

~~~powershell
dotnet restore XamlNexus.Gallery.sln -p:NuGetAudit=false
dotnet build XamlNexus.Gallery.sln -m:1 -p:Platform=x64 -p:UseSharedCompilation=false -p:NuGetAudit=false
dotnet run --project XamlNexus.Gallery.UI/XamlNexus.Gallery.UI.csproj -p:Platform=x64
~~~

## 数据与恢复 / Data and recovery

首次启动执行数据库迁移，数据文件是 `%LOCALAPPDATA%\XamlNexus.Gallery\Data\app.db`。

SQLite 页选中记录后可编辑，同键保存为更新、新键为新增。排序偏好随设置持久化，操作期间禁止重复提交。成功与异常日志位于 `%LOCALAPPDATA%\XamlNexus.Gallery\logs\UI`。业务接入见 [第一个业务页面](../../docs/user-guide/business-page.zh-CN.md)。

创建备份会保存完整数据库。选择备份并确认恢复后替换当前数据；恢复前自动保留安全备份，可再次恢复以撤销。备份位于 `%LOCALAPPDATA%\XamlNexus.Gallery\Data\Backups`，不会自动清理。导出时复制已完成的 .db；导入时放入该目录并刷新。仅接受迁移版本兼容的备份，本入口不承担无法启动时的离线修复。

语言、主题、窗口材质在保存失败时回退；Gallery 不提供开机启动或存储目录切换入口。KeepAlive 仅保留当前进程中的页面实例，进程退出后的恢复需要业务持久化。

## 源码与维护 / Source and maintenance

- 模板源码直接嵌入 CLI 实际使用的 ListPage、DetailsPage、FormPage 文件。
- 示例源码嵌入当前 Gallery 的 XAML、后台代码、ViewModel 和数据服务，发布包可离线浏览。
- 源码浏览支持名称参数预览、文本选择和完整复制；无效名称回退到原始模板，不写磁盘。
- 源码使用轻量着色，高对比度跟随系统文本色。复制反馈与错误提示就地显示。
- 中英文文案必须同时更新 `Gallery/Strings/en-US.json` 与 `zh-CN.json`；共享颜色、卡片和排版维护在 `GalleryStyles.xaml`。
- `GalleryCatalog.cs` 维护场景入口、搜索关键词和最近访问；`GalleryHomeContent.cs` 选择首页流程与示例。
- 页面和窗口独立维护，公共 UI 复用方式见 [项目架构](../../docs/architecture.md)。Gallery 重构无需修改 `src/Templates/` 的默认项目布局。

导航使用独立的短标签，完整标题用于页面、卡片、搜索和悬停提示。主标题统一使用 PageTitle（30 / 38），章节使用 SectionTitle（20 / 28），卡片使用 CardTitle（16 / 24）；修改字号应维护共享样式。

Navigation uses compact labels independently of full page titles. Page, section and card headings share the PageTitle, SectionTitle and CardTitle styles.

Gallery pages use paired XAML/code-behind files. Public UI and page models reuse their original sources, while Gallery-specific interaction and presentation remain local. Generated user applications stay self-contained.

## 便携发布 / Portable build

在仓库根目录运行：

~~~powershell
./eng/Publish-Gallery.ps1 -Architecture x64
~~~

产物位于 `.artifacts/gallery/`，ZIP 命名为 XamlNexus Gallery v{版本号}.zip。解压后保留完整目录并运行 `XamlNexus.Gallery.exe`；包内包含 .NET 与 Windows App SDK 依赖。

Gallery portable build 工作流在相关 PR 和手动触发时构建 x64 包并上传 Actions artifact，不自动发布 Release。正式发布时 ZIP 与 NuGet Tool 使用相同版本，工具包只携带下载清单。发布前仍需在无开发环境的 Windows 设备验证启动、源码查看和 SQLite 写入。

Portable packages include their runtime dependencies. CI produces an artifact; release publication is separate. A clean Windows machine smoke test remains required before release.

## 业务流程验收 / Business workflow acceptance

自动测试覆盖新建、编辑、重复名称、数据库重开、取消和非法写入，以及与普通演示记录的隔离。Gallery CI 执行这些测试；UI 交互仍需单独验证。

Automated tests cover persistence, identity, cancellation and data isolation. Verify the UI separately:

1. 新建记录，填写名称与备注并保存；返回列表，搜索并打开详情。 / Create, save, search and open a record.
2. 编辑记录并保存；返回列表，确认内容更新且记录没有重复。 / Edit and verify the refreshed list.
3. 体验加载失败，再重试；确认恢复显示记录。 / Trigger a read failure and retry.
4. 修改表单后切换标签、侧栏页面或关闭窗口；继续编辑应保留草稿及原侧栏选中项，放弃修改应保留数据库原值。保存并离开必须等待成功，必填字段错误或写入失败应留在原页。 / Test tab, sidebar and window-close guards: keep editing preserves state, discard leaves stored data intact, and save-and-leave only proceeds after a successful save.
5. 重启 Gallery；确认记录仍在，并可再次编辑。 / Restart and edit the persisted record.
6. 选中记录，分别填入详情或编辑路由并应用；修改表单后再次应用同页另一条记录，验证保存/放弃/继续编辑。未知路由、无效 JSON 和详情缺少 ID 不应改变业务页面；检查复制命令包含正确记录 ID。 / Apply record routes and test draft retention, invalid inputs and copied commands.
7. 切换中英文、浅深主题及窄窗口；检查新演示按钮、反馈、标题和生命周期离线源码。 / Check languages, themes, narrow layout and offline lifecycle source.
