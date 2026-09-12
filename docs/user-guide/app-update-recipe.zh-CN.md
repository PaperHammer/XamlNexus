# 在线更新 Recipe

[English](app-update-recipe.md) | [简体中文](app-update-recipe.zh-CN.md)

当前纯 WinUI 模板默认不注册更新器，也不显示更新控件。

```powershell
xamlnexus add app-update --project <project-directory>
xamlnexus remove app-update --project <project-directory>
```

安装后加入 HTTPS 更新源、校验下载器、安装生命周期、客户端和模块注册。现有设置面板会检测可选更新服务。该 Recipe 依赖 settings：基础版会自动安装设置面板，标准版复用已有设置模块。

无需手动修改 App.xaml.cs。将 `Consts.Updates.ManifestUrl` 配置为自己的 HTTPS 更新清单；发布和签名仍需按应用配置。

移除时删除未修改的组件文件，下次构建运行后设置入口禁用。用户修改由事务检查保护。最小更新契约保留在核心模板中，展示界面保留在 settings 模块；移除 app-update 不会移除 settings。

该 Recipe 支持新建纯 WinUI 项目。混合架构和现有 Showcase 保留内置更新器。旧项目若已有更新器，需先迁移；工具拒绝覆盖已有更新文件。安装器流程面向非打包应用；MSIX 更新遵循其分发渠道。
