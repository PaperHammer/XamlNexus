# 统一导航接口

[English](navigation.md) | [简体中文](navigation.zh-CN.md)

纯 WinUI 和混合架构的 WinUI 前端使用同一套接口，位于生成项目的
`UIComponent/Navigation`。导航不涉及混合架构的后台进程或 RPC。

## 注册页面

`xamlnexus page add Orders` 生成页面、ViewModel 和 `UI/Navigation/OrdersNavigation.cs`：

```csharp
using System.Runtime.CompilerServices;
using MyApp.UIComponent.Navigation;

namespace MyApp.UI.Navigation;

internal static class OrdersNavigation {
    [ModuleInitializer]
    internal static void Register() => NavigationRegistry.Default.Register(new NavigationEntry(
        "Orders", typeof(MyApp.MainPanel.OrdersPage), "订单", Order: 10));
}
```

注册文件编译在 UI 程序集中，启动时自动执行。添加后重新编译运行；当前菜单读取启动时
的注册快照，不提供运行中安装、卸载页面的能力。页面需要继承 ArcPage；AppObjectFactory 支持无参构造和已注册服务的构造函数注入，
实例创建和页面生命周期仍由现有 ArcNavigationContentView 管理。

`NavigationEntry` 描述路由、页面类型、标题、可选资源键、图标字形、排序和是否放入底部菜单。
路由区分大小写，必须唯一；重复注册抛出异常，不覆盖已有页面。菜单按 Order、Route 排序，
显示标题与路由独立。内置路由为 `home` 和 `settings`，位于 `UI/Navigation/BuiltInNavigation.cs`。

翻译标题时在 UIComponent 的各语言 Resources.resw 添加同名字符串，并设置 TitleResourceKey。
标准菜单订阅 LanguageUtil.LanguageUpdated，切换语言后更新标题和提示文字；关闭窗口时解除订阅。

## 标准窗口与自定义窗口

`INavigationRegistry` 提供 Register、Entries 和 Resolve。默认实现为 NavigationRegistry，
`NavigationRegistry.Default` 是本进程共用的实例。标准 MainWindow 使用 NavigationMenu
创建菜单，将路由保存在 NavigationViewItem.Tag 中；选择时 Resolve(route) 得到页面类型，
再等待 NaviContent.NavigateAsync(pageType)，通过离开确认后才更新侧栏选中项。无需在窗口维护逐页 switch。

自定义窗口可以复用适配器：

```csharp
var menu = new NavigationMenu(myNavigationView, NavigationRegistry.Default);
menu.Select("home");
// 在选择事件中解析 SelectedItemContainer.Tag 并等待内容控件 NavigateAsync，成功后再更新选中项。
// 窗口关闭时调用 menu.Dispose()。
```

也可以直接遍历 Entries 渲染自己的控件，用 Resolve 解析路由。注册表接口不要求窗口名、
XAML 布局或菜单控件类型；自定义宿主负责选择处理、标题刷新和页面展示。

业务激活通过 MainWindow.NavigateActivationAsync 复用此导航和未保存拦截。启动参数与 Payload 格式见[应用激活与业务导航](app-activation.zh-CN.md)。

## 生成与迁移边界

page add 检查 `UIComponent/Navigation/INavigationRegistry.cs` 是否存在，存在时生成独立注册文件，
不读取或改写 MainWindow。自定义宿主必须消费注册表才能显示这些页面；CLI 的 automatic
表示已生成注册代码，并不验证自定义宿主是否正确使用接口。

旧项目缺少接口时只生成三个页面相关文件并提示手动接入；`--no-navigation` 也只生成这三个文件。
旧窗口可以保留现有导航，或迁移到本接口；不会由 page add 自动重写已有窗口或示例工程。
新生成的业务文件由用户维护，不登记为可删除的 Recipe；删除页面时一起删除对应注册文件。

--dry-run、文件冲突检查和事务回滚继续生效。模块 DI 生命周期与导航注册分别负责服务和页面，
新增导航不要求安装数据库，也不改变页面 ViewModel 的所有权。

## 页面进入失败时的回滚

等待 `NavigateAsync`，仅在成功后更新导航选中项。标准侧栏与业务激活已遵循这一约定。兼容方法 `Navigate` 会记录异常，但不返回可等待的结果。

离开确认通过后，导航宿主保存目标页的框架状态、合并参数、挂载页面并执行同步 `OnEnter`。进入与动画启动成功后，才提交目标激活和旧页退出。不支持的过渡类型在离开确认前拒绝；进入准备完成时观察到取消也会回滚。

失败时，本次新建的目标页会从宿主移除并清理一次，包括新建的 KeepAlive 页。复用后台页或同页更新参数时，恢复状态、Payload 引用及键值、DataContext 引用、上下文活动标记、显示属性和预离开标记。失败的重新进入不会取消已经等待中的退出任务；调用方传入的 Payload 字典也会恢复。

原始异常继续传给调用方，清理异常单独记录，不替换原始错误；后续导航可以重试。构造失败不会改变宿主，但构造函数抛异常前已分配的资源仍需自己清理。

恢复范围是框架管理的状态，Payload 值为浅快照。不会撤销已成功保存的数据、递归恢复业务对象、停止页面任意启动的工作，或逆转自定义外部副作用。`OnEnter` 返回后的异步加载错误应使用页面的错误与重试流程处理，`async void` 重写内部的异常也无法通过同步导航调用捕获。

无界面测试编译实际导航和页面源码，覆盖新建页、保留页、同页传参、取消、等待中的退出、清理异常与动画启动失败；原生动画效果和 XAML 绑定仍需运行验收。
