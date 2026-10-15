---
name: xamlnexus-project
description: Develop or diagnose a generated XamlNexus WinUI or hybrid application, including its XAML pages, ViewModels, services, navigation, activation, and leave/close lifecycle. Use for application code rather than changing the XamlNexus generator repository.
---

# Develop the generated application

Read the project-root `AGENTS.md` and `xamlnexus.json` to identify the project, preset, initial profile, and current installed modules. Inspect actual project references and source before relying on generated guidance after customization or an upgrade.

Read [references/architecture.md](references/architecture.md) for this project's startup, dependency injection, and host boundaries. For page, form, navigation, or close changes, read [references/pages.md](references/pages.md). Use `xamlnexus-cli` when the task involves CLI generation or maintenance.

Keep UI presentation in the panel, page state/actions in the ViewModel, and storage/network access behind a data-source/service interface. Extend the actual application's conventions rather than replacing its architecture. Use generated page examples and services as the starting point; the stock data sources demonstrate the contract and do not by themselves persist business data.

Use `xamlnexus run --project <root> --dry-run --json` for the selected startup/build command, or use the build command in `AGENTS.md`. Build the Windows architecture explicitly. Apply the verification appropriate to the affected logic; compilation alone does not prove a XAML binding, native confirmation dialog, or cross-process activation worked at runtime.
