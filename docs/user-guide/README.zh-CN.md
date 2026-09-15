# 用户手册

[English](README.md) | [简体中文](README.zh-CN.md)

## 首次开发

1. [快速开始](quickstart.zh-CN.md)：安装 → 创建 → 运行 → 修改首页 → 添加页面 → 添加组件。
2. [页面开发](business-page.zh-CN.md)：普通 Page、ViewModel、依赖注入与页面生命周期。
3. [设置](settings.zh-CN.md)与[多语言](localization.zh-CN.md)：配置持久化、恢复、主题与运行时语言切换。

## 按需添加能力

| 能力 | 文档 |
|---|---|
| SQLite 与数据访问 | [SQLite](sqlite-recipe.zh-CN.md) |
| 托盘、菜单和通知 | [系统托盘](system-tray-recipe.zh-CN.md) |
| 更新检查和安装流程 | [在线更新](app-update-recipe.zh-CN.md) |

使用 `xamlnexus recipes` 查看当前工具提供的组件，使用 `xamlnexus list` 查看项目已安装的模块。`settings` 的添加方式见快速开始；`editorconfig` 用于添加编辑器配置。

## 常用命令

完整参数、默认值、适用范围和退出码见[命令详解](commands.zh-CN.md)。

| 任务 | 示例 |
|---|---|
| 创建默认项目 | `xamlnexus new MyApp` |
| 开发运行 | `xamlnexus run` |
| 添加普通页面 | `xamlnexus page add Orders` |
| 添加能力 | `xamlnexus add sqlite` |
| 预览组件移除 | `xamlnexus remove sqlite --dry-run` |
| 预览组件更新 | `xamlnexus update sqlite --dry-run` |
| 预览脚手架升级 | `xamlnexus upgrade --dry-run` |
| 检查项目 | `xamlnexus validate` |
| 诊断环境与集成 | `xamlnexus doctor` |
| 查看帮助 | `xamlnexus --help` |

项目命令在生成项目目录执行，或通过 `--project` 指定路径。`run` 当前使用 Debug／x64／非打包模式；不提供 watch 或热重载。

## 维护与交付

- [PR 流程与校验开关](pr-checks.zh-CN.md)：自动检查的触发条件、合并限制，以及开启、关闭和调整方式。

- [变更预览和 JSON 输出](change-plans.zh-CN.md)：先检查自动修改计划，再应用变更。
- [项目升级](project-upgrade.zh-CN.md)：区分组件 `update` 与整体脚手架 `upgrade`，处理本地定制和合并冲突。
- [环境与项目诊断](doctor.zh-CN.md)：定位 SDK、工程引用和能力接入问题。
- [生成项目发布说明](../../src/Templates/Shared/RELEASING.md)：安装器、签名和更新资产的准备。实际使用时以生成项目自己的 `RELEASING.md` 与 `eng/publishing/release.json` 为准。

生成的源码和配置都可以修改。内容差异会产生提示；自动移除或升级可能覆盖用户修改时，会报告冲突并停止写入。详情见[产品模型](../introduction/product-model.zh-CN.md)。
