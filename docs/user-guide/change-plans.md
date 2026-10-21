# Change plans and machine-readable output

[English](change-plans.md) | [简体中文](change-plans.zh-CN.md)

Commands that modify an existing project support a validated preview:

```powershell
xamlnexus add sqlite --project D:\Projects\MyApp --dry-run
xamlnexus remove sqlite --project D:\Projects\MyApp --dry-run
xamlnexus update sqlite --project D:\Projects\MyApp --dry-run
xamlnexus update --all --project D:\Projects\MyApp --dry-run
xamlnexus upgrade --project D:\Projects\MyApp --dry-run
```

`--dry-run` builds and validates the same file and structured project-change
plan used by the real transaction, but does not update project files or
`xamlnexus.json`. An upgrade may create and remove a temporary target scaffold
outside the project while planning.

Add `--json` when a script or CI job needs to consume the result:

```powershell
xamlnexus add sqlite --project D:\Projects\MyApp --dry-run --json
```

A successful single-item preview has `status: "planned"`; a batch add or
`update --all` preview has `status: "preview"`. Both include `dryRun: true`,
version information and changed paths. Single Recipe changes also include an
`owned` or `project` scope. Upgrade plans expose `canApply`, conflicts, and only the
change kind and relative path; generated file contents are never included.
An upgrade change reports `strategy: "textMerge"` for independent line edits
or `strategy: "xmlMerge"` when an XML/MSBuild/XAML semantic merge combines
independent attributes or keyed child nodes. Independent `.sln` project,
section, and configuration additions use `strategy: "solutionMerge"`.
Unmerged replacements use `strategy: "direct"`.

Conflict contents are likewise excluded from JSON. To export reviewable
`LOCAL`/`BASE`/`TARGET` documents, use `upgrade --conflict-output <directory>`
without `--dry-run`. Export is explicit and never overwrites an existing file.

The same four commands support `--json` without `--dry-run`. Successful
transactions return `status: "applied"` (or a more specific no-op/baseline
status) and their changed paths. Failures use this stable envelope:

```json
{
  "operation": "add",
  "status": "error",
  "error": {
    "code": "XR1201",
    "message": "XR1201: ..."
  }
}
```

`code` is null when an error does not originate from a versioned Recipe or
upgrade contract. Exit codes remain authoritative: `0` means success, `1`
means validation/planning/application failure, and `2` means invalid command
usage. JSON is written as one document to standard output.

## Query commands

`status`, `list`, `validate`, and `recipes` also accept `--json`:

```powershell
xamlnexus status --project D:\Projects\MyApp --json
xamlnexus list --project D:\Projects\MyApp --json
xamlnexus validate --project D:\Projects\MyApp --json
xamlnexus recipes --json
```

`status` combines scaffold and Recipe versions with validation and Doctor
results, plus suggested maintenance commands. Available updates do not make it
unhealthy; validation or Doctor errors return exit code `1`. `list` returns
project identity, generator version, and installed modules.
`validate` returns `status: "valid"` or `"invalid"`, an `isValid` boolean, and
the complete coded issue list. `recipes` returns the complete built-in Recipe
descriptors, including compatibility, dependency, and conflict metadata.

Operational and command-line failures use the same `operation/status/error`
envelope as change commands. Invalid syntax requested with `--json` uses code
`XC1001`; project lookup or manifest-loading failures use `XL1001` for `list`
and `XV1001` for `validate`. Human-readable output remains the default.
