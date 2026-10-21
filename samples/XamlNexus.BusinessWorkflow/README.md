# Standalone business workflow / 独立业务闭环样例

此样例由当前 CLI 生成纯 WinUI basic 应用，再安装 SQLite、生成 Orders / OrderDetails / OrderEditor 三个页面并应用业务接入代码。仓库只保存可重复生成脚本与业务定制，不维护第二份框架模板。生成应用不引用 Gallery 或 CLI 运行库。

This sample creates a pure WinUI basic application through the current CLI, installs SQLite, generates three business pages, and applies application-owned adapters. The generated app has no Gallery or CLI runtime dependency.

## 创建与运行 / Generate and run

在仓库根目录使用 PowerShell 7 与 Windows WinUI 构建环境：

~~~powershell
pwsh -NoProfile -File eng/New-BusinessWorkflow.ps1
# 或指定新项目的父目录；已有 BusinessWorkflow 目录不会覆盖
pwsh -NoProfile -File eng/New-BusinessWorkflow.ps1 -Output D:/Projects/Samples
~~~

脚本打印实际项目路径。默认位于唯一临时目录，保留产物供检查；避免 IDE 在开放仓库中发现并同时构建生成工程。需要长期保留时显式指定自己的输出目录。

进入打印的 BusinessWorkflow 根目录：

~~~powershell
xamlnexus run
~~~

当前源码的样例建议在仓库根目录使用已经构建的 CLI：

~~~powershell
dotnet src/XamlNexus/bin/Debug/net8.0/XamlNexus.dll run --project '<打印的项目路径>'
~~~

项目基本流程：New order → 保存并返回 Orders → 点击列表记录 → Edit → 修改并保存 → 退出后重开。数据库默认位于 %LOCALAPPDATA%/BusinessWorkflow/Data/app.db。

Use the printed project root to build/run. The list, details and editor share one SQLite adapter. The default database is under %LOCALAPPDATA%/BusinessWorkflow/Data/app.db.

编译后可从 UI 可执行文件目录使用 PowerShell 7：

~~~powershell
& ./BusinessWorkflow.UI.exe --route OrderDetails --data '{"id":"替换为实际记录的32位GUID"}'
& ./BusinessWorkflow.UI.exe --route OrderEditor --data '{"id":"替换为实际记录的32位GUID"}'
& ./BusinessWorkflow.UI.exe --route OrderEditor --data '{}'
~~~

内部按钮和外部参数导航都调用 MainWindow.NavigateActivationAsync，保留侧栏选择与未保存确认。路由区分大小写，ID 是字符串；缺失记录会显示读取错误。导航服务在动作发生时才取得主窗口，避免默认 Orders 页面构造期间产生 DI 依赖环。

Internal actions use the same guarded navigation as launch arguments. Window resolution is deferred until an action to avoid a constructor dependency cycle.

## 数据接入与生成边界 / Data integration

- SQLite Recipe 提供 Data 工程、迁移、工厂、初始化模块和工程引用。
- 页面与 ViewModel、数据接口、未保存确认仍来自实际 page add 模板。
- 业务脚本为 OrdersItem 增加独立 Id，重复名称不会混淆记录。
- OrderDataSource 实现三个生成接口，所有页面使用同一个 OrderStore。
- OrderStore 由 Gallery 已验证的业务存储源码转换名称和 orders/ 前缀后生成；没有复制 Gallery UI 或引入 Gallery 工程。
- Overlay 中的模块、导航接口与按钮逻辑属于用户业务代码。生成脚本检查修改锚点；模板契约变化时明确失败，不静默跳过接线。
- 保存按钮等待 TrySaveAsync 成功才返回列表；侧栏、返回按钮、同页激活和关闭仍使用原离开保护。

The Recipe supplies the database infrastructure. Business code owns identity, adapters and page actions. Generated files remain editable; these additions are an integration example, not automatic CRUD generation by the SQLite Recipe.

## 无界面验收 / Headless acceptance

~~~powershell
pwsh -NoProfile -File eng/Test-BusinessWorkflow.ps1
~~~

证据保存在 .artifacts/business-workflow-*/results.json 及对应日志，生成应用与目标脚手架目录均保留。数据库使用该次验收专属路径，不读取或修改用户默认数据库。

验收包含：

1. 当前 CLI 创建、安装 SQLite、生成三种页面，接入 DI 后编译完整 WinUI 应用。
2. 探针直接编译生成产物的 ViewModel、数据接口、适配器、激活解析与路由源码，并引用生成的 Data 工程。
3. 用生成表单新建和编辑，再用列表刷新与详情读取核对 ID 和内容。
4. 通过真实 SQLite trigger 拒绝更新，验证保存失败保留脏状态且数据库原值不变；检查取消写入。
5. 启动独立探针进程重新打开数据库，验证持久化和激活 ID；回调拒绝导航时保留草稿。
6. 从第二份干净 CLI 脚手架构造可控目标修订，通过真实升级 API 合并独立源码和工程 XML 修改，保留本地 Orders 默认路由、SQLite 引用、Recipe 归属、业务文件与数据。
7. 验证多组无唯一标识的工程 XML 仍需人工处理；再制造重叠默认路由修改，核对 XU2011 冲突与拒绝应用后的源码、清单及数据不变；升级后重新校验和构建应用。

The probe uses actual generated sources and a real isolated SQLite database. Persistence is verified in separate processes. Maintenance uses controlled target scaffold revisions and the production upgrade API; it does not claim a migration between historical released CLI versions.

## 人工 UI 验收 / Manual UI acceptance

无界面探针不构造 WinUI 窗口。它验证数据、模型和路由回调契约，不能证明实际 ContentDialog、焦点、侧栏及窗口关闭行为。实际 SDK 跨进程转交另由 eng/Test-AppInstance.ps1 验证。

1. 新建、保存、列表、详情、编辑、返回及应用重开；检查记录 ID 和内容一致。
2. 修改编辑页，再点返回、侧栏或关闭：继续编辑保留草稿；放弃保留数据库旧值；保存并离开等待成功。
3. 用二次启动打开同页另一记录，确认旧草稿不被直接替换。
4. 使用未知路由、错误 JSON 和不存在的 ID，检查原页面或就地错误状态。
5. 切换语言、主题、缩窄窗口，检查布局；新增业务标签沿用英文示例，可按本地化文档扩展。
6. 打包、安装升级与实际发布版本迁移须另行验收。

Headless evidence does not prove native dialogs, focus, sidebar behavior, packaged runtime, or installation upgrade. Follow the checklist with the generated app.

接入契约见[业务页面](../../docs/user-guide/business-page.zh-CN.md)、[激活](../../docs/technical/app-activation.zh-CN.md)和[脚手架升级](../../docs/user-guide/project-upgrade.zh-CN.md)。
