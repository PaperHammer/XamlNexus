# 系统托盘 Recipe

[English](system-tray-recipe.md) | [简体中文](system-tray-recipe.zh-CN.md)

向生成的纯 WinUI 项目安装可选桌面集成：

```powershell
xamlnexus add system-tray --project <project-directory>
```

Recipe 向 UI 工程添加 `Modules/SystemTrayModule.cs`，引用兼容 .NET 8 的 H.NotifyIcon.WinUI 2.3.2。启动时创建通知区域图标，提供：

- 左键显示／隐藏窗口；
- 显示／隐藏及退出菜单；
- 初始气泡通知；
- 应用退出时确定性释放。

生成的 SystemTrayService 注册为单例。通过应用服务提供器创建的对象可构造注入该服务并发送通知，无需直接依赖托盘控件：

```csharp
public sealed class SyncJob(SystemTrayService tray) {
    public void Complete() =>
        tray.ShowNotification("Sync complete", "All local files are up to date.");
}
```

目前仅支持 winui。混合架构已有 WPF 宿主托盘，安装第二个托盘所有者会被拒绝。

`xamlnexus remove system-tray` 在文件未被修改时删除生成模块。与其他确保存在的包引用一样，NuGet 引用会保留，因为项目或其他组件可能仍在使用。
