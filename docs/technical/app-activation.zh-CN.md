# 应用激活与单实例

[English](app-activation.md) | [简体中文](app-activation.zh-CN.md)

纯 WinUI 模板在 Program.Main 中使用 Windows App SDK AppInstance 注册项目专属的单实例键。第二次启动在创建 App、服务容器或窗口前，把 Windows 激活参数转交给原进程，然后退出。原进程恢复最小化窗口，显示并激活已有 MainWindow，普通启动不改变页面；带业务路由的启动在离开拦截通过后才切换页面。

初次启动和后续激活进入同一个队列。队列在模块 InitializeAsync 完成、主窗口创建后才开始处理；请求按接收顺序执行，不会重叠。处理器在 UI 线程调用，await 默认继续回到 UI 线程。单个请求失败会写入日志，后续请求继续处理；框架不记录原始启动参数。

## 激活参数与业务导航

使用 PowerShell 7 启动已编译的应用：

~~~powershell
# 假设通过 page add Orders --kind form 注册了 Orders 路由
& .\MyApp.UI.exe --route Orders --data '{"id":"42"}'
# 混合架构首次及二次启动均使用后台宿主
& .\MyApp.exe --route Orders --data '{"id":"42"}'
~~~

`--route` 对应 NavigationRegistry 中的路由，区分大小写。`--data` 是可选 JSON 对象，省略时使用空对象。Windows 命令行解析保留引号、空格和 Unicode；读取的是当前激活事件的 Arguments，包括二次启动转交的参数，不重复读取原进程的启动参数。其他命令行参数继续保留；`--route`、`--data` 为框架保留选项。

启动链路为 ActivationQueue → BusinessActivationRequest → NavigationRegistry.Resolve → MainWindow.NavigateActivationAsync → NaviContent.NavigateAsync。模块完成初始化后才消费请求。只允许已注册路由；未知路由、重复或缺失的选项、无效 JSON 显示错误消息，不切换当前页。取消未保存提示或保存失败时保留原页面、Payload 和侧栏选中项；成功后才更新菜单。打开同一页面的另一条记录也需要离开确认。

业务数据存入 Payload 的 `BusinessActivationRequest.PayloadKey`，以 JsonElement 保留 JSON 类型，不展开成框架键。生成的详情页、编辑页用以下方式读取记录 ID，同时兼容应用内部导航的普通 `"id"` 键：

~~~csharp
using System.Text.Json;
using MyApp.UIComponent.Activation;

string? id = payload.GetBusinessString("id");
// 自定义业务字段
if (payload is not null &&
    payload.TryGet<BusinessActivationRequest>(BusinessActivationRequest.PayloadKey, out var request)) {
    JsonElement data = request.Data;
    // 由业务验证字段并调用自己的数据源。
}
~~~

ID 使用 JSON 字符串。详情页未给 ID 时使用示例 ID，编辑页未给 ID 时创建新记录；不会沿用缓存的旧激活 ID。带新 Payload 的导航以新值优先，带显式 Payload 的应用内部导航会清除旧的外部激活标记。列表页可以通过 request.Data 自行接入查询、筛选等业务参数。

纯 WinUI 已接入首次启动和二次启动。混合架构将宿主首次启动参数通过 ProcessStartInfo.ArgumentList 一次性交给自己的 UI 子进程，并在 UI 就绪后导航；UI 重启不会重放启动路由。混合宿主的二次启动在创建服务或窗口前，通过独立命名管道转交参数并退出。已有宿主在模块初始化完成后处理请求：复用已有 UI，或在 UI 已退出时创建子进程，再通过自有 stdin/stdout 控制通道交给业务导航。普通二次启动只恢复窗口，不切换记录。

Gallery 的业务页面新增路由 / JSON 预览，复用这些模板解析与路由源码，再适配现有 SQLite 表单。Gallery 自身仍使用独立 Mutex；页内预览不代表已验证 SDK 转交或混合宿主退出。示例步骤见 [Gallery 说明](../../samples/XamlNexus.Gallery/README.md#业务流程验收--business-workflow-acceptance)。

## 混合宿主转交契约

Mutex 继续决定宿主唯一性，HybridActivationPipe 负责传输完整参数数组；不把已有 gRPC 数字命令改成携带业务 JSON。监听器在耗时模块初始化前开放，在宿主就绪后开始按接收顺序处理。UI 控制通道将激活与关闭请求串行执行；读取和入队不等待用户的未保存决定，取消导航后仍能处理下一个请求。取消、保存失败以及同页记录替换沿用 MainWindow 的离开拦截。

两个队列分别最多缓存 64 个请求，参数最多 128 项、UTF-8 JSON 总长不超过 64 KiB。宿主回执表示已进入内存队列，UI 回执表示已进入 UI 控制队列，均不等于业务导航成功。宿主连接/回执最多等 15 秒，UI 投递最多等 30 秒；不会限制用户确认或保存的时间。超时可能发生在请求已经接收之后，框架不自动重试或重放。宿主退出会停止监听并丢弃待处理请求；不提供持久化或跨退出的恰好一次保证。UI 在接收前退出时，此次投递失败；下一次新激活可以重新创建 UI。

管道使用 CurrentUserOnly，限定相同 Windows 用户与提权状态，详见 [Microsoft PipeOptions 文档](https://learn.microsoft.com/dotnet/api/system.io.pipes.pipeoptions)。不同权限、宿主退出、初始化失败、队列满或超时不会启动第二个宿主。发送失败返回非零退出码；宿主接收后遇到 UI 投递或导航错误时写日志，业务导航错误在 UI 中显示。直接启动 UI.exe 不走宿主二次激活协议；应使用 MyApp.exe。

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

多个处理器按 DI 注册顺序执行。处理器需要遵守取消令牌；应用关闭时取消正在处理的请求，并丢弃尚未处理的请求。处理器使用异步 API，避免阻塞 UI 线程。普通 Launch 以及 Protocol、File 等仍交给这些处理器。包含保留业务选项的 Launch 由内置路由处理，成功、取消及错误均不会继续调用其他处理器，避免同一个请求重复导航或绕过未保存拦截。

## 边界与验证

- 此接入适用于新生成的纯 WinUI basic / standard 项目，包含安装或移除托盘 Recipe 后的项目。托盘隐藏的窗口也复用同一个 MainWindow。
- 接口保留完整 AppActivationArguments，可处理 Launch、Protocol、File 等激活种类；文件关联与协议注册需要业务自行配置，当前模板没有自动注册。
- 混合架构继续由 WPF 后台宿主、UIRunnerService 和命名管道管理前端进程。此变更不把前端改成独立的应用实例；首次和二次启动业务参数走上述宿主链路；前端仍是宿主管理的子进程。开发启动标记 --xamlnexus-run 仍拒绝加入已运行宿主。
- 生成入口使用 DISABLE_XAML_GENERATED_MAIN，保留 STA 和 DispatcherQueueSynchronizationContext。转交期间通过 COM 等待泵保持 STA 可响应，避免直接同步等待 SDK 转交造成死锁。
- 本次没有为已有生成项目自动替换 App.xaml.cs。项目升级须审阅 scaffold 差异，并合并自己的启动逻辑。

队列测试覆盖初始化前激活、串行处理、失败恢复、关闭取消及调度拒绝。生成项目构建验证入口与 SDK 类型是否可编译；窗口恢复和跨进程参数转交须在 Windows 上单独运行验证。

参考：[Microsoft 单实例接入说明](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/applifecycle/applifecycle-single-instance)。

## 无界面验收

~~~powershell
pwsh -NoProfile -File eng/Test-AppInstance.ps1 -Repetitions 2
~~~

脚本构建独立的控制台探针，只使用 AppInstance 和 COM 等待泵，不创建 WinUI 窗口，也不模拟鼠标键盘。默认读取纯 WinUI 模板的 Microsoft.WindowsAppSDK 版本。在同一个父进程、相同用户、相同提权状态和工作目录下，分别测试相同路径、混合大小写与全小写、混合大小写与全大写的双向启动。每个请求验证原实例 PID 和包含空格的参数转交，并比较实际加载的 SDK 二进制版本和 SHA-256。

结果保存在 .artifacts/app-instance-*/Results.json；检测到身份分离或转交失败时返回非零退出码。所有探针进程由脚本管理并在结束时清理。路径矩阵还验证注册路由和含中文、空格、引号、反斜线的 JSON 参数经 SDK 转交后进入实际解析器和路由器。路由器在探针中使用无界面导航回调；模板单元测试另行调用实际 ArcNavigateAsync，验证取消、同页更新和 Payload 继承。此验收验证实例识别与参数转交，窗口恢复、前台焦点及托盘行为仍须单独验证。

2026-10-09，在当前 SDK 1.8.260317003 上的受控实验结果：相同路径 2/2 成功，改变路径大小写的 8/8 次均出现身份分离，所有对照进程的用户、提权状态、工作目录及 SDK 二进制哈希相同。SDK DLL 与此前生成 UI 项目中的 DLL 哈希也相同。这是修复前的记录；SDK 主干已有规范化代码，并不意味着当前发布包已包含相关修复。

### 路径规范化与退出等待

2026-10-12 的兼容修复保留 SDK 1.8.260317003。在首次访问 AppInstance 前，未打包应用通过文件句柄取得 EXE 的真实路径。路径写法不一致时，以该路径重新启动并保留参数、工作目录和子进程退出码；启动代理不构造 App、服务容器或窗口。使用文件系统返回的路径，而非强制转换大小写，保留大小写敏感目录的文件选择语义。MSIX 不重新启动，以保留 Windows 提供的原始打包激活数据。

激活事件在发布单实例键之前订阅，避免并发启动丢失早期请求。转交等待最多 15 秒，并监听原实例退出；失败明确返回错误，不自动启动第二个 UI。SDK 的注册/查找窗口出现原实例退出时，只有限重试注册。

验收探针直接编译 CanonicalAppLaunch、ActivationRedirector 和 ActivationQueue 的实际模板源码。默认包含路径大小写矩阵、并发启动、初始化期激活、原实例退出和首次启动业务路由。使用 -RawSdk 可跳过路径兼容层重现 SDK 原始行为；此模式在当前版本上预期返回非零。窗口恢复和 MSIX 实际运行仍需要独立验收。

### 混合转交与关闭探针

~~~powershell
dotnet run --project eng/diagnostics/HybridActivationProbe/HybridActivationProbe.csproj -c Release
dotnet run --project eng/diagnostics/HybridCloseProbe/HybridCloseProbe.csproj -c Release
~~~

HybridActivationProbe 在相同用户与提权状态下创建测试自有的二次启动进程和模拟 UI 进程，直接编译模板的 HybridActivationPipe、UIHostControl、UIActivationCoordinator、业务解析器及路由器。覆盖初始化排队、完整 JSON、取消保留模拟草稿、普通启动、8 个并发转交、UI 退出重建和宿主退出后的有界失败。单元测试补充队列满、错误帧、过期/PID 不匹配回执、UI 超时及导航等待期间的关闭串行性。探针不执行真实 WPF App、UIRunnerService、原生窗口或 ContentDialog；生成项目构建检查这些接线能否编译，实际窗口恢复、焦点、弹窗及 MSIX 运行仍需独立验收。
