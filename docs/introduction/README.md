# Introduction

[English](README.md) | [简体中文](README.zh-CN.md)

XamlNexus is a .NET CLI scaffolding tool for Windows desktop development. It generates runnable, editable projects with common client infrastructure and lets you add components as needed.

## Choose a starting point

The default standard client includes a settings panel. A basic profile is also available. Optional components become source code in the user's project, where pages, services, configuration, and release scripts can be edited directly.

Two independent options select the starting point:

- `--preset winui|hybrid` selects the process architecture: pure WinUI, or a WinUI frontend with a WPF background host.
- `--profile standard|basic` selects the initial capabilities. Basic omits the full settings panel but keeps configuration, logging, navigation, themes, and localization infrastructure.

See the [product model](product-model.md) for capabilities and constraints. The hybrid host's tray and updater are still built in, rather than fully optional components.

## Source ownership

`add` integrates component source and configuration into the project. The application registers and runs its own modules without a runtime scaffolding service. A Recipe describes how to add a component to a project: which files to copy, which dependencies to add, and which configuration to change.

`xamlnexus.json` records origins, versions, and baselines. Hashes protect user changes during automatic updates, removals, and upgrades; they are not editing permissions. Users do not need to recalculate hashes to keep developing. Manual conflict handling is required only when automatic changes cannot be made safely.

## Repository layout

| Directory | Purpose |
|---|---|
| `src/XamlNexus` | CLI entry point, command handling, and tool packaging |
| `src/XamlNexus.Common` | Creation, pages, running, diagnostics, transactions, and upgrades |
| `src/XamlNexus.Generator.*` | Generation adapters for the two architectures |
| `src/XamlNexus.Recipes.BuiltIn` | Built-in component Recipes and assets |
| `src/Templates` | Client source templates and shared publishing assets |
| `src/XamlNexus.TemplateTests` | Automated tests |
| `samples/SqliteShowcase` | Runnable composition example |
| `eng`, root `.github` | Repository acceptance and release workflows |
| `docs` | Introduction, technical documentation, and user guides |

Generated projects include `.gitignore` and `.github`. The latter provides only a PR restore/build/test workflow and a short PR template; users configure automated publishing themselves. Local release configuration is in `eng/publishing/release.json`. This repository retains its own root `.github/workflows` directory.

## Getting started

Follow the [quickstart](../user-guide/quickstart.md) to run your first project, or read the [architecture](../technical/architecture.md) to understand the implementation.
