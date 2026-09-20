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

## 验证 / Validation

- `dotnet test src/XamlNexus.TemplateTests/XamlNexus.TemplateTests.csproj`
- `dotnet build src/XamlNexus/XamlNexus.csproj`
- `./eng/Publish-Gallery.ps1 -Architecture x64`（ARM64 使用 `arm64` / use `arm64` for ARM64）

便携包构建会逐一检查共享页面对应的 XBF 资源。模板 UI 或共享 target 变更会触发 Gallery CI。
发布前仍需验证实际启动和示例页面，构建成功不等同于资源加载成功。

Portable builds validate every shared page's XBF resource. Template UI and shared target changes trigger Gallery CI.
Verify startup and sample navigation before release; a successful build alone does not prove runtime resource loading.
