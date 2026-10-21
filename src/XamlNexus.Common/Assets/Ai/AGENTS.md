# {{APP_NAME}} development rules

Generated application: **{{PRESET}}**, initial profile **{{PROFILE}}**, initial language **{{LANGUAGE}}**, solution `{{APP_NAME}}.{{SOLUTION_FORMAT}}`. Read `xamlnexus.json` and actual project references for current capabilities; the starting profile is not a live module inventory.

## Project guidance

- Use the `xamlnexus-cli` skill for CLI creation, pages, Recipes, diagnosis and upgrades.
- Use the `xamlnexus-project` skill for application architecture and business code. Start with its `references/architecture.md`; load `references/pages.md` when changing pages/navigation/lifecycle.
- Skills are supplied in `.agents/skills`, `.claude/skills`, and `.qwen/skills`. They start with identical content. Customize the directory used by your agent. Guidance is maintained through scaffold upgrades with the existing merge/conflict workflow.

## Development conventions

- Pages and their ViewModels/data sources are business code in `{{APP_NAME}}.MainPanel`. Use constructor injection and the existing module catalog. Respect the preset's frontend/host boundary.
- Preserve awaited navigation and leave/close guards, including same-page record changes. Failed saves or cancellation keep the draft and sidebar selection. Respect KeepAlive lifecycle and container ownership of shared services.
- Treat scaffold, Recipe, and user business ownership separately. Keep recorded baselines intact; use CLI previews and supported conflict resolution for maintenance rather than altering hashes to bypass checks.
- Follow the application's existing XAML styles, resource/localization keys, naming and formatting. Keep page titles/navigation consistent with the current application; do not edit generated `bin`/`obj` or runtime Plugins output.
- Use the user's requested scope and existing authorization. Guidance files do not authorize publishing, destructive data changes or sending project data to another service.

## Build and acceptance

From the project root:

```powershell
xamlnexus validate --project . --json
dotnet build "{{STARTUP_PROJECT}}" -p:Configuration=Debug -p:Platform=x64 -p:RuntimeIdentifier=win-x64 -p:WindowsPackageType=None
```

`xamlnexus run --project . --dry-run --json` gives the current build/startup plan. Applying `run` opens the application. Run the checks relevant to the modification and state any material runtime gaps; a build does not establish native UI, focus, tray or MSIX acceptance.
