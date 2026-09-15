# 设置面板

[English](README.md) | [简体中文](README.zh-CN.md)

此项目提供应用设置页面，包括背景材质、语言、存储路径和诊断日志导出。standard 默认包含；basic 项目可执行 `xamlnexus add settings` 添加。

脚手架自动注册设置模块和导航，项目归入解决方案的 Panels 文件夹。新增业务设置时可扩展本项目的 Views 和 ViewModels；持久化仍需接入用户设置模型及客户端，仅添加控件不会自动保存值。

纯 WinUI 安装 updater 后会提供更新服务和设置入口，HTTPS 更新清单地址需按照 updater README 配置。安装 tray 后会增加窗口关闭行为设置，两种安装顺序均支持。仅添加 settings 不会自动安装这些可选服务。hybrid 使用已有的宿主服务。

界面语言选择后立即生效，背景材质修改需重启 UI。存储路径迁移复制支持的应用文件并保留原文件，不迁移 SQLite 数据库。

默认设置页面无需额外配置。通过 Recipe 安装的面板可在停止应用后执行 `xamlnexus remove settings` 移除，需要先移除依赖它的 Recipe。模板自带的设置面板不能作为 Recipe 卸载。
