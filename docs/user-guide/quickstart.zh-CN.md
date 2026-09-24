# XamlNexus 快速开始

[English](quickstart.md) | [简体中文](quickstart.zh-CN.md)

本指南从创建和运行项目开始，再介绍如何修改首页、添加页面和组件。
默认生成标准客户端，包含设置界面、主题、多语言、导航、MVVM、DI、配置和日志。

## 1. 准备与安装

查找具体参数时可直接阅读[命令详解](commands.zh-CN.md)。

需要 Windows、.NET 8 SDK，以及能编译 WinUI 3 的 Visual Studio 2022 或 Build Tools 环境。

```powershell
dotnet --info
dotnet tool install --global XamlNexus
xamlnexus --version
xamlnexus --help
```

本文对应当前源码。如果已安装版本缺少文中的命令，先运行 `dotnet tool update --global XamlNexus` 更新工具。尚未发布的功能需要从源码构建。

从仓库根目录执行 `dotnet build src/XamlNexus/XamlNexus.csproj` 后，可在 PowerShell 中定义入口；
函数使用绝对路径，后续切换到生成项目目录仍可调用：

```powershell
$XamlNexusCli = (Resolve-Path ./src/XamlNexus/bin/Debug/net8.0/XamlNexus.dll).Path
function xamlnexus { & dotnet $XamlNexusCli @args }
```

### 使用简写 `xn`

在 PowerShell 中设置别名后，文中的 `xamlnexus` 都可以写成 `xn`：

```powershell
Set-Alias -Name xn -Value xamlnexus
xn --help
xn new MyApp
```

此设置只对当前 PowerShell 会话有效。希望新终端也能使用时，将 `Set-Alias -Name xn -Value xamlnexus` 加入自己的 PowerShell 配置文件 `$PROFILE`。它也适用于上面的源码入口函数。

`xn` 是终端别名；工具包名仍是 `XamlNexus`，安装、更新和卸载命令不变。

## 2. 创建并运行

在准备放置应用的目录执行：

```powershell
xamlnexus new MyApp
cd MyApp
xamlnexus run
```

默认是纯 WinUI 的标准版。也可以直接运行 `xamlnexus`，交互选择架构、预设和组件。

`run` 自动定位启动工程，构建并启动 **Debug / x64 / 非打包**应用。
混合架构会启动 WPF 宿主，由宿主启动 WinUI 前端。
先按 Ctrl+C 停止本次运行，再执行后续修改和命令；重新执行 `xamlnexus run` 会重新构建。
它目前不提供 watch、热重载、Release 或 MSIX 运行模式。

如果构建失败，先查看构建输出，再运行 `xamlnexus doctor` 检查环境与项目。
SDK 检查以项目目录中 `dotnet --version` 的实际结果为准，遵循项目或祖先目录的 `global.json`；
选定版本未安装或低于 .NET 8 时会报告错误，而不会因机器上另有新版 SDK 就判定通过。
可用 `xamlnexus run --dry-run` 查看构建计划；`--no-build` 只运行已有产物，不会编译新修改。

父进程退出后，CLI 最多等待 2 秒排空输出；子进程继续持有输出管道不会阻止命令结束。
创建项目时若目标名称已被占用，会原子预留带后缀的新目录；以完成提示中的实际路径为准。

## 3. 修改首页

| 要修改的内容 | MyApp 中的位置 |
|---|---|
| 首页布局 | `MyApp.MainPanel/MainPage.xaml` |
| 首页状态与交互逻辑 | `MyApp.MainPanel/ViewModels/MainViewModel.cs` |
| 页面初始化、控件事件 | `MyApp.MainPanel/MainPage.xaml.cs` |
| 窗口布局 | `MyApp.UI/MainWindow.xaml` |
| 内置导航 | `MyApp.UI/Navigation/BuiltInNavigation.cs` |
| 组件服务注册 | `MyApp.UI/Modules/`；混合后台模块在 `MyApp/Modules/` |

在首页的 `<!-- Add page content here. -->` 处加入：

```xml
<TextBlock Text="我的第一个原型" />
```

再次执行 `xamlnexus run` 查看变化。首页 ViewModel 由工厂创建，可通过构造函数注入模块注册的服务；
不需要修改应用启动流程。主题和语言设置可在标准版设置页中切换。

## 4. 添加页面

停止运行后，在 MyApp 目录执行：

```powershell
xamlnexus page add Workspace --dry-run
xamlnexus page add Workspace
xamlnexus page add OrderDetails --kind details
xamlnexus page add OrderEditor --kind form
xamlnexus run
```

当前模板会生成以下文件，并自动加入导航：

- `MyApp.MainPanel/WorkspacePage.xaml`
- `MyApp.MainPanel/WorkspacePage.xaml.cs`
- `MyApp.MainPanel/ViewModels/WorkspaceViewModel.cs`
- `MyApp.UI/Navigation/WorkspaceNavigation.cs`

默认生成普通页面骨架；`--kind list`、`details`、`form` 会生成带可替换数据源接口的可运行业务起点。直接编辑生成的页面、ViewModel 和服务即可。
自定义窗口需消费统一导航注册表；`--no-navigation` 会跳过导航注册。
详见 [页面开发](business-page.zh-CN.md)和[导航接口](../technical/navigation.zh-CN.md)。

## 5. 按需添加能力

停止运行后，先查看可用能力，再选择安装：

```powershell
xamlnexus recipes
xamlnexus add sqlite --dry-run
xamlnexus add sqlite
xamlnexus run
```

SQLite 会增加 Data 工程及初始化逻辑，页面的数据操作由你实现。
混合架构的数据访问归后台宿主，前端通过 RPC 访问；Recipe 会生成 AppState RPC 示例，但不会生成你的自定义业务协议。

| 能力 | 命令 | 边界 |
|---|---|---|
| 设置界面 | `xamlnexus add settings` | 用于基础版；标准版已包含 |
| SQLite | `xamlnexus add sqlite` | 两种架构均支持 |
| 托盘与通知 | `xamlnexus add tray` | 纯 WinUI 可选；混合架构已内置 |
| 在线更新 | `xamlnexus add updater` | 纯 WinUI 可选，自动补齐 settings；需配置自己的更新源。混合架构已内置 |
| 编辑器配置 | `xamlnexus add editorconfig` | 两种架构均支持 |

已有项目可批量安装，例如基础版执行 `xamlnexus add settings,sqlite`。
依赖会自动处理，已安装的组件不需要重复 add。
详见 [SQLite](sqlite-recipe.zh-CN.md)、[托盘](system-tray-recipe.zh-CN.md)、[在线更新](app-update-recipe.zh-CN.md)。

## 6. 选择其他起点

以下是独立创建示例，不是在 MyApp 上切换预设：

```powershell
# 基础版：保留核心机制，省去完整设置界面
xamlnexus new MinimalApp --profile basic

# 创建时组合组件
xamlnexus new DataApp --profile basic --features settings,sqlite

# 混合架构：WinUI 前端 + WPF 后台宿主
xamlnexus new HybridApp --preset hybrid --profile standard
```

`--preset winui|hybrid` 选择架构；`--profile standard|basic` 选择起始能力组合。
基础版仍保留主题、多语言、配置、日志等基础设施，也保留混合架构原有后台能力。
预设不限制后续添加或移除组件。完整边界见[产品模型](../introduction/product-model.zh-CN.md)。

## SLNX 与脚手架语言资源

```powershell
xamlnexus new XmlApp --solution-format slnx
```

默认仍为 `sln`，交互创建也可选择格式。SLNX 要求当前选中的 .NET SDK 至少为 9.0.200，
生成应用的目标框架仍为 .NET 8。参见[微软 SLNX 说明](https://devblogs.microsoft.com/dotnet/introducing-slnx-support-dotnet-cli/)。
组件 add/remove、清单校验与升级支持 SLNX；升级保留原格式，不提供已有项目的自动格式转换。
仅手动转换解决方案文件不会同步 xamlnexus.json 与升级基线。

脚手架现有 i18n 和交互向导文案位于 `src/XamlNexus.Common/Resources/Strings.resx`（英文默认）
与 `Strings.zh-CN.resx`，使用 ResourceManager 加载。新增翻译时保持两份资源键一致；
`GetText` 返回原始文案，`GetI18n` 额外转义 Spectre 标记。生成客户端原有 `.resw` 资源保持不变。
CLI 诊断消息和帮助中的英文常量尚未全部本地化。

## 7. 后续维护与交付

在生成项目内执行：

```powershell
xamlnexus list
xamlnexus status
xamlnexus validate
xamlnexus update sqlite --dry-run
xamlnexus update --all --dry-run
xamlnexus upgrade --dry-run
```

从项目外调用时加 `--project <项目目录>`，例如 `xamlnexus run --project .\MyApp`。
`update sqlite` 用于已安装 SQLite 的项目；升级行为见[项目升级](project-upgrade.zh-CN.md)。

开发运行与发布分开。需要分发时使用生成项目中的发布工作流和脚本，并配置自己的版本、签名与更新源。
[XamlNexus.Gallery](../../samples/XamlNexus.Gallery/README.md)提供组合示例和分发目录构建示例。
