# 混合宿主的关闭保护

[English](hybrid-close.md) | [简体中文](hybrid-close.zh-CN.md)

UIRunnerService 的 CloseUIAsync、RestartUIAsync 和宿主 App.RequestShutDownAsync 使用同一套受保护关闭流程。普通退出、托盘退出、后台窗口关闭及 gRPC 关闭/重启请求不再以 500 毫秒或 3.5 秒的等待超时作为强杀 UI 的依据。

宿主通过自己创建的 UI 子进程 stdin 发送带唯一 ID 的关闭请求。UI 在模块初始化和主窗口创建后读取请求，并在 DispatcherQueue 上调用 ArcWindow.RequestCloseAsync。启动期间请求保留在管道内；侧栏、返回和关闭仍经过同一套未保存保护。允许关闭后，宿主等待真实进程退出；取消、重复离开请求或保存失败时，UI 通过 stdout 返回对应请求 ID，宿主取消本次操作。

stdout 中的普通日志不作为控制消息。请求 ID 与进程 ID 同时匹配，过期响应不能影响新请求。此通道仅属于后台宿主创建的子进程，原有 gRPC 服务和消息枚举不变。

宿主退出必须先等待 UI 实际退出，再停止激活与 RPC、在服务释放前保存设置、关闭 Job、等待 DI 异步释放并移除通知。清理异常逐阶段记录，后续阶段仍会执行；重复退出请求等待清理结束。详见[应用资源释放](module-lifecycle.zh-CN.md#应用退出与资源释放)。确认期间仍保留托盘和后台服务，并禁止创建新的 UI 进程；取消后恢复正常运行。重启也先等待 UI 退出，取消后不启动替代进程。UI 退出会取消自己的 RPC，宿主已接受的关闭操作不随该取消中断。

正在保存或等待用户操作时，宿主继续等待。无响应时不会自动强杀；调用方可以通过 CancellationToken 取消等待，取消等待不代表允许销毁 UI。宿主崩溃、操作系统强制终止及显式资源销毁仍可能终止 Job 中的进程，业务应另行设计恢复机制。

## 无界面验收

~~~powershell
dotnet run --project eng/diagnostics/HybridCloseProbe/HybridCloseProbe.csproj -c Release
~~~

探针直接编译实际 UIHostControl 和 UICloseCoordinator 源码，创建自己管理的控制台子进程，验证取消后重试、保存耗时超过原 3.5 秒阈值、保存失败、无响应时取消等待。测试清理只终止探针自身的子进程。单元测试还覆盖重复请求、过期响应和普通日志。

这些验收证明进程协议和协调逻辑；实际 WinUI 对话框、窗口焦点和托盘显示需另行验证。生成项目构建验证宿主与 UI 的接入是否编译通过。

业务激活与关闭共享 UI 控制队列。导航期间收到的关闭请求先排队，等导航完成后处理；收到新激活回执不会绕过离开拦截。详见[混合宿主转交契约](app-activation.zh-CN.md#混合宿主转交契约)。
