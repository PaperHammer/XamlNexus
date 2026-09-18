# 列表页面模板

[English](list-page.md) | 简体中文

```powershell
xamlnexus page add Projects --kind list --dry-run
xamlnexus page add Projects --kind list
xamlnexus run
```

适用于 winui 和 hybrid，以及 basic 和 standard。省略 `--kind` 或使用 `--kind blank` 保持原有普通页面行为。支持 `--project`、`--no-navigation`、`--dry-run`、`--json`；无自动导航契约时会提示手动接入。重复名称或任意目标文件冲突会在写入前拒绝整个操作。

## 生成内容

以 MyApp 和 Projects 为例：

- `MyApp.MainPanel/ProjectsPage.xaml`：搜索、刷新、进度条、错误提示、列表与空状态。
- `MyApp.MainPanel/ProjectsPage.xaml.cs`：页面进入、离开和卸载，语言事件订阅与清理。
- `MyApp.MainPanel/ViewModels/ProjectsViewModel.cs`：异步加载、过滤、刷新命令和取消管理。
- `MyApp.MainPanel/Services/ProjectsDataSource.cs`：记录模型、数据源接口与可运行的内存示例。
- `MyApp.UI/Navigation/ProjectsNavigation.cs`：导航注册（未禁用且项目支持时）。

这些文件属于业务代码，不登记为可移除 Recipe。新建页面不安装 SQLite，也不会更改已有服务注册。

## 替换示例数据

实现 `IProjectsDataSource.LoadAsync(CancellationToken)`，返回 `IReadOnlyList<ProjectsItem>`。在页面创建 ViewModel 的位置传入实现：

```csharp
public ProjectsViewModel ViewModel { get; } = new(new MyProjectsDataSource());
```

也可以从项目现有 DI 容器取得服务，再传给 ViewModel。默认构造函数使用示例数据源，不依赖额外的服务注册。hybrid 的数据访问应通过宿主客户端完成，不要在前端直接打开宿主数据库。

异步服务应传递取消令牌，不要用同步 I/O 阻塞 UI。ViewModel 的生命周期、搜索与命令应在 UI 线程调用；正常 await 会保留 UI 同步上下文。

## 行为约定

默认页面未标记 `[KeepAlive]`：离开后销毁，再次进入会重新创建页面与 ViewModel，因此重新加载且搜索条件重置。复用同一 ViewModel（例如页面保活）时才保留已加载数据和搜索条件；点击刷新重新读取。刷新期间禁止重复提交；失败保留上次数据并显示错误，可再次刷新重试。按名称或描述进行不区分大小写的本地搜索，不提供服务端分页。

页面预离开、销毁或卸载会取消加载。即使服务忽略取消，旧请求的结果和错误也不能覆盖后来进入页面发起的新请求。页面保活时同样执行取消逻辑。

搜索、刷新、空状态和错误标题提供中英文，跟随应用语言切换；文本定义在生成的 ViewModel 中，可改用应用资源键。页面标题、导航标题默认是页面名称，示例记录是演示内容，业务文本需自行维护。

模板提供列表交互基础，不包含数据库 CRUD、编辑、删除、分页或自动生成业务协议。

## 保留页面状态

```csharp
using MyApp.UIComponent.Attributes;

[KeepAlive]
public sealed partial class ProjectsPage : ArcPage
{
    // 保留页面实例，也保留其持有的 ViewModel。
}
```

在生成页面的现有类声明上添加特性，无需重复声明类。保活仅适用于当前进程；退出后恢复需另行持久化。即使页面保活，离开时仍取消加载并解除语言事件订阅。

仓库的 `samples/XamlNexus.Gallery` 提供 Gallery 风格的列表与 KeepAlive 对照示例，可以直接比较页面实例编号和搜索条件，并模拟慢请求、失败和重试。
