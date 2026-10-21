# XamlNexus documentation

[English](README.md) | [简体中文](README.zh-CN.md)

XamlNexus generates runnable, customizable Windows desktop prototypes. Start with the quickstart, or use the categories below to find implementation details and usage guides.

These documents describe the 1.0.5 source. Check the installed tool with `xamlnexus --version`; updating the CLI does not automatically update generated applications. Use `update <id>` for an installed component or `upgrade --dry-run` to review scaffold changes.

| Category | Contents | Start here |
|---|---|---|
| Introduction | Goals, profiles, capability boundaries, and repository layout | [Introduction](introduction/README.md) |
| Technical documentation | Architecture, modules, navigation, Recipes, and manifests | [Technical documentation](technical/README.md) |
| User guide | Installation, creation, development, components, upgrades, and diagnostics | [User guide](user-guide/README.md) |

## Suggested reading order

- First-time users: [Quickstart](user-guide/quickstart.md) → [Page development](user-guide/business-page.md) → add components as needed.
- Understanding the design: [Product model](introduction/product-model.md) → [Architecture](technical/architecture.md).
- Building custom capabilities: [Module lifecycle](technical/module-lifecycle.md) → [Recipe contract](technical/recipe-contract.md) → [Change previews](user-guide/change-plans.md).
- Maintaining generated projects: [Diagnostics](user-guide/doctor.md) → [Project upgrades](user-guide/project-upgrade.md).
- Business navigation and shutdown: [Application activation](technical/app-activation.md) → [Navigation guards and rollback](technical/navigation.md) → [Hybrid shutdown](technical/hybrid-close.md).
- End-to-end business example: [BusinessWorkflow](../samples/XamlNexus.BusinessWorkflow/README.md), with SQLite persistence and headless upgrade acceptance.
