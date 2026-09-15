# 系统托盘

[English](tray.README.md) | [简体中文](tray.README.zh-CN.md)

此组件为纯 WinUI 项目添加 `{{ProjectName}}.UI/Modules/SystemTrayModule.cs` 和 H.NotifyIcon.WinUI，启动时自动注册，无需修改 App.xaml.cs。

托盘图标支持显示或隐藏窗口，提供 WinUI Fluent 菜单和退出命令。关闭窗口时可选择隐藏到托盘、退出应用或取消；记住选择后，会在普通用户设置 JSON 中保存 `WindowCloseBehavior`：

- `Ask`：每次关闭时询问，默认值
- `HideToTray`：隐藏窗口，应用继续运行
- `Exit`：退出应用

安装 settings 后，设置面板也可以修改该选项，两种安装顺序均支持。未安装面板时，关闭提示框仍可使用并记住选择。托盘菜单的退出命令始终退出应用。

添加或移除组件前先停止应用，完成后重新构建运行。移除命令：`xamlnexus remove tray`；受管理文件若已修改，会受到事务检查保护。hybrid 已内置自己的托盘能力，此 Recipe 面向纯 WinUI。
