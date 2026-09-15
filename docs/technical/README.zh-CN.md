# 技术说明

[English](README.md) | [简体中文](README.zh-CN.md)

本目录介绍脚手架实现和生成应用的扩展方式。命令用法见[用户手册](../user-guide/README.zh-CN.md)。

| 文档 | 解决的问题 |
|---|---|
| [整体架构与关键流程](architecture.zh-CN.md) | CLI 如何生成项目，两种进程架构如何运行，自动变更如何保护用户代码 |
| [模块生命周期](module-lifecycle.zh-CN.md) | 服务注册、初始化顺序、页面依赖注入和资源所有权 |
| [统一导航接口](navigation.zh-CN.md) | 页面注册、标准窗口和自定义导航接入 |
| [Recipe 契约](recipe-contract.zh-CN.md) | 组件描述、文件计划、项目操作、更新与移除 |
| [项目清单](project-manifest.zh-CN.md) | 模块来源、文件哈希、生成基线与兼容边界 |

修改核心机制后运行 `dotnet test src/XamlNexus.TemplateTests/XamlNexus.TemplateTests.csproj`。
涉及生成项目的依赖、模板或构建配置时，再运行[生成项目构建验收脚本](../../eng/Test-GeneratedProjects.ps1)。自动化测试检查代码与生成结构；WinUI 构建检查编译结果；GUI 和安装升级需要单独运行验证。
