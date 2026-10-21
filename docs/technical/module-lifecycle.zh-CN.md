# 生成应用的模块生命周期

[English](module-lifecycle.md) | [简体中文](module-lifecycle.zh-CN.md)

两种官方架构在能力所属应用工程中提供相同的两阶段生命周期：

1. `ConfigureServices(IServiceCollection)`：服务提供器构建前执行。
2. `InitializeAsync(IServiceProvider, CancellationToken)`：服务提供器已存在、应用尚未接收工作时执行。

纯 WinUI 在显示第一个窗口前完成初始化。混合架构在 WPF 后台宿主中完成初始化后，才启动命名管道 gRPC 服务。

混合后台模块目录还支持 `IXamlNexusGrpcModule`，其绑定在核心服务之后、管道服务启动前添加。WinUI 前端也有自己的模块目录，负责客户端服务注册与初始化。

Recipe 模块在宿主的 Modules 命名空间实现 `IXamlNexusModule`，通过模块初始化器注册工厂：

```csharp
[ModuleInitializer]
internal static void Register() =>
    XamlNexusModuleCatalog.Register(static () => new MyModule());
```

显式工厂引用兼容生成项目的裁剪设置，避免运行时扫描程序集。模块按类型全名排序实例化和执行，启动顺序是确定的。初始化异常中止启动，不允许 UI 或 IPC 在部分初始化状态下接收工作。

内置 SQLite Recipe 使用这一流程注册上下文工厂，并在数据库工作可达前完成迁移。

## 在页面中使用模块服务

新前端模板通过 `AppObjectFactory` 创建导航页面；page add 也用它创建页面持有的 ViewModel。构造参数来自现有应用服务提供器，Page 或 ViewModel 自身无需注册。无参类型仍可使用；缺少构造依赖时明确失败。

工厂始终创建由调用方持有的新对象，不创建每页 DI scope，也不自动释放业务对象。KeepAlive 继续控制页面复用；持有资源的 ViewModel 需要在页面生命周期中显式清理。页面不能释放容器拥有的共享服务。数据库操作使用上下文工厂或显式操作 scope。SQLite 注入示例见[页面开发](../user-guide/business-page.zh-CN.md)。

## 应用退出与资源释放

生成的纯 WinUI、混合前端和 WPF 宿主都会等待 DI 容器异步释放，支持服务的 `IDisposable` 与 `IAsyncDisposable`。共享资源服务应由容器管理；调用方创建的页面、ViewModel 仍由自己的生命周期负责清理，容器不会自动接管它们。

主 WinUI 窗口的最终退出处理器在模块初始化后注册。已有离开确认与模块安装的托盘策略先执行；真正退出时暂缓关闭，保留隐藏窗口等待清理，再退出应用。自定义关闭策略应在初始化期间注册，先于最终处理器，并保留 `RequestCloseAsync`。

混合宿主先等 UI 实际退出，再停止激活与 RPC、在服务仍可用时保存设置、关闭自己的 Job、等待容器释放并移除通知。重复宿主退出请求等待清理结束。退出获准后，调用方取消不再中断资源释放。

`ApplicationCleanup` 逐阶段记录异常并继续后续应用清理。这不保证失败的 DI 容器内部每个服务都已释放：某个服务抛异常可能中断容器自己的释放序列。资源释放应可靠并尽快完成；框架不以超时强制跳过。

异步容器释放可能在线程池继续释放其余服务。持有 XAML/WPF 资源的服务须自行切回 UI dispatcher；托盘 Recipe 1.0.1 提供 `DisposeAsync`，在窗口线程释放托盘资源。参见 [.NET 8 DI 实现](https://github.com/dotnet/runtime/blob/v8.0.0/src/libraries/Microsoft.Extensions.DependencyInjection/src/ServiceLookup/ServiceProviderEngineScope.cs)。

无界面测试覆盖真实 DI 释放、延迟异步服务与应用清理阶段的异常恢复，不代替原生关闭、弹窗和托盘验收；生成项目构建只证明接线可编译。
