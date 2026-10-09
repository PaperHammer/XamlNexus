# 项目架构 / Architecture

## 工具与生成引擎 / Tooling and generation

依赖方向：`XamlNexus CLI → XamlNexus.Tooling → XamlNexus.Common`。
CLI 同时引用框架生成器和内置配方，后二者只依赖 Common，不依赖 Tooling。

- **Common**：配置校验、项目生成、项目清单、页面模板、配方协议与事务、检查与升级合并，以及本地化资源。
- **Tooling**：命令参数解析、交互向导、进度与结果展示、环境与项目诊断、开发进程运行、Gallery 下载与缓存。
- **CLI**：命令分发、输出、具体生成器与配方的组装，以及 Gallery 进程启动。

Common 不引用 Tooling。新增下载、进程管理和命令宿主功能放入 Tooling，避免重新进入生成引擎。
Common 不依赖 Spectre.Console，不读取终端或输出进度。`ProjectWizard` 收集完整的 `ProjectConfig`；
`GenerateProject(config, progress)` 返回 `GenerationResult`，成功时包含输出目录，失败时保留原异常和清理诊断。
同步进度回调只包含阶段、计数和当前条目；`GenerationConsole` 决定如何显示，`CreationReport` 仅在最终创建成功后展示结果。
组合创建沿用同一进度回调；普通升级通过 Tooling 展示目标项目生成进度，`--json` 模式只输出 JSON，无需重定向标准输出。原 `Generate(config)` 布尔便捷入口也不再输出。

Dependencies flow from CLI to Tooling to Common. Generators and built-in recipes depend only on Common.
Common owns configuration validation, generation, manifests, page templates, recipe transactions, upgrade merging and localization resources. It has no Spectre.Console dependency or terminal interaction.
Tooling owns the wizard, progress and result presentation, command parsing, diagnostics, development processes and Gallery distribution. CLI composes these services and launches Gallery.
`GenerateProject(config, progress)` returns the output path or original exception in a `GenerationResult`. Its synchronous callback carries structured stages, counts and items, without presentation markup.
`GenerationConsole` renders progress and `CreationReport` reports final success. Composition forwards the same progress contract; normal upgrades display target generation progress through Tooling, while `--json` emits only JSON without redirecting stdout. The boolean `Generate(config)` convenience entry is also silent.
Do not add a reverse reference from Common to Tooling.

## Gallery 公共 UI / Shared Gallery UI

WinUI 模板是公共 UI 实现的唯一源码来源：
`src/Templates/Winui3/Winui3_XamlNexus.UIComponent`。

Gallery 的 `XamlNexus.Gallery.UIComponent/SharedSources.props` 显式列出复用文件。
`eng/GallerySharedUI.targets` 在构建时将这些文件写入 Gallery 的 `obj/GallerySharedUI/`，只替换项目命名空间，
通过 Compile 和 Page 项参与正常编译。Page 的 Link 元数据保留原有 XAML 资源路径。
生成文件按配置、平台、目标框架和运行时隔离，内容未变时不重写。

修改公共控件、转换器、页面宿主或主题辅助逻辑时，应编辑模板源码。
Gallery 的窗口、导航、页面与其他有差异的文件继续独立维护；共享清单不会自动包含新文件。
若某个共享文件需要 Gallery 专用实现，先从共享清单移除，再添加本地实现。

生成给用户的项目仍携带完整源码，不依赖本仓库、Gallery、这个构建 target 或额外 UI 包。
Gallery 需要完整仓库才能从源码构建；便携包无需模板源码。

The WinUI template is the single source of truth for shared UI implementations.
Gallery explicitly selects files in `SharedSources.props`; `eng/GallerySharedUI.targets` adapts their namespace into intermediate files and compiles them normally.
XAML Link metadata preserves resource URIs. Outputs are isolated by configuration, platform, framework and runtime, and unchanged files are not rewritten.
Edit shared behavior in the template. Keep Gallery-specific windows, navigation and pages local.
To specialize a shared file, remove it from the allowlist before adding a local implementation.
Generated user projects remain self-contained. Building Gallery requires the repository; running its portable package does not.

## Gallery 工作流演示 / Gallery workflows

页面模板演示通过 `Gallery/GalleryPageModels.targets` 从 Common 的 ListPage、DetailsPage 和 FormPage 资源编译 ViewModel 与数据接口，只替换应用与页面名称。Gallery 通过专用适配器把模型接入 SQLite 业务存储，不复制模型行为；生成源码仍嵌入发布包用于离线浏览。

Gallery MainPanel 直接引用 `XamlNexus.Recipes.BuiltIn` 及其 Common 依赖。组件页调用生产组合规划器，项目演进页调用生产三方合并器；二者只进行内存预览，不调用 CLI 或写入项目。不要把 Gallery 的展示文案或业务演示反向加入 Common。

Gallery compiles page template models and interfaces directly from Common resources with substituted names. Its own adapter persists business records in SQLite. MainPanel references BuiltIn recipes and Common to preview the production composition planner and three-way merger in memory, without command execution or project writes.

## 页面退出与异步调度 / Page exit and async dispatch

WinUI 与 WinUI/WPF 模板保持同一语义。UI 线程上调用 `ArcPage.NavigateExitAsync` 可等待退出：正常完成返回 `true`，重新进入或更新的退出请求取代旧请求时返回 `false`；生命周期钩子异常传回调用方。现有 `NavigateExit` 入口仍记录异常，兼容原有导航代码。

退出钩子在移出视觉树之前运行，仍可访问页面上下文与 DataContext。退出请求独立持有并释放取消源；页面销毁只执行一次。覆写带 `CancellationToken` 的 `OnPreLeaveAsync` / `OnLeaveAsync` 可把取消传给业务操作；原有无参数覆写继续有效。目标页面由当前导航立即激活，旧页面延迟退出不会改变后续导航的活动页面。

`DispatcherQueue.EnqueueOrInvokeAsync` 保留异步操作的成功、异常与取消状态；队列拒绝工作时返回失败任务，调用方不会无限等待。

Both templates expose awaitable `NavigateExitAsync` on the UI thread. It returns false for superseded exits and propagates hook failures. The existing void entry point logs failures for compatibility. Leave hooks retain their context until visual-tree removal; each exit owns its cancellation source and destruction runs once. Token-aware hook overloads support cooperative cancellation while existing overrides remain valid. Navigation activates its target immediately, without relying on an earlier page's delayed cleanup. Dispatcher tasks preserve failure and cancellation, and rejected enqueue attempts fail promptly.

The headless behavior tests compile the actual template sources with UI boundary doubles; Gallery builds and startup checks cover real WinUI integration separately.

## 导航与窗口离开拦截 / Navigation and window leave guards

页面覆写 `CanLeaveAsync(ArcLeaveReason, CancellationToken)`，返回 `false` 时保留页面、Payload、DataContext 和上下文。使用 `ArcNavigationContentView.NavigateAsync` 获取导航是否成功的结果，宿主只在成功后更新侧栏选中项和返回历史。现有 `Navigate` 入口也经过拦截；重复选择当前页面不会重新加载草稿。

窗口通过 `NavigationContent` 指定导航宿主。`ArcWindow` 同步取消首次原生关闭，在 UI 队列中等待所有仍保留的页面确认，再重放关闭。确认期间保持窗口可见；`CanCloseAsync` 可覆写以检查窗口自己的数据，`RequestCloseAsync` 支持程序发起同一套关闭流程。其他 Closing 处理器必须先检查 `args.Cancel`，不得提前隐藏窗口。导航和关闭共享互斥的离开请求，不会叠加确认框。

新生成的表单与 Gallery 提供“保存并离开 / 放弃修改 / 继续编辑”。`TrySaveAsync` 仅在保存成功且草稿干净时允许离开；校验失败、服务异常和过期保存结果均保留草稿。旧工程的 ArcPage 没有新钩子时，页面生成器保留旧表单行为；要启用拦截，需要升级框架与宿主接入代码。

Override `ArcPage.CanLeaveAsync` to veto navigation or window close before state changes. Await `NavigateAsync` and update shell selection/history only after success. Windows expose their root navigation host through `NavigationContent`; the first native close is cancelled synchronously, confirmation runs on the UI queue, and only approved requests replay close. Other native close handlers must respect `args.Cancel`. Retained background pages are checked on window close as well. Duplicate leave requests are rejected while confirmation is pending. Generated forms offer save, discard and cancel; validation or save failure prevents leaving. Old ArcPage contracts keep compatible form generation until upgraded.

## 验证 / Validation

- `dotnet test src/XamlNexus.TemplateTests/XamlNexus.TemplateTests.csproj`
- `dotnet build src/XamlNexus/XamlNexus.csproj`
- `./eng/Publish-Gallery.ps1 -Architecture x64`（ARM64 使用 `arm64` / use `arm64` for ARM64）

便携包构建会逐一检查共享页面对应的 XBF 资源。模板 UI 或共享 target 变更会触发 Gallery CI。
发布前仍需验证实际启动和示例页面，构建成功不等同于资源加载成功。

Portable builds validate every shared page's XBF resource. Template UI and shared target changes trigger Gallery CI.
Verify startup and sample navigation before release; a successful build alone does not prove runtime resource loading.
