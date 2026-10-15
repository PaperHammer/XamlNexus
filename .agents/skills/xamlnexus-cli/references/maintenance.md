# Maintaining customized projects

Read `xamlnexus.json` and `status --json` for the installed generator/Recipe versions and file ownership. Page generation does not turn business files into removable Recipes. The original basic/standard profile is a starting point, not the current module list.

## Recipes

Use `add`, `update`, `update --all`, and `remove` previews before changing installed components. Recipe update/removal verifies registered files against installation hashes; modified or missing files stop the operation. Preserve the user's content and inspect the reported difference. Do not edit stored hashes to make an operation pass. Shared project/solution files are modified by structural operations, not owned exclusively by a Recipe.

## Scaffold upgrades

`upgrade --dry-run --json` compares recorded BASE, actual LOCAL, and generated TARGET. Customized compatible text/XML/solution edits can be merged; ambiguous structure, overlapping changes, unknown baselines, ownership conflicts, or deletion of customized files require explicit resolution.

For supported merge conflicts:

1. Export into a new directory with `upgrade --project <root> --conflict-output <directory> --json`. This writes conflict artifacts and cannot be combined with `--dry-run`.
2. Inspect LOCAL/BASE/TARGET in the exported `.merge` files and preserve the requested local behavior. Do not delete a merge file to request deletion of a source file.
3. Preview with `upgrade --project <root> --resolve-from <directory> --dry-run --json`, then apply the resolved plan within the authorized task.

Stale exports, remaining conflict markers, invalid XML, and unsupported conflict categories remain errors. Re-export against current files if the plan is stale. Do not replace an application's `App.xaml.cs` wholesale or remove lifecycle/close guards just to resolve a textual conflict.

AI guidance is written automatically when creating a project, recorded as user-editable scaffold baselines, and maintained through the same upgrade merge/conflict workflow. Preserve local team conventions when merging guidance updates.
