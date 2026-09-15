# Technical documentation

[English](README.md) | [简体中文](README.zh-CN.md)

This section explains the scaffolding implementation and how to extend generated applications. For command usage, see the [user guide](../user-guide/README.md).

| Document | Questions covered |
|---|---|
| [Architecture and key flows](architecture.md) | How the CLI generates projects, how each process architecture runs, and how automatic changes protect user code |
| [Module lifecycle](module-lifecycle.md) | Service registration, initialization order, page injection, and resource ownership |
| [Navigation contract](navigation.md) | Page registration and integration with standard or custom windows |
| [Recipe contract](recipe-contract.md) | Component metadata, file plans, project operations, updates, and removal |
| [Project manifest](project-manifest.md) | Module origins, file hashes, baselines, and compatibility boundaries |

After changing core mechanisms, run `dotnet test src/XamlNexus.TemplateTests/XamlNexus.TemplateTests.csproj`. For generated-project dependencies, templates, or build configuration, also run the [generated-project acceptance script](../../eng/Test-GeneratedProjects.ps1). Automated tests check code and generated structure. WinUI builds check compilation. GUI behavior and installation upgrades require separate runtime checks.
