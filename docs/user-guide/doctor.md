# XamlNexus Doctor

[English](doctor.md) | [简体中文](doctor.zh-CN.md)

Run the read-only project and environment diagnostic from any directory inside
a generated project:

```powershell
xamlnexus doctor
xamlnexus doctor --project D:\Projects\MyApp
```

Use JSON output in CI or when attaching a diagnostic report to an issue:

```powershell
xamlnexus doctor --project D:\Projects\MyApp --json
```

The command exits with code `1` when an error is present. Warnings are reported
but keep exit code `0`.

## Checks

- `XD10xx`: Windows, the SDK selected in the project directory, and locally enabled NuGet sources;
- `XD20xx` and `XNxxxx`: manifest and managed-file integrity;
- `XD30xx`: Windows target frameworks and Windows App SDK version consistency;
- `XD40xx`: installed Recipe availability and Catalog version drift;
- `XD50xx`: SQLite Data-project ownership, WAL-related source hints, and hybrid Protobuf wiring;
- `XD60xx`: release metadata and publishing configuration.

Environment checks do not contact NuGet feeds. They inspect only locally
selected SDK and configured source state. SDK resolution follows `global.json`
in the project or its ancestors; another installed SDK does not make a missing
or unsupported selected SDK valid. Secrets and signing certificates are not printed.

Modified generated files are reported as customization warnings, not editing
violations. Missing required files remain errors. SQLite WAL detection is only
a source-text heuristic: finding WAL text does not verify the running database,
and not finding it produces a warning rather than blocking custom initialization.

## Check before creating a project

```powershell
xamlnexus doctor --environment
xamlnexus doctor --environment --json
```

No xamlnexus.json is required. Run from the intended creation directory: SDK and NuGet configuration resolution still follows that directory and its ancestors. This mode rejects project arguments and `--project` and does not create or repair files.

Standalone mode additionally reports OS/CLI architecture (XD1004), Windows SDK 10.0.19041+ x64 header/library discovery (XD1005), and verification limitations (XD1006). Discovery uses the registry or standard Windows Kits location; missing assets produce a warning, not proof that a custom toolchain is unusable. Each dotnet probe has a 15-second process timeout.

This is not build acceptance: it does not check NuGet connectivity, compile XAML or prove Windows App SDK runtime availability. Create, build and launch an application afterward. Existing project doctor behavior remains available. Environment messages and report headings support English/Chinese; JSON codes and field names remain stable.
