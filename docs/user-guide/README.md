# User guide

[English](README.md) | [简体中文](README.zh-CN.md)

## First development session

1. [Quickstart](quickstart.md): install → create → run → edit the home page → add pages → add components.
2. [Page development](business-page.md): ordinary Pages, ViewModels, dependency injection, and page lifecycle.
3. [Settings](settings.md) and [localization](localization.md): persistence, recovery, themes, and live language switching.

## Add capabilities as needed

| Capability | Guide |
|---|---|
| SQLite and data access | [SQLite](sqlite-recipe.md) |
| Tray icon, menus, and notifications | [System tray](system-tray-recipe.md) |
| Update checks and installation | [App updates](app-update-recipe.md) |

Use `xamlnexus recipes` to inspect available components and `xamlnexus list` for installed modules. Settings installation is covered in the quickstart; `editorconfig` adds editor configuration.

## Common commands

See the [command reference](commands.md) for all options, defaults, supported combinations, and exit codes.

| Task | Example |
|---|---|
| Create a default project | `xamlnexus new MyApp` |
| Run for development | `xamlnexus run` |
| Add an ordinary page | `xamlnexus page add Orders` |
| Add a capability | `xamlnexus add sqlite` |
| Preview component removal | `xamlnexus remove sqlite --dry-run` |
| Preview a component update | `xamlnexus update sqlite --dry-run` |
| Preview a scaffold upgrade | `xamlnexus upgrade --dry-run` |
| Validate the project | `xamlnexus validate` |
| Diagnose the environment and integration | `xamlnexus doctor` |
| Read help | `xamlnexus --help` |

Run project commands inside the generated project, or supply `--project`. Development running currently uses Debug/x64/unpackaged mode, without watch or hot reload.

## Maintenance and distribution

- [PR workflows and check settings](pr-checks.md): triggers, merge requirements, and how to enable, disable, or customize checks.

- [Change previews and JSON output](change-plans.md): inspect automatic changes before applying them.
- [Project upgrades](project-upgrade.md): distinguish component `update` from scaffold `upgrade`, and handle local changes and conflicts.
- [Diagnostics](doctor.md): investigate SDK, project reference, and integration problems.
- [Generated-project release instructions](../../src/Templates/Shared/RELEASING.md): prepare installers, signing, and update assets. Use your generated project's own `RELEASING.md` and `eng/publishing/release.json` when publishing.

All generated source and configuration can be customized. Content differences produce warnings; automatic removal or upgrades stop with a conflict when they cannot protect user changes. See the [product model](../introduction/product-model.md).

[List page template](list-page.md): search, asynchronous loading, cancellation and replacing the data source.
