# PR workflows and check settings

[English](pr-checks.md) | [简体中文](pr-checks.zh-CN.md)

This guide covers **newly generated application projects**. The XamlNexus repository has its own release workflow. For older projects, inspect their `.github/workflows`; new templates do not automatically replace existing files.

## What a PR triggers

Generated projects include `.github/workflows/validate-pull-request.yml`, named `Validate pull request`, with job ID `validate`. It runs for PRs targeting `main` on:

- `opened`, including draft PRs;
- `synchronize`, when new commits are pushed to the PR branch;
- `reopened`;
- `ready_for_review`, when a draft becomes ready for review.

Label or description edits, direct pushes to `main`, and closing or merging a PR do not independently trigger it. There is no manual dispatch trigger. See the [workflow template](../../src/Templates/Shared/.github/workflows/validate-pull-request.yml).

A Windows runner installs .NET 8 and 10, reads `solution` from `eng/publishing/release.json`, and runs these steps in order:

| Step | Scope |
|---|---|
| Restore | Restore dependencies |
| Build | Compile the solution in Release/x64 |
| Test | Run solution tests; meaningful coverage depends on the user's test projects |

Later steps normally do not run after a failure. There are no GUI, installer execution, signature, or mandatory formatting checks. Commands explicitly set `NuGetAudit=false`, disabling NuGet vulnerability auditing.

The workflow does not publish packages or installers, create Releases, or change versions. It does not require `release:*` labels or release notes. The PR template only asks for a summary and validation details. CI does not invoke `xamlnexus validate` or `doctor` either.

## Enable or disable CI execution

Matching events run the workflow when the repository allows GitHub Actions and the workflow is enabled. Use **Actions → Validate pull request → menu → Disable workflow / Enable workflow**, or an authenticated GitHub CLI with the required permissions:

```powershell
gh workflow disable validate-pull-request.yml --repo OWNER/REPO
gh workflow enable validate-pull-request.yml --repo OWNER/REPO
```

Disabling keeps the YAML file. After enabling it, trigger a matching event, such as pushing a new PR commit. [GitHub: disable and enable workflows](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/disable-and-enable-workflows)

## Blocking merges is a separate setting

The generator does not configure GitHub branch protection or rulesets. To require passing checks before merging:

1. Run PR validation once.
2. In repository Settings, use Rules / Rulesets or Branches protection to require status checks for `main`.
3. Select the actual `validate` check reported by the workflow and save. Requiring the branch to be up to date is a separate choice.

To keep CI running without requiring its result for merging, remove that required check from the rule; keep the workflow. Other review or organization rules may still restrict merging. Changing these settings requires appropriate permissions, and availability depends on the repository and GitHub plan. [GitHub: protected branches](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches)

Before disabling or deleting a workflow, check whether its result is required. Skipping an entire workflow with path filters or commit messages can leave required checks Pending and block merging. If you enable a merge queue, add the `merge_group` trigger; the current template does not include it. [GitHub: troubleshoot required checks](https://docs.github.com/en/pull-requests/how-tos/merge-and-close-pull-requests/troubleshooting-required-status-checks)

## Adjust the checks

Edit the generated YAML directly:

| Requirement | Change |
|---|---|
| Check other target branches | Change `pull_request.branches`, for example to `[main, develop]` |
| Skip the job for drafts | Add `if: github.event.pull_request.draft == false` under `jobs.validate`; drafts currently run |
| Temporarily omit tests | Remove or comment out the Test step; restore it to enable tests |
| Enable vulnerability auditing | Change Restore's `NuGetAudit=false` to `true`; whether audit warnings fail CI also depends on NuGet/MSBuild settings |
| Check formatting | Add a formatting step; none is included by default |
| Use a different solution | Update `solution` in `eng/publishing/release.json`, or supply a path directly in the workflow |

Do not remove Build while retaining Test with `--no-build`: tests depend on its output. Check required status settings when renaming jobs or changing triggers.

## CLI validation and file protection

Local command rules operate independently of GitHub Actions:

| Category | Behavior and controls |
|---|---|
| `validate` / `doctor` | Run explicitly to request a report; other mutation commands still enforce their own preconditions |
| Creation arguments | Names, architectures, languages, and solution formats must be valid; no general off switch |
| Component dependencies and conflicts | `add` checks compatibility, dependencies, and duplicate installation; disabling CI does not bypass them |
| Customized source | Editing is allowed; content differences normally warn, while missing tracked files are errors |
| Automatic update and removal | Unexpected hashes or missing files stop the operation to protect edits; no `--force` bypass |
| Scaffold merging | Unreliable merges report conflicts; resolve them through the upgrade process |
| Paths and concurrency | Escaping paths, symbolic links, duplicate targets, stale state, and concurrent writers are rejected; no off switch |
| Page navigation | `page add Orders --no-navigation` skips automatic navigation registration |
| Building | `run --no-build` uses existing output without compiling recent edits |

`--dry-run` previews operations without bypassing validation. `--json` only changes output formatting. Do not edit hashes to bypass user-change protection. See the [command reference](commands.md), [diagnostics](doctor.md), and [project upgrades](project-upgrade.md).
