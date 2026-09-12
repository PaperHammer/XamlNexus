# App update Recipe

[English](app-update-recipe.md) | [简体中文](app-update-recipe.zh-CN.md)

The current pure WinUI template does not register an updater or expose update controls by default.

```powershell
xamlnexus add app-update --project <project-directory>
xamlnexus remove app-update --project <project-directory>
```

Adding the Recipe installs the HTTPS update source, verified downloader, installer lifecycle,
client and module registration. The existing settings panel detects the optional service.
The Recipe depends on `settings`; installing it in a basic project automatically
adds the settings panel. Standard projects reuse their included settings module.
No manual App.xaml.cs edits are required. Configure `Consts.Updates.ManifestUrl` with your
own HTTPS manifest; publication and signing remain application-specific work.

Removal deletes unchanged owned files and disables the settings entry on the next build/run.
User-edited owned files are protected by the existing Recipe transaction checks. The minimal
updater contract remains in the core template and the settings presentation remains
in the settings module. Removing app-update leaves settings installed.

This Recipe supports new pure WinUI projects. Hybrid and the existing Showcase retain their
built-in updater. Older projects containing a built-in updater require migration first;
adding over existing updater files is refused rather than overwriting them.
The installer path targets unpackaged apps; MSIX updates follow their distribution channel.
