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
再调用 NaviContent.Navigate(pageType)。无需在窗口维护逐页 switch。

自定义窗口可以复用适配器：

```csharp
var menu = new NavigationMenu(myNavigationView, NavigationRegistry.Default);
menu.Select("home");
// 在选择事件中解析 SelectedItemContainer.Tag 并调用内容控件 Navigate。
// 窗口关闭时调用 menu.Dispose()。
```

也可以直接遍历 Entries 渲染自己的控件，用 Resolve 解析路由。注册表接口不要求窗口名、
XAML 布局或菜单控件类型；自定义宿主负责选择处理、标题刷新和页面展示。

## 生成与迁移边界

page add 检查 `UIComponent/Navigation/INavigationRegistry.cs` 是否存在，存在时生成独立注册文件，
不读取或改写 MainWindow。自定义宿主必须消费注册表才能显示这些页面；CLI 的 automatic
表示已生成注册代码，并不验证自定义宿主是否正确使用接口。

旧项目缺少接口时只生成三个页面相关文件并提示手动接入；`--no-navigation` 也只生成这三个文件。
旧窗口可以保留现有导航，或迁移到本接口；不会由 page add 自动重写已有窗口或示例工程。
新生成的业务文件由用户维护，不登记为可删除的 Recipe；删除页面时一起删除对应注册文件。

--dry-run、文件冲突检查和事务回滚继续生效。模块 DI 生命周期与导航注册分别负责服务和页面，
新增导航不要求安装数据库，也不改变页面 ViewModel 的所有权。
