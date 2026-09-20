# XamlNexus

[![NuGet](https://img.shields.io/nuget/v/XamlNexus)](https://www.nuget.org/packages/XamlNexus)
[![Stars](https://img.shields.io/github/stars/PaperHammer/XamlNexus)](https://github.com/PaperHammer/XamlNexus/stargazers)
[![Issues](https://img.shields.io/github/issues/PaperHammer/XamlNexus)](https://github.com/PaperHammer/XamlNexus/issues)

[English](README.md) | **简体中文**

基于 .NET 的 Windows 桌面原型脚手架。通过命令行创建 WinUI 3 项目、添加页面和组件，快速开始应用开发。

[XamlNexus Gallery](samples/XamlNexus.Gallery/README.md) 是独立的示例应用，提供功能体验、使用提示和真实源码模板的离线浏览；默认生成的用户项目不包含 Gallery。

正式工具包通过 `xamlnexus gallery` 下载、校验并启动匹配版本。执行 `dotnet tool update -g XamlNexus` 后，下次启动自动获取对应新版；缓存完成后可离线使用。此命令随包含该功能的新包发布后可用。

## 功能

- **两种架构**：纯 WinUI 3 应用，或 WinUI 3 前端搭配 WPF 后台宿主。
- **应用基础设施**：集成 ArcXaml、导航、MVVM、依赖注入、配置和日志。
- **可选组件**：设置、SQLite、系统托盘、应用更新和 EditorConfig。
- **项目维护**：预览变更、校验项目、诊断配置和升级脚手架基础代码。

## 安装

准备 Windows、.NET 8 SDK 和 WinUI 构建环境。

```powershell
dotnet tool install --global XamlNexus
xamlnexus --version
xamlnexus doctor --environment
```

更新或卸载工具：

```powershell
dotnet tool update --global XamlNexus
dotnet tool uninstall --global XamlNexus
```

本文对应当前源码，已发布的 NuGet 包可能尚未包含所有改动。

## 快速开始

```powershell
xamlnexus new MyApp
cd MyApp
xamlnexus run
```

修改代码或再次运行前，按 **Ctrl+C** 停止当前运行。首页布局位于 `MyApp.MainPanel/MainPage.xaml`，页面状态和逻辑位于 `MyApp.MainPanel/ViewModels/MainViewModel.cs`。

添加页面并运行：

```powershell
xamlnexus page add Workspace --dry-run
xamlnexus page add Workspace
xamlnexus page add Projects --kind list
xamlnexus run
```

停止应用后，按需添加组件：

```powershell
xamlnexus recipes
xamlnexus add sqlite
xamlnexus run
```

`run` 自动选择启动程序，以 Debug/x64/非打包模式构建并运行，不提供文件监听或热重载。使用 `run --dry-run` 查看执行命令，使用 `run --no-build` 启动已有构建产物。

## 项目选项

直接执行 `xamlnexus` 进入交互式创建，也可以指定参数：

```powershell
xamlnexus new MinimalApp --profile basic
xamlnexus new DataApp --profile basic --features settings,sqlite
xamlnexus new HybridApp --preset hybrid
```

| 参数 | 可选值 |
|---|---|
| `--preset` | `winui`（默认）或 `hybrid` |
| `--profile` | `standard`（默认，包含设置面板）或 `basic`（省去完整设置面板） |
| `--solution-format` | `sln`（默认）或 `slnx`（需要 SDK 9.0.200+） |

两种配置均保留主题、多语言、配置和日志基础设施。

| 组件 | 用途与适用范围 |
|---|---|
| `settings` | 设置面板；standard 已包含，basic 可选装 |
| `sqlite` | 本地数据存储；hybrid 的数据访问由宿主负责 |
| `tray` | 系统托盘；WinUI 可选装，hybrid 已内置 |
| `updater` | 应用更新；WinUI 可选装且依赖 settings，hybrid 已内置；需要配置更新源 |
| `editorconfig` | 编辑器格式配置 |

使用 `xamlnexus add settings,sqlite` 一次安装多个组件，已安装的组件会跳过。具体配置请阅读生成项目中各组件的 README，文档提供英文和简体中文两个版本。

## 项目维护

在项目目录执行以下命令，或使用 `--project <directory>` 指定项目：

```powershell
xamlnexus list
xamlnexus validate
xamlnexus doctor
xamlnexus update sqlite --dry-run
xamlnexus upgrade --dry-run
xamlnexus --help
```

- `update <id>` 更新已安装组件，`upgrade` 升级脚手架基础代码；更新 CLI 不会自动更新已有项目。
- 生成的文件可以自行修改。组件更新和移除会检查受管理文件，脚手架升级会报告合并冲突。
- `--dry-run` 用于预览变更；`--json` 只改变输出格式，不会阻止文件写入。
- 安装包、签名和在线更新分发，需要单独配置生成项目中的发布文件。

如需在当前 PowerShell 会话中使用简短命令，可设置别名：

```powershell
Set-Alias -Name xn -Value xamlnexus
xn --help
```

## 文档

- [文档目录](docs/README.zh-CN.md)
- [快速开始](docs/user-guide/quickstart.zh-CN.md)
- [命令详解](docs/user-guide/commands.zh-CN.md)
- [整体架构与关键流程](docs/technical/architecture.zh-CN.md)
- [配置与能力边界](docs/introduction/product-model.zh-CN.md)
- [页面与 ViewModel](docs/user-guide/business-page.zh-CN.md)
- [SQLite](docs/user-guide/sqlite-recipe.zh-CN.md)
- [设置与多语言](docs/user-guide/localization.zh-CN.md)
- [脚手架升级](docs/user-guide/project-upgrade.zh-CN.md)
- [发布流程（英文）](.github/RELEASING.md)

## 从源码构建

```powershell
dotnet build ./src/XamlNexus/XamlNexus.csproj
dotnet ./src/XamlNexus/bin/Debug/net8.0/XamlNexus.dll --help
```

使用 PowerShell 7 和 Windows/WinUI 构建环境，执行生成项目验收：

```powershell
./eng/Test-GeneratedProjects.ps1
./eng/Test-GeneratedProjects.ps1 -Profile standard
```

每次执行都会检查两种架构在添加页面、组件前后的构建结果，不启动界面。日志保存在 `.artifacts/generated-build-*/`，生成项目保留在脚本输出的临时目录中。
