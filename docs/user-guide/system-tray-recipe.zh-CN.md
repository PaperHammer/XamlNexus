# 系统托盘 Recipe

[English](system-tray-recipe.md) | [简体中文](system-tray-recipe.zh-CN.md)

向生成的纯 WinUI 项目安装可选桌面集成：

```powershell
xamlnexus add tray --project <project-directory>
```

Recipe 向 UI 工程添加 `Modules/SystemTrayModule.cs`，引用兼容 .NET 8 的 H.NotifyIcon.WinUI 2.3.2。启动时创建通知区域图标，提供：

- 左键显示／隐藏窗口；
- WinUI Fluent 风格的显示／隐藏及退出菜单；
- 关闭窗口时选择“关闭窗口”（隐藏到托盘）、“退出应用”或“取消”；
- 应用退出时确定性释放。

默认每次关闭都会询问。“记住我的选择”将选项保存到常规用户设置 JSON 的
`WindowCloseBehavior` 字段：`"Ask"`、`"HideToTray"` 或 `"Exit"`。
托盘菜单中的“退出应用”始终退出。启动时不再自动弹出气泡，仍可通过
`ShowNotification` 主动发送通知。

安装 `settings` 后，常规设置页会显示“关闭主窗口时”选项，无论先安装设置还是
先安装托盘。移除托盘并重新构建运行后，该项隐藏。没有设置页时，关闭对话框
仍可使用并记住选择。

修改组件后请停止应用、重新构建并启动。托盘使用 H.NotifyIcon 的 `SecondWindow`
模式，在主窗口隐藏时也能显示 WinUI 菜单；该模式在上游库中仍标记为预览。

退出时先隐藏主窗口，再清理窗口和服务，避免窗口在清理期间仍停留在屏幕上。
取消关闭对话框时窗口保持显示。

XamlNexus 仍使用尚未发布的 1.0.4，相关 Recipe 保持 1.0.0。较早开发构建生成的
项目可能缺少关闭设置契约：临时测试项目可用最新构建重新生成；已有业务代码的项目
应保留用户修改并手动迁移模板变更。同版本的 `upgrade` 或 `update` 不用于刷新这些
开发阶段的旧项目。

生成的 SystemTrayService 注册为单例。通过应用服务提供器创建的对象可构造注入该服务并发送通知，无需直接依赖托盘控件：

```csharp
public sealed class SyncJob(SystemTrayService tray) {
    public void Complete() =>
        tray.ShowNotification("Sync complete", "All local files are up to date.");
}
```

目前仅支持 winui。混合架构已有 WPF 宿主托盘，安装第二个托盘所有者会被拒绝。

`xamlnexus remove tray` 在文件未被修改时删除生成模块。与其他确保存在的包引用一样，NuGet 引用会保留，因为项目或其他组件可能仍在使用。
