# 接入第一个业务页面（WinUI 前端）

[English](business-page.md) | [简体中文](business-page.zh-CN.md)

[XamlNexus.Gallery 主页](../../samples/XamlNexus.Gallery/XamlNexus.Gallery.MainPanel/MainPage.xaml.cs)
演示同一页面使用设置、日志和 SQLite。业务代码位于 MainPanel，未修改 App 启动流程。

## 一条命令生成页面

纯 WinUI 和 WinUI + WPF 混合架构的前端都可运行：

```powershell
xamlnexus page add Orders --dry-run
xamlnexus page add Orders
# 自定义导航，只生成页面和 ViewModel
xamlnexus page add Reports --no-navigation
# 从项目外调用或用于脚本
xamlnexus page add Customers --project <项目目录> --dry-run --json
```

命令生成 `MainPanel/OrdersPage.xaml`、对应代码文件和
`MainPanel/ViewModels/OrdersViewModel.cs`，并生成 `UI/Navigation/OrdersNavigation.cs`，通过统一导航注册表接入。
页面名使用 `Orders` 这样的名称：以英文大写字母开头，后续为英文字母或数字。
默认标题直接使用名称；需要翻译时按本地化文档添加资源。
自动生成仍使用现有 MainPanel、ArcPage 和 ObservableObject；这些前端基础结构需要保留。

生成的是用户业务代码，不会登记为可删除的 Recipe。后续直接编辑即可；如需移除页面，
同时删除页面、ViewModel 和对应导航注册文件。两种架构共用前端生成逻辑，不更改混合架构后台或自动创建 RPC。
项目存在 `UIComponent/Navigation/INavigationRegistry.cs` 时生成导航注册文件；不读取或改写 MainWindow。
旧项目缺少此接口时只生成三个业务文件并提示手工接入。`--no-navigation` 显式跳过注册文件生成。
自定义窗口需要消费同一注册表，详见 [统一导航接口](../technical/navigation.zh-CN.md)。
JSON 输出的 `navigation` 为 `automatic` 或 `manual`，预览同样显示是否需要手工接入。
文件重名、项目结构错误或不安全路径会拒绝写入；写入失败复用现有事务回滚。
手工注册的重复路由由启动时的注册表检查并报错，CLI 不扫描 C# 注册代码。
自定义外壳替代 MainWindow 时允许页面生成，但命令不会修复原外壳或原有 XAML 语法错误。
生成不会修改 App.xaml.cs，也不要求 SQLite。页面级 ViewModel 由页面持有，通过 AppObjectFactory 创建；构造参数从模块注册的应用服务中解析。
`--dry-run` 只列出变更文件，`--json` 可用于脚本。添加后执行 `xamlnexus run`。
默认生成空白 ViewModel 和普通页面骨架，不包含下文的 Query 示例。

## 不依赖数据库的页面起点

普通页面不需要先安装 SQLite。以下以生成项目 `MyApp` 为例，先在
`MyApp.MainPanel/ViewModels/OrdersViewModel.cs` 创建页面状态：

```csharp
using MyApp.Models.Mvvm;

namespace MyApp.MainPanel.ViewModels;

public sealed class OrdersViewModel : ObservableObject {
    private string _query = string.Empty;
    public string Query {
        get => _query;
        set {
            if (_query == value) return;
            _query = value;
            OnPropertyChanged();
        }
    }
}
```

修改命令已生成的 `OrdersPage.xaml` 和 `OrdersPage.xaml.cs`，保留已有 `ArcPage`：

```xml
<arc:ArcPage
    x:Class="MyApp.MainPanel.OrdersPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:arc="using:MyApp.UIComponent.Templates">
    <StackPanel Padding="24" Spacing="12">
        <TextBox Text="{x:Bind ViewModel.Query, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
        <TextBlock Text="{x:Bind ViewModel.Query, Mode=OneWay}" />
    </StackPanel>
</arc:ArcPage>
```

```csharp
using System;
using MyApp.MainPanel.ViewModels;
using MyApp.Common.Utils.DI;
using MyApp.UIComponent.Templates;

namespace MyApp.MainPanel;

public sealed partial class OrdersPage : ArcPage {
    public override Type ArcType => typeof(OrdersPage);
    public OrdersViewModel ViewModel { get; } = AppObjectFactory.Create<OrdersViewModel>();
    public OrdersPage() => InitializeComponent();
}
```

在 `MyApp.UI/Navigation/OrdersNavigation.cs` 注册页面：

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

这是 `page add` 生成的注册方式。标准窗口自动显示菜单，无需修改窗口的 XAML 或 switch。

简单页面状态由页面自身持有即可，不为了使用 DI 而注册没有依赖的对象。
需要共享服务时，通过现有模块注册到容器，并明确页面状态的生命周期。
先完成输入与显示绑定，再按业务需求增加数据库、通知和本地化。

## SQLite 组合示例：页面与服务（纯 WinUI）

在生成项目中执行 `xamlnexus add sqlite --project <项目目录>`，Recipe 会为标准 MainPanel 工程
增加对 `<项目名>.Data.csproj` 的 ProjectReference。SQLite Recipe 的模块负责注册服务、
启动迁移；页面不再自行初始化数据库。详见 [模块生命周期](../technical/module-lifecycle.zh-CN.md)。

页面继承 `ArcPage` 并重写 `ArcType`。新模板的导航使用 AppObjectFactory 创建页面，
支持无参构造以及从 DI 注入已注册服务的构造函数。
本例在页面构造时从 `AppServiceLocator.Services` 取得以下应用所有的服务：

| 服务 | 页面用途 |
|---|---|
| `IUserSettingsClient` | 读取、保存排序偏好 |
| `IDbContextFactory<AppDbContext>` | 每次数据库操作创建独立上下文 |
| `SqliteDatabaseInitializer` | 用户点击时执行完整性检查 |

这些共享服务由应用容器管理，页面不要 Dispose 它们。每次操作创建的 DbContext
使用 `await using` 释放，避免把跟踪状态或长期数据库上下文放进页面。

本例直接扩展现有主页。若新增独立页面，使用 `page add` 或上述注册方式。旧版 XamlNexus.Gallery 的窗口仍使用手工导航映射；
其现有主页示例不受影响。无需在 App.xaml.cs 增加业务初始化代码。

## 设置与数据

在 Models 的 `ISettings` 和 `Settings` 同时增加 `bool RecentEntriesFirst`。
现有 JSON 源生成上下文包含 Settings；旧配置没有该属性时使用默认值 false。
不要把这个业务专属偏好加入通用模板。

读取设置后初始化复选框；用户点击时更新内存并等待
`SaveAsync<ISettings>()`。保存失败恢复原值及复选框，向用户显示错误。
保存成功再按偏好查询：最近更新时间降序，并以键排序保证相同时间时顺序稳定。

选中记录会将键和值填入编辑框。保存相同键更新记录，保存新键插入记录；
修改键表示写入另一条记录，并不是重命名原记录。删除只针对当前选中项。

数据库和设置文件是两个独立存储，不构成跨存储事务。本例的排序偏好保存不修改
业务记录。数据库提交成功后若刷新失败，提示“已保存，列表刷新失败”，避免把
后续读取失败描述成写入失败。

## 日志、错误与页面生命周期

通过 `ArcLog.GetLogger<MainPage>()` 记录操作名称及异常，不主动记录用户输入值。
日志位于 `%LOCALAPPDATA%\XamlNexus.Gallery\logs\UI`。异常本身可能包含路径等诊断信息。

页面使用统一 RunAsync 捕获操作异常、显示提示，并在 finally 恢复控件。
执行期间禁用编辑、列表、按钮和排序选择，同时用忙碌标志拒绝重复操作。
Loaded 时重新加载数据；本例不持有长期数据库上下文，本地化事件的订阅与解绑见下文。
若业务页面订阅了共享服务事件，需在页面离开或销毁时取消订阅。

## 手动验收

1. 新增一个唯一键，选中后修改值并保存，刷新确认只有一条对应记录。
2. 切换“最近更新优先”，退出后重开，确认记录和排序选择保留。
3. 提交空键应显示校验提示；不应写入数据库。
4. 让测试应用专属设置文件不可写后切换排序，确认错误、选择回退和控件恢复；
   解除限制后再次保存应成功。
5. 删除测试记录，刷新确认消失，完整性检查返回 ok；检查操作和错误日志。

示例现在提供中英文资源，XAML 使用 `Uids.Uid`，运行状态保留资源键与参数，
在 LanguageUpdated 时重新生成译文。页面在 Loaded 订阅、Unloaded 取消订阅，
托盘服务在 Dispose 时取消订阅。接入方式见 [本地化文档](localization.zh-CN.md)。

“发送提醒”通过 Models 中的 `INotificationService` 调用 UI 托盘实现。
MainPanel 不引用 UI 工程；点击时从应用服务容器取得接口，避免窗口构造期间
提前解析依赖主窗口的托盘服务。成功提示只表示请求完成，Windows 决定最终显示。
已有设置、日志和数据库操作保持独立，通知失败不会撤销之前保存的数据。


## ViewModel 构造函数注入（新模板）

纯 WinUI 项目运行 `xamlnexus add sqlite`、`xamlnexus page add Orders` 后，
在生成的 OrdersViewModel 中增加构造函数即可，无需修改 App 或窗口：

```csharp
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyApp.Data.Persistence;

private readonly IDbContextFactory<AppDbContext> _database;
public OrdersViewModel(IDbContextFactory<AppDbContext> database) {
    _database = database;
}

public async Task<int> CountAsync() {
    await using var context = await _database.CreateDbContextAsync();
    return await context.AppState.CountAsync();
}
```

混合架构改为注入已有 `IAppStateClient`，通过 RPC 操作后台持有的数据库；前端不引用 Data。
现有 XamlNexus.Gallery 保留原有写法，新生成项目使用工厂。旧项目缺少 AppObjectFactory 时，
page add 保留 `new()`，不会生成无法编译的工厂调用。

工厂通过 ActivatorUtilities 创建新的业务对象，构造参数必须是已注册的服务；它不会递归
注册任意依赖，也不会吞掉缺少服务的错误。无依赖 ViewModel 无需注册到容器。

页面与 ViewModel 的实例由页面导航持有，保活页面继续复用原实例。工厂创建的对象不由
应用容器自动 Dispose；若业务 ViewModel 自己持有资源，页面需在合适的 OnLeaveAsync / OnDestroy
中释放，并保证重复销毁回调安全。注入的共享服务由容器管理，页面不要释放它们。
当前创建入口使用应用级服务提供器，不为每页建立 DI scope；数据库上下文按操作创建并释放，
需要 scoped 服务的业务操作应显式创建 scope，避免将其长期保存在页面上。
