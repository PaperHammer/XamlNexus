# AI development guidance

[English](ai-guidance.md) | [简体中文](ai-guidance.zh-CN.md)

XamlNexus bundles two kinds of guidance with the CLI for existing AI coding tools. These are skills, architecture references and development rules; no model, inference engine or separate agent harness is included.

| Skill | Purpose | Availability |
|---|---|---|
| `xamlnexus-cli` | Creation, pages, Recipes, diagnosis, upgrades, JSON plans and ownership/conflict handling | Included in the source repository and generated automatically |
| `xamlnexus-project` | Application architecture, host boundaries, module registration, DI, page lifecycle, business activation and close protection | Generated automatically |

## New applications

```powershell
xamlnexus new MyApp --preset winui --profile basic
xamlnexus new HybridApp --preset hybrid
```

Guidance is written as local files during project creation; no separate installation or initialization command is needed. The source repository also includes CLI skill files.

Each application contains root `AGENTS.md`, `CLAUDE.md`, `QWEN.md`, and tool-specific skill directories:

| Tool | Project skills | Rules entry point |
|---|---|---|
| Codex | `.agents/skills` | `AGENTS.md` |
| Claude Code | `.claude/skills` | `CLAUDE.md` imports `AGENTS.md` |
| Qwen Code | `.qwen/skills` | `QWEN.md` points to `AGENTS.md` |

The three skill directories initially contain identical copies of both skills. Application guidance includes the actual project name, preset, initial profile/language, solution format and startup project. WinUI starts from `.UI`; hybrid starts from the WPF host, keeping business services and storage behind the existing RPC boundary.

Root rules cover business ownership, constructor injection, leave/close guards, maintenance baselines, existing styles/localization, and build/diagnostic commands. Use the manifest and actual code for current capabilities; the initial profile is not a live module inventory.

## Customization and maintenance

Extend rules with actual naming, business boundaries, team conventions and test commands. Put longer explanations in skill `references`. Guidance does not enforce command permissions or runtime isolation.

For new applications, guidance is tracked as editable scaffold baselines and maintained through `upgrade --dry-run` and the existing three-way conflict workflow. See [project upgrades](project-upgrade.md).

Example request: “Use XamlNexus CLI to add an Orders list page, connect its data source following project conventions, and verify compilation.” The agent reads the relevant skills and actual code/command version before applying the CLI operation and business edits. A successful build does not establish native window, focus or unsaved-dialog acceptance.

Directory conventions follow [Codex skills](https://learn.chatgpt.com/docs/build-skills), [Claude Code skills](https://code.claude.com/docs/en/skills) and [Qwen Code skills](https://qwenlm.github.io/qwen-code-docs/en/users/features/skills/). Actual discovery/invocation depends on client version and configuration.
