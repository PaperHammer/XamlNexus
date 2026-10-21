---
name: xamlnexus-cli
description: Use XamlNexus CLI to create XAML desktop projects, add pages or Recipes, inspect project health, and maintain generated scaffolds. Use for CLI operations; use the project skill for editing business code.
---

# XamlNexus CLI operations

Use the installed CLI's `--help` and `--version` as the command contract. In the XamlNexus source checkout, build `src/XamlNexus/XamlNexus.csproj` and invoke its `bin/Debug/net8.0/XamlNexus.dll` through `dotnet` when an installed tool would use a different version.

Pass the target directory explicitly with `--project` for existing-project operations. Discover its identity and installed capabilities through `xamlnexus.json`, `list --json`, and `status --json`; do not infer current capabilities from the initial basic/standard profile.

## Choose the existing operation

- Create: `xamlnexus new MyApp --preset winui --profile basic --output <parent>`. Presets are `winui` and `hybrid`; solution formats are `sln` and `slnx`. The output is a new child directory and automatically includes CLI/project skills and root development rules. `new` has no `--json` or `--dry-run` option.
- Pages: `xamlnexus page add Orders --kind list --project <root> --dry-run --json`. Kinds are `blank`, `list`, `details`, `form`. Run the same authorized operation without `--dry-run` to apply it. `--no-navigation` leaves registration to the application.
- Components: first query `xamlnexus recipes --json`. Use `add`, `remove`, or `update` with `--project`, `--dry-run`, and `--json` for a machine-readable plan. Command aliases `tray` and `updater` map to manifest IDs `system-tray` and `app-update`. Comma-separated `add` and `update --all` are transactional batches.
- Diagnose: `xamlnexus validate --project <root> --json`; `xamlnexus doctor --project <root> --json`. `doctor --environment --json` does not require a generated project.
- Upgrade: `xamlnexus upgrade --project <root> --dry-run --json`. Read [references/maintenance.md](references/maintenance.md) when updating/removing customized components or resolving upgrade conflicts.
- Build plan: `xamlnexus run --project <root> --dry-run --json` returns executable/argument arrays and the working directory. Use the build command from that plan for compilation-only checks. Applying `run` opens the application; follow the user's runtime-validation scope. `--no-build` uses existing output; it is not hot reload.

## Apply and verify

Inspect plans that affect existing project structure; proceed within the user's authorized scope without adding an approval round for every ordinary edit. Respect CLI conflicts rather than bypassing them with manual deletions or manifest/hash edits. Check exit codes and JSON status/error fields; `--json` changes output format, not authorization or write behavior. Conflict export writes files even though it is a planning aid.

Generated pages are user business code. Recipe files and scaffold baselines have distinct ownership. After structural changes, run `validate` and the build/checks relevant to the change. Compilation does not verify native dialogs, focus, tray behavior, or MSIX runtime. Report those acceptance boundaries when material.

Preserve unrelated local edits. Do not commit, push, publish, launch UI, or send data externally unless covered by the user's task.
