# 应用发布配置

[English](RELEASING.md) | [简体中文](RELEASING.zh-CN.md)

生成项目的 `.github` 仅提供面向 `main` 的 PR 还原、构建与测试。若需在失败时阻止合并，请通过分支保护或规则集将其设为必需检查。没有自动发布、发布标签要求或版本递增校验。发布触发条件、凭据和上传位置由用户自行配置；本地发布脚本保留在 `eng/publishing` 中。

## 生成安装器

检查 `eng/publishing/release.json` 中的解决方案、启动项目、应用名、运行时和签名配置。解决方案路径与创建时选择的 SLN/SLNX 格式一致。准备 WinUI 构建环境、所需 .NET SDK 和 Inno Setup（`ISCC.exe`）；SLNX 要求 SDK 9.0.200 或以上。

在生成项目根目录执行：

```powershell
./eng/publishing/Build-Installer.ps1 -Version 1.0.4
```

脚本发布自包含的 win-x64 应用，并在 `artifacts/release` 中生成安装器；此步骤不会自动签名或上传。

## 签名与更新

使用 `Sign-Installer.ps1 -InstallerPath <path>` 签名。脚本读取发布配置中的 `requireSigning`、`timestampUrl`，以及环境变量 `WINDOWS_SIGNING_PFX_BASE64`、`WINDOWS_SIGNING_PASSWORD`；凭据由用户自行配置。

使用在线更新时，将 `Consts.Updates.ManifestUrl` 配置为自己的更新清单地址。更新器按 `System.Version` 比较版本，因此版本使用三段或四段数字；`Directory.Build.props` 中的应用版本需与安装器版本一致。

更新清单、安装器及哈希文件由用户按自己的发布流程准备和托管。
