# 应用更新

[English](app-update.README.md) | [简体中文](app-update.README.zh-CN.md)

app-update Recipe 会自动注册更新器，并启用设置中的更新入口。
请在 `{{ProjectName}}.Common/Consts.cs` 中，将 `Consts.Updates.ManifestUrl` 配置为你的 HTTPS 更新清单地址。
安装包和 SHA-256 文件地址也必须使用 HTTPS。

安装此模块不会发布更新源，也不会配置签名。
此安装器流程用于非打包 WinUI 应用；MSIX 应用使用其自身的分发渠道。
可通过 `xamlnexus remove updater` 移除未被修改的模块文件。
已有内置更新器的项目需要先完成迁移，再添加此 Recipe。
