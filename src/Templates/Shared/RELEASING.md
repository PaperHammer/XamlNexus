# Application release setup / 应用发布配置

Generated projects include one `.github` workflow that restores, builds, and tests pull requests targeting `main`. To block merging on failures, configure branch protection or a ruleset requiring the check. There are no automatic releases, required release labels, or version-increase checks. Configure release triggers, credentials, and upload destinations yourself. Local publishing helpers remain under `eng/publishing`.

生成项目的 `.github` 仅提供面向 `main` 的 PR 还原、构建与测试。若需在失败时阻止合并，请通过分支保护或规则集将其设为必需检查。没有自动发布、发布标签要求或版本递增校验。发布触发条件、凭据和上传位置由用户自行配置；本地发布脚本保留在 `eng/publishing` 中。

## Build an installer / 生成安装器

Review `eng/publishing/release.json` for the solution, startup project, application name, runtime, and signing settings. The solution path follows the selected SLN or SLNX format. Prepare a WinUI build environment, the required .NET SDK, and Inno Setup (`ISCC.exe`). SLNX requires SDK 9.0.200 or newer.

检查 `eng/publishing/release.json` 中的解决方案、启动项目、应用名、运行时和签名配置。解决方案路径与创建时选择的 SLN/SLNX 格式一致。准备 WinUI 构建环境、所需 .NET SDK 和 Inno Setup（`ISCC.exe`）；SLNX 要求 SDK 9.0.200 或以上。

From the generated project root / 在生成项目根目录执行：

```powershell
./eng/publishing/Build-Installer.ps1 -Version 1.0.4
```

The script publishes a self-contained win-x64 application and creates an installer in `artifacts/release`. Building an installer does not sign or upload it.

脚本发布自包含的 win-x64 应用，并在 `artifacts/release` 中生成安装器；此步骤不会自动签名或上传。

## Signing and updates / 签名与更新

Use `Sign-Installer.ps1 -InstallerPath <path>` to sign the installer. It reads `requireSigning` and `timestampUrl` from the release configuration and uses `WINDOWS_SIGNING_PFX_BASE64` and `WINDOWS_SIGNING_PASSWORD` environment variables. Configure these credentials yourself.

使用 `Sign-Installer.ps1 -InstallerPath <path>` 签名。脚本读取发布配置中的 `requireSigning`、`timestampUrl`，以及环境变量 `WINDOWS_SIGNING_PFX_BASE64`、`WINDOWS_SIGNING_PASSWORD`；凭据由用户自行配置。

For online updates, set `Consts.Updates.ManifestUrl` to your update manifest URL. Versions must contain three or four numeric components because the updater compares `System.Version` values. Keep application versions in `Directory.Build.props` aligned with the installer version.

使用在线更新时，将 `Consts.Updates.ManifestUrl` 配置为自己的更新清单地址。更新器按 `System.Version` 比较版本，因此版本使用三段或四段数字；`Directory.Build.props` 中的应用版本需与安装器版本一致。

Prepare and host the update manifest, installer, and hash file using your own release process.

更新清单、安装器及哈希文件由用户按自己的发布流程准备和托管。
