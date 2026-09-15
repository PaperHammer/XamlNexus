# Application updates

[English](app-update.README.md) | [简体中文](app-update.README.zh-CN.md)

The app-update Recipe registers the updater and enables its settings entry automatically.
Configure `Consts.Updates.ManifestUrl` in `{{ProjectName}}.Common/Consts.cs` with your HTTPS
update manifest. The installer and SHA-256 URLs must also use HTTPS.
Installing this module does not publish an update feed or configure signing.
This installer flow targets unpackaged WinUI applications; MSIX uses its distribution channel.
Remove unchanged module files with `xamlnexus remove updater`.
Existing projects with a built-in updater must be migrated before adding this Recipe.
