# XamlNexus Recipe contract v1

[English](recipe-contract.md) | [简体中文](recipe-contract.zh-CN.md)

A Recipe is a versioned, transactional change to an existing generated
project. The v1 contract is implemented in `XamlNexus.Common.Recipes` and is
shared by built-in Recipes and future package-based Recipe adapters.

Use `xamlnexus recipes` to inspect the built-in Catalog and
`xamlnexus add <recipe> [--project <path>]` to install one. Use
`xamlnexus remove <recipe> [--project <path>]` to remove an installed Recipe.
Use `xamlnexus update <recipe> [--project <path>]` to migrate it to the newer
version in the active Catalog.
The built-in Recipes are `settings`, `editorconfig`, `sqlite`, `system-tray`, and
`app-update`. The first three support both official Presets; the last two support
pure WinUI because the hybrid host already owns its tray and updater. App-update
depends on settings. See the [SQLite guide](../user-guide/sqlite-recipe.md),
[system tray guide](../user-guide/system-tray-recipe.md), and
[app update guide](../user-guide/app-update-recipe.md) for runtime boundaries.

## Descriptor

Every Recipe declares:

- a lowercase kebab-case ID and semantic version;
- a display name and description;
- supported Presets (`winui`, `hybrid`, or both);
- module dependencies that must already be installed;
- modules that conflict with the Recipe.

The Recipe ID becomes a case-insensitively unique module ID in
`xamlnexus.json`. A successfully installed Recipe is recorded with
`source: "recipe"` and its own version.

## File and project changes during installation

`IXamlNexusRecipe.CreatePlan` returns file operations and optional structured
project operations. File operations are:

- `Create` creates a file only when the destination does not exist.
- `Replace` replaces an existing file only when its SHA-256 matches the
  Recipe's expected hash.
- `Delete` deletes an existing file only when its SHA-256 matches the expected
  hash.

Paths are relative to the project root. Absolute paths, paths that escape the
project, duplicate destinations, and direct edits to `xamlnexus.json` are
rejected. Recipe code must not modify the project while it is creating a plan.

Structured project operations support:

- adding a `PackageReference` to an existing `.csproj`;
- ensuring that a `PackageReference` exists at or above a numeric minimum
  version, without downgrading a newer reference;
- adding a `ProjectReference` to an existing `.csproj`;
- adding an existing or transaction-created `.csproj` to a `.sln` or `.slnx`,
  including the SLN build-configuration mappings;
- adding an existing or transaction-created `.proto` source to an MSBuild
  gRPC project.

These operations parse MSBuild XML or the solution structure and reject an
existing equivalent reference. They do not perform textual search-and-replace.

`EnsurePackageReference` rejects conditional references with `XR1247`, including
conditions on the item, its ancestors or metadata, conditional `Update` items,
and `Choose` branches. It cannot guarantee the dependency for every build
configuration. Review the conditions and provide an unconditional reference
meeting the minimum version before retrying. Rejection occurs during plan
validation, before project files or the manifest are written.
Shared `.csproj`, `.sln` and `.slnx` files are transactionally restored on failure but
are not recorded as exclusively owned Recipe files. This allows later Recipes
and users to make independent changes to the same project structure.

## Removing a component

Recipe-owned files and their installation hashes are recorded in
`xamlnexus.json`. Removal checks every current file against that hash before it
deletes anything. A missing or user-modified owned file aborts removal without
changing the manifest.

Recipes can implement `IXamlNexusRecipeRemovalPlanProvider` to declare inverse
structured operations. The contract supports removing an exact
`ProjectReference`, solution project and its build-configuration and folder mappings, and
MSBuild `Protobuf` item. Item groups containing comments or other non-whitespace
content are retained after item removal. Unrelated XML, solution projects, and user files are
preserved. Package references ensured during installation are not downgraded
or removed because their pre-installation state is not owned by the Recipe.

## Updating a component

Generated Recipe files are available for user customization. Normal validation
reports content changes as warning `XN1202`; they do not block `run`, `page add`,
or other development work. Missing tracked files remain error `XN1201`.
The manifest preserves the original generation baseline instead of accepting
edits automatically. Update and removal retain strict hash preconditions and
reject modified files before writing anything, protecting user changes.

An update compares the installed module version with the Recipe version in the
active Catalog. Equal versions are a no-op at the CLI, and downgrades are
rejected. For a newer version, the installation plan describes the desired
owned-file set:

- an existing owned path becomes `Replace` with the installed SHA-256 as its
  precondition;
- a newly declared path remains `Create`;
- an old owned path no longer declared becomes `Delete` with its installed
  SHA-256 as its precondition.

This makes user edits block the complete update before any file is written.
Recipes that change `.csproj`, `.sln` or `.slnx` structure implement
`IXamlNexusRecipeUpdatePlanProvider` and return explicit structured migration
operations for the installed version. Installation project operations are not
replayed automatically because doing so could duplicate existing references.

Recipes that need dependency injection or startup work can add a host-side
module using the trim-safe [generated application module lifecycle](module-lifecycle.md).
This keeps Recipe integration out of `App.xaml.cs` while preserving explicit,
deterministic startup behavior.

## Transaction rules

Before writing, XamlNexus validates the descriptor, Preset, dependencies,
conflicts, every target path, file existence, and all replacement/deletion
hashes. This prevents a Recipe from silently overwriting user changes.

Files are written through same-directory temporary files. If applying the plan
fails, XamlNexus restores files already changed and removes empty directories
created by the transaction. The project manifest is updated last through its
own atomic writer. A failed transaction does not add the Recipe module.
Removal follows the same ordering and rollback rules: its manifest entry is
removed only after all owned-file and structured changes succeed.

## Stable failures

Recipe contract errors use `XR` codes:

- `XR10xx`: invalid Recipe metadata;
- `XR11xx`: Preset, dependency, installed-module, or conflict failures;
- `XR12xx`: unsafe or stale file plan;
- `XR13xx`: transaction execution or rollback failure.
- `XR14xx`: invalid Recipe removal request.
- `XR15xx`: invalid Recipe update or version transition.

The contract deliberately does not define arbitrary script execution or
textual search-and-replace. Future operation types should remain explicit,
declarative, and independently testable.

SLNX solution operations edit XML Project elements by Path, including projects inside folders.
Unrelated folders and configuration elements are preserved. Use the manifest
`project.solutionFormat` when constructing a Recipe solution path.

Recipe transactions, page additions, and scaffold upgrades share a per-project
write lease. Concurrent writers are rejected; retry after the active operation
finishes. Transactions recheck the manifest and file preconditions while holding
the lease. `XR1304` means the supplied project context is stale: reload it with
`XamlNexusProjectLocator.Locate` and preview the operation again. A batch whose
manifest changed since planning fails with `XR1803`. The lease coordinates
XamlNexus commands; external editors do not participate in it.

Project paths and operation targets must not traverse junctions, symbolic links,
or other reparse points. This includes ancestor directories of the project root,
so an alias cannot bypass the per-project write lease. Use the physical project
directory instead. Target paths are checked again before transaction writes.
