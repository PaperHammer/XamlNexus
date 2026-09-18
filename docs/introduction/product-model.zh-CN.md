# 产品模型与能力边界

[English](product-model.md) | [简体中文](product-model.zh-CN.md)

XamlNexus 默认生成可以直接运行和修改页面的 Windows 标准客户端。创建时可以选择基础版，也可以按需添加组件。

## 创建入口

- `--preset winui|hybrid`：选择架构，默认纯 WinUI；hybrid 为 WinUI 前端与 WPF 后台宿主。
- `--profile standard|basic`：选择起始能力组合，默认 standard。
- `--features settings,sqlite`：创建时组合组件，复用 Recipe 依赖与安装机制。
- 不带参数运行 `xamlnexus`：交互选择架构、预设和组件。
- `--solution-format sln|slnx`：选择解决方案格式，默认 sln；SLNX 需要当前 SDK 9.0.200+。

基础版省去完整设置界面，保留启动、导航、MVVM、DI、配置、日志、主题与多语言基础机制。
标准版在此基础上包含 settings。两种预设复用现有模板，不维护两套完整副本。
混合架构保留原有托盘、更新和后台宿主能力，基础版并不表示这些能力已全部可选化。

## 能力清单

| 能力 | 基础版 | 标准版（默认） | 当前约束 |
|---|---|---|---|
| 启动、窗口、导航、单实例 | 自带 | 自带 | 不按单个机制拆 Recipe |
| MVVM、DI、模块生命周期 | 自带 | 自带 | 页面持有 ViewModel，共享服务归应用容器 |
| 配置、日志、异常处理 | 自带 | 自带 | 保留已有恢复与错误处理机制 |
| 主题、多语言基础设施 | 自带 | 自带 | settings 提供配置界面 |
| 完整设置界面 | 按需 | 自带 | `add settings` |
| SQLite | 按需 | 按需 | 两种架构均支持；混合前端通过 RPC 访问后台数据 |
| 托盘、菜单、通知 | 按架构 | 按架构 | 纯 WinUI `add tray`；混合架构内置 |
| 在线更新 | 按架构 | 按架构 | 纯 WinUI `add updater`，依赖 settings；混合架构内置 |
| 登录自启动 | 保留基础实现 | 设置界面可配置 | 无独立 Recipe，不默认开启系统注册 |
| 编辑器配置 | 按需 | 按需 | `add editorconfig` |
| 发布、安装器、签名 | 保留脚本 | 保留脚本 | 需维护者自行配置和执行，不属于开发运行 |
| 普通页面生成 | `page add` | `page add` | 页面、ViewModel、导航注册；支持 blank/list，无表单类型 |

实际安装状态以 `xamlnexus list` 和 `xamlnexus.json` 中的 modules 为准。
profile 记录创建意图，后续移除组件不会因初始预设被自动装回。
旧清单没有 profile 时按 standard 兼容；preset 的原有架构含义不变。

## 首次开发流程

按[快速开始](../user-guide/quickstart.zh-CN.md)完成：

```text
new → run → 修改首页 → page add → 按需 add → run
```

`run` 负责 Debug/x64/非打包构建与启动，支持 Ctrl+C、退出码及同项目重复运行保护。
当前不提供 watch、热重载或 MSIX 运行。
业务页面不会登记为可删除的 Recipe。两种前端共用[导航注册接口](../technical/navigation.zh-CN.md)和页面工厂，
无需为了添加普通页面改写 App.xaml.cs 或 MainWindow。
页面开发与服务注入见[页面说明](../user-guide/business-page.zh-CN.md)、[模块生命周期](../technical/module-lifecycle.zh-CN.md)。

## 组件边界

初始模板和 `add` 生成的源码、配置都交给用户自定义，包括版本号和发布脚本。
哈希与生成基线用于辅助更新、升级和移除，保护用户修改，不用于限制编辑权限。
内容差异不阻断运行、页面生成或组件添加；必要文件缺失仍需处理，实际编译错误由构建报告。
自动变更无法安全合并时应提示冲突，不能覆盖用户内容，也不要求用户重算清单哈希才能继续开发。

独立用途、依赖或配置成本较高且有清晰增减边界的能力，才适合拆成组件。
已有项目支持批量 add、依赖和冲突检查、预览、更新、移除与事务回滚，保护用户修改和业务数据。
创建时组合会复用已包含能力；已有项目显式 add 已安装组件会提示错误。

updater 仍使用设置界面的更新展示与公共接口，基础版安装时自动补齐 settings。
主题和多语言保留为基础机制，目前没有独立 Recipe 或通用设置扩展框架。
混合架构前端使用 RPC 客户端，SQLite 数据访问归后台宿主；普通页面生成不自动创建业务 RPC。

## 序列化兼容性

两套模板及 Showcase 已移除未使用的 MessagePack 引用与 MVVM 特性，JSON 存储和 gRPC/Protobuf 通信格式不变。
如果已有应用自行使用 MessagePack，需要单独评估，不能直接移除依赖。
