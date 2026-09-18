# 项目介绍

[English](README.md) | [简体中文](README.zh-CN.md)

XamlNexus 是用于 Windows 客户端开发的 .NET CLI 脚手架。它生成可以直接运行和修改的项目，包含日常客户端需要的基础设施，并支持按需添加组件。

## 选择项目起点

默认生成包含设置界面的标准客户端，也提供基础版起点。按需添加的组件以源码进入用户项目，用户可直接修改页面、服务、配置和发布脚本。

两个选项彼此独立：

- `--preset winui|hybrid` 选择进程架构：纯 WinUI，或 WinUI 前端与 WPF 后台宿主。
- `--profile standard|basic` 选择初始能力组合。basic 省去完整设置界面，仍保留配置、日志、导航、主题和多语言机制。

详细能力与限制见[产品模型](product-model.zh-CN.md)。其中混合架构的托盘和更新仍为内置能力，尚未全部拆成可选组件。

## 源码归用户所有

`add` 将组件源码和所需配置加入项目。应用自行注册并运行模块，无需在运行时连接脚手架服务。Recipe 描述如何向项目添加组件，包括复制哪些文件、添加哪些依赖，以及修改哪些配置。

`xamlnexus.json` 保存来源、版本和生成基线。哈希用于保护自动更新、移除和升级中的用户修改，不是编辑许可。用户不需要重算哈希才能继续开发；不能安全自动修改时才需要手动处理冲突。

## 仓库目录

| 目录 | 用途 |
|---|---|
| `src/XamlNexus` | CLI 入口、命令解析和工具打包 |
| `src/XamlNexus.Common` | 创建、页面、运行、诊断、事务和升级机制 |
| `src/XamlNexus.Generator.*` | 两种架构的生成适配 |
| `src/XamlNexus.Recipes.BuiltIn` | 内置组件的安装定义及资源 |
| `src/Templates` | 客户端源码模板及共享发布资产 |
| `src/XamlNexus.TemplateTests` | 自动化测试 |
| `samples/XamlNexus.Gallery` | 可运行的组合示例 |
| `eng`、根目录 `.github` | 仓库构建验收与发布流程 |
| `docs` | 项目介绍、技术说明和用户手册 |

生成项目包含 `.gitignore` 和 `.github`。`.github` 仅提供 PR 还原、构建、测试工作流及简洁的 PR 模板，自动发布由用户自行配置。本地发布配置位于 `eng/publishing/release.json`。当前仓库自己的工作流仍在根目录 `.github/workflows`。

## 开始使用

按[快速开始](../user-guide/quickstart.zh-CN.md)运行第一个项目；了解实现则从[整体架构](../technical/architecture.zh-CN.md)开始。
