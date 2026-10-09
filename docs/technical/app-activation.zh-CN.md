# 应用激活与单实例

[English](app-activation.md) | [简体中文](app-activation.zh-CN.md)

纯 WinUI 模板在 Program.Main 中使用 Windows App SDK AppInstance 注册项目专属的单实例键。第二次启动在创建 App、服务容器或窗口前，把 Windows 激活参数转交给原进程，然后退出。原进程恢复最小化窗口，显示并激活已有 MainWindow，保留页面与未保存内容。

初次启动和后续激活进入同一个队列。队列在模块 InitializeAsync 完成、主窗口创建后才开始处理；请求按接收顺序执行，不会重叠。处理器在 UI 线程调用，await 默认继续回到 UI 线程。单个请求失败会写入日志，后续请求继续处理；框架不记录原始启动参数。

## 注册业务处理器

在 MainPanel 等前端工程实现 UIComponent.Activation.IAppActivationHandler：

~~~csharp
public sealed class BusinessActivationHandler : IAppActivationHandler {
    public async Task HandleAsync(AppActivationArguments arguments,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        if (arguments.Kind == ExtendedActivationKind.Launch &&
            arguments.Data is Windows.ApplicationModel.Activation.ILaunchActivatedEventArgs launch) {
            // launch.Arguments 是 Windows 提供的原始命令行，请使用业务自己的解析器。
            // 在这里调用业务服务；切换页面使用 await NavigateAsync(...)，保留离开拦截。
        }
        await Task.CompletedTask;
    }
}
~~~

在 UI 工程的 IXamlNexusModule.ConfigureServices 中注册：

~~~csharp
services.AddSingleton<IAppActivationHandler, BusinessActivationHandler>();
~~~

多个处理器按 DI 注册顺序执行。处理器需要遵守取消令牌；应用关闭时取消正在处理的请求，并丢弃尚未处理的请求。处理器使用异步 API，避免阻塞 UI 线程。框架只负责转交激活参数，不自动把参数解释成导航目标。

## 边界与验证

- 此接入适用于新生成的纯 WinUI basic / standard 项目，包含安装或移除托盘 Recipe 后的项目。托盘隐藏的窗口也复用同一个 MainWindow。
- 接口保留完整 AppActivationArguments，可处理 Launch、Protocol、File 等激活种类；文件关联与协议注册需要业务自行配置，当前模板没有自动注册。
- 混合架构继续由 WPF 后台宿主、UIRunnerService 和命名管道管理前端进程。此变更不把前端改成独立的应用实例；混合架构的参数转交需要走宿主链路。
- 生成入口使用 DISABLE_XAML_GENERATED_MAIN，保留 STA 和 DispatcherQueueSynchronizationContext。转交期间通过 COM 等待泵保持 STA 可响应，避免直接同步等待 SDK 转交造成死锁。
- 本次没有为已有生成项目自动替换 App.xaml.cs。项目升级须审阅 scaffold 差异，并合并自己的启动逻辑。

队列测试覆盖初始化前激活、串行处理、失败恢复、关闭取消及调度拒绝。生成项目构建验证入口与 SDK 类型是否可编译；窗口恢复和跨进程参数转交须在 Windows 上单独运行验证。

参考：[Microsoft 单实例接入说明](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/applifecycle/applifecycle-single-instance)。

## 无界面验收

~~~powershell
pwsh -NoProfile -File eng/Test-AppInstance.ps1 -Repetitions 2
~~~

脚本构建独立的控制台探针，只使用 AppInstance 和 COM 等待泵，不创建 WinUI 窗口，也不模拟鼠标键盘。默认读取纯 WinUI 模板的 Microsoft.WindowsAppSDK 版本。在同一个父进程、相同用户、相同提权状态和工作目录下，分别测试相同路径、混合大小写与全小写、混合大小写与全大写的双向启动。每个请求验证原实例 PID 和包含空格的参数转交，并比较实际加载的 SDK 二进制版本和 SHA-256。

结果保存在 .artifacts/app-instance-*/Results.json；检测到身份分离或转交失败时返回非零退出码。所有探针进程由脚本管理并在结束时清理。此验收验证实例识别与参数转交，窗口恢复、前台焦点及托盘行为仍须单独验证。

2026-10-09，在当前 SDK 1.8.260317003 上的受控实验结果：相同路径 2/2 成功，改变路径大小写的 8/8 次均出现身份分离，所有对照进程的用户、提权状态、工作目录及 SDK 二进制哈希相同。SDK DLL 与此前生成 UI 项目中的 DLL 哈希也相同。当前单实例接入尚未通过路径大小写验收；不能仅凭 SDK 主干已有规范化代码，就认为当前发布包已包含相关修复。
