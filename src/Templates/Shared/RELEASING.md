# Application release setup

[English](RELEASING.md) | [简体中文](RELEASING.zh-CN.md)

Generated projects include one `.github` workflow that restores, builds, and tests pull requests targeting `main`. To block merging on failures, configure branch protection or a ruleset requiring the check. There are no automatic releases, required release labels, or version-increase checks. Configure release triggers, credentials, and upload destinations yourself. Local publishing helpers remain under `eng/publishing`.

## Build an installer

Review `eng/publishing/release.json` for the solution, startup project, application name, runtime, and signing settings. The solution path follows the selected SLN or SLNX format. Prepare a WinUI build environment, the required .NET SDK, and Inno Setup (`ISCC.exe`). SLNX requires SDK 9.0.200 or newer.

From the generated project root:

```powershell
./eng/publishing/Build-Installer.ps1 -Version 1.0.4
```

The script publishes a self-contained win-x64 application and creates an installer in `artifacts/release`. Building an installer does not sign or upload it.

## Signing and updates

Use `Sign-Installer.ps1 -InstallerPath <path>` to sign the installer. It reads `requireSigning` and `timestampUrl` from the release configuration and uses `WINDOWS_SIGNING_PFX_BASE64` and `WINDOWS_SIGNING_PASSWORD` environment variables. Configure these credentials yourself.

For online updates, set `Consts.Updates.ManifestUrl` to your update manifest URL. Versions must contain three or four numeric components because the updater compares `System.Version` values. Keep application versions in `Directory.Build.props` aligned with the installer version.

Prepare and host the update manifest, installer, and hash file using your own release process.
