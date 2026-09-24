# 命令详解

[English](commands.md) | [简体中文](commands.zh-CN.md)

本文对应当前源码。安装与首次运行见[快速开始](quickstart.zh-CN.md)。执行 `xamlnexus --help` 查看已安装版本的帮助，`xamlnexus --version` 查看版本。

## 命令名与参数写法

安装包提供 `xamlnexus`。PowerShell 中执行以下命令后，可以用 `xn` 代替它：

```powershell
Set-Alias -Name xn -Value xamlnexus
xn --help
```

别名只对当前会话有效；将 `Set-Alias` 那行加入 `$PROFILE` 后，新 PowerShell 会话也能使用。安装、更新、卸载仍使用包名 `XamlNexus`。

下文的 `<name>` 等表示需要替换的值，`[...]` 表示可选参数，不要输入尖括号或方括号。参数与值用空格分开；包含空格的路径用引号括起。支持 `--help`、`-h`、`-?` 和 `help` 查看帮助，`--version`、`-v` 和 `version` 查看版本。

## 打开对应版本示例：`gallery`

```powershell
dotnet tool update --global XamlNexus
xamlnexus gallery
```

无需位于项目目录。正式工具包内的清单固定 Gallery 版本、Windows x64 下载地址和 SHA-256。首次启动需要联网，之后使用 `%LOCALAPPDATA%/XamlNexus/Gallery` 下的已校验缓存，可离线运行。更新 tool 后，下次启动获取对应新版。已有 Gallery 正在运行时，请先关闭；命令不会强制结束它。

Gallery 设置和数据库仍位于 `%LOCALAPPDATA%/XamlNexus.Gallery`。旧程序缓存保留以便回退，关闭 Gallery 后可手动清理。取消或失败的下载不会安装，可重新执行命令。未包含发布清单的源码构建会提示本地构建方法。支持 Windows 10 1809 及更新版本，仅 x64。

## 创建项目：`new`

```powershell
xamlnexus new MyApp
xamlnexus new DataApp --profile basic --features settings,sqlite
xamlnexus new HybridApp --preset hybrid --output "D:\Projects" --language en-US
```

| 参数 | 含义与默认值 |
|---|---|
| `<name>` 或 `-n, --name <name>` | 必填项目名，以字母或下划线开头，只含字母、数字、下划线；首字母小写时转为大写 |
| `-p, --preset <preset>` | `winui`（默认）或 `hybrid`；选择进程架构 |
| `-o, --output <directory>` | 输出父目录，默认当前目录；项目生成在其下的项目名目录中 |
| `--profile <profile>` | `standard`（默认）或 `basic`；基础版省去完整设置界面，保留核心服务 |
| `--features <ids>` | 创建时添加组件，多个 ID 用逗号分隔，例如 `settings,sqlite` |
| `-l, --language <language>` | `zh-CN`（默认）或 `en-US`；设置生成项目的初始语言 |
| `-f, --solution-format <format>` | `sln`（默认）或 `slnx`；SLNX 要求选中的 SDK 为 9.0.200 或以上 |

架构参数也接受 `winui3`、`winui-wpf`、`winui3-wpf`，语言也接受 `zh`、`en`。建议使用表中的标准名称。

直接运行 `xamlnexus` 会进入交互式创建，需要可交互终端。脚本中使用 `new` 并明确参数。`new` 不支持 `--dry-run` 或 `--json`。

## 指定已有项目

项目命令默认从当前目录定位项目，也可使用 `-p, --project <path>` 指定目录或 `xamlnexus.json` 文件：

```powershell
xamlnexus list --project "D:\Projects\MyApp"
xamlnexus validate --project "D:\Projects\MyApp\xamlnexus.json"
```

`run`、`list`、`status`、`validate`、`doctor`、`upgrade` 也接受位置参数路径，例如 `xamlnexus run ./MyApp`。路径只指定一次，不要同时传位置路径和 `--project`。`new -p` 表示架构，不表示项目路径。

## 开发运行：`run`

```powershell
xamlnexus run
xamlnexus run --dry-run
xamlnexus run --no-build
```

支持项目路径、`--no-build`、`--dry-run` 和 `--json`。默认构建并运行 Debug/x64/非打包应用，自动选择纯 WinUI 入口或混合后台宿主。

- `--dry-run` 只显示将执行的构建与启动操作，不启动应用。
- `--no-build` 运行已有输出，不编译最近修改；需要已有构建结果。
- Ctrl+C 停止本次运行。修改后重新执行 `run`；目前没有 watch、热重载或 Release/MSIX 运行参数。

## 添加页面：`page add`

```powershell
xamlnexus page add Orders --dry-run
xamlnexus page add Orders
xamlnexus page add Details --no-navigation
xamlnexus page add OrderDetails --kind details
xamlnexus page add OrderEditor --kind form
```

支持 `--project`、`--no-navigation`、`--dry-run` 和 `--json`。名称以英文大写字母开头，其余为英文字母或数字，例如 `Orders`。

默认生成普通 Page、ViewModel 并接入导航。`--no-navigation` 跳过导航接入，适合自行组织导航的项目。支持 `--kind blank|list|details|form`（默认 blank）：list 包含搜索、刷新和异步状态；details 演示导航 Payload、加载、重试和取消；form 提供新建/编辑、字段校验、脏状态、保存和重置。生成的数据源是可运行示例，接入业务时替换接口实现。详见[页面开发](business-page.zh-CN.md)和[列表页面模板](list-page.zh-CN.md)。

## 查看与管理组件

| 命令 | 用途 | 支持的选项 |
|---|---|---|
| `recipes` | 查看当前工具提供的组件 | `--json` |
| `list` | 查看项目已安装的模块 | 项目路径、`--json` |
| `add <id[,id...]>` | 添加一个或多个组件 | `--project`、`--dry-run`、`--json` |
| `remove <id>` | 移除一个组件 | `--project`、`--dry-run`、`--json` |
| `update <id>` | 将一个组件更新到当前工具提供的版本 | `--project`、`--dry-run`、`--json` |
| `update --all` | 在一个事务中更新全部过期组件 | `--project`、`--dry-run`、`--json` |

```powershell
xamlnexus recipes
xamlnexus add settings,sqlite --dry-run
xamlnexus add settings,sqlite
xamlnexus update sqlite --dry-run
xamlnexus update --all --dry-run
xamlnexus remove sqlite --dry-run
```

上述批量添加适用于尚未安装这两个组件的项目，例如基础版。标准版已包含 settings。不要重复添加已安装组件。`remove` 和 `update <id>` 每次处理一个组件；`update --all` 先在临时项目快照中演练所有更新，再一次提交。同版本更新不做修改，降级会被拒绝。

内置组件包括 `settings`、`editorconfig`、`sqlite`、`tray`、`updater`。前三个支持两种架构；后两个用于纯 WinUI，混合宿主已有对应能力。`updater` 依赖 `settings`。批量添加会处理依赖顺序；组件边界见[产品模型](../introduction/product-model.zh-CN.md)。

生成源码和配置都可以自行修改。组件更新和移除会检查文件，遇到用户修改或缺失时停止，避免覆盖用户内容；不会自动丢弃修改或重新计算哈希来跳过检查。

## 项目状态：`status`

```powershell
xamlnexus status
xamlnexus status --json
```

`status` 汇总工具与脚手架版本、项目校验、Doctor 结果及已安装 Recipe 的可用版本，并给出 `update --all --dry-run`、`upgrade --dry-run` 等建议命令。发现可更新内容不会导致失败退出；项目校验或 Doctor 存在错误时返回 `1`。

## 检查项目：`validate` 与 `doctor`

```powershell
xamlnexus validate --json
xamlnexus doctor
```

两者支持项目路径和 `--json`。创建项目前可使用 `doctor --environment [--json]`，该模式不接受项目路径。`validate` 检查清单及所记录文件；内容变化通常是警告，缺失文件是错误。`doctor` 进一步检查开发环境、工程引用与组件接入。它们不代替实际构建或 GUI 验证，详见[诊断说明](doctor.zh-CN.md)。

## 升级脚手架：`upgrade`

```powershell
xamlnexus upgrade --dry-run
xamlnexus upgrade
xamlnexus upgrade --conflict-output ./upgrade-conflicts
```

支持项目路径、`--dry-run`、`--json`、`--conflict-output <directory>`。根据生成基线合并脚手架管理的基础设施，保留能够安全合并的用户修改；无法可靠合并时报告冲突。

`--conflict-output` 在发生合并冲突时导出用于处理冲突的文件，会写入指定目录，因此不能与 `--dry-run` 同用。它不是“仅导出”开关，无冲突时仍会执行升级。解决方案格式保持不变，不会将 SLN 转为 SLNX。处理流程见[项目升级](project-upgrade.zh-CN.md)。


使用 `--resolve-from <目录>` 应用冲突导出中已编辑的 `.merge` 文件，可加 `--dry-run` 预览。请保留导出记录，并先清除所有冲突标记。支持范围和过期检查见[应用手动解决的冲突](project-upgrade.zh-CN.md#应用手动解决的冲突)。

## 预览、JSON 与退出码

`--dry-run` 用于 `run`、`page add`、`add`、`remove`、`update`、`upgrade`，检查并显示将执行的操作，不应用项目变更。`--json` 只改变输出格式，不阻止写入或启动；自动化预览应同时指定两个选项：

```powershell
xamlnexus add sqlite --dry-run --json
```

常规命令成功返回 `0`，执行或检查失败返回 `1`，参数错误返回 `2`。`run` 还会返回应用的退出码，Ctrl+C 取消返回 `130`。JSON 字段与输出边界见[变更预览和 JSON 输出](change-plans.zh-CN.md)。

## 区分三种更新

| 命令 | 更新对象 |
|---|---|
| `dotnet tool update --global XamlNexus` | 本机安装的 CLI 工具 |
| `xamlnexus update sqlite` | 项目中安装的 SQLite 组件 |
| `xamlnexus upgrade` | 项目中由脚手架管理的基础设施 |

工具更新不会自动修改已有项目。执行项目更新前先保存或提交修改，再预览变更。客户端的安装包、签名和在线更新发布使用生成项目自己的发布配置，详见[快速开始](quickstart.zh-CN.md)。

命令行名称 `updater` 和 `tray` 仍兼容旧名称 `app-update` 和 `system-tray`；为兼容已有项目，清单内部 ID 和受管理文件名保持不变。
