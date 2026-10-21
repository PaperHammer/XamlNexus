# XamlNexus 文档

[English](README.md) | [简体中文](README.zh-CN.md)

XamlNexus 用于生成可运行、可自由修改的 Windows 客户端原型。你可以从快速开始入手，也可以按下面的分类查找技术说明和使用方法。

本文档对应 1.0.5 源码。使用 `xamlnexus --version` 核对已安装工具；更新 CLI 不会自动更新已生成应用。组件使用 `update <id>` 更新，脚手架变更先通过 `upgrade --dry-run` 审阅。

| 分类 | 内容 | 从这里开始 |
|---|---|---|
| 项目介绍 | 定位、预设、能力边界与仓库结构 | [项目介绍](introduction/README.zh-CN.md) |
| 技术说明 | 整体架构、模块、导航、Recipe 和项目清单 | [技术说明](technical/README.zh-CN.md) |
| 用户手册 | 安装、创建、运行、页面、组件与升级排错 | [用户手册](user-guide/README.zh-CN.md) |

## 推荐阅读顺序

- 第一次使用：[快速开始](user-guide/quickstart.zh-CN.md) → [页面开发](user-guide/business-page.zh-CN.md) → 按需添加组件。
- 理解项目设计：[产品模型](introduction/product-model.zh-CN.md) → [整体架构](technical/architecture.zh-CN.md)。
- 开发自定义能力：[模块生命周期](technical/module-lifecycle.zh-CN.md) → [Recipe 契约](technical/recipe-contract.zh-CN.md) → [变更预览](user-guide/change-plans.zh-CN.md)。
- 维护已生成项目：[诊断](user-guide/doctor.zh-CN.md) → [项目升级](user-guide/project-upgrade.zh-CN.md)。
- 业务导航与退出：[应用激活](technical/app-activation.zh-CN.md) → [导航保护与回滚](technical/navigation.zh-CN.md) → [混合宿主关闭](technical/hybrid-close.zh-CN.md)。
- 完整业务示例：[BusinessWorkflow](../samples/XamlNexus.BusinessWorkflow/README.md)，包含 SQLite 持久化与升级的无界面验收。
