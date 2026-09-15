# XamlNexus release policy

Releases are driven by pull requests merged into `main`. The workflow uses a `push` trigger because NuGet Trusted Publishing rejects `pull_request_target`. It resolves the merged PR associated with the pushed commit and reads its labels and description. Direct pushes without a matching merged PR skip publishing; feature branch pushes do not trigger the workflow.

## Repository setup

Create these labels:

- `release:stable`
- `release:preview`
- `release:none`
- `release:retry` (supplemental label for retrying an unpublished version)

Configure [NuGet Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing):

1. On nuget.org, create a trusted publishing policy for repository owner `PaperHammer`, repository `XamlNexus`, and workflow file `release-merged-pull-request.yml` (filename only).
2. Set the policy environment to `production`, matching the release job's GitHub environment. Grant publishing permission for the `XamlNexus` package to the appropriate package owner.
3. In GitHub Settings > Secrets and variables > Actions, add a repository secret named `NUGET_USER` containing your nuget.org username (profile name, not email address).

The release job requests an OIDC token and uses `NuGet/login@v1` to obtain a temporary API key immediately before uploading. A stored `NUGET_API_KEY` is no longer required. Keep the environment name consistent between the job and the NuGet policy.

Protect `main` and require:

- changes to arrive through pull requests;
- the `Validate pull request` status check;
- resolved review conversations;
- no force pushes or branch deletion.

The release job uses the GitHub `production` environment. Configure its protection rules under Settings > Environments; required reviewers are optional and will pause the job for approval if enabled. Publishing-labeled PRs publish after merge and any required environment approval; PRs without a release label do not publish.

## Publishing pull requests

1. Set `Version`, `AssemblyVersion`, and `PackageVersion` in `src/XamlNexus/XamlNexus.csproj` for the release. Normally the version must exceed the base branch version. If that version was merged but publishing never completed, add `release:retry` to permit the same version. Downgrades are always rejected. Existing tags block new publishing PRs; only rerunning the release for the same commit may reuse a tag.
2. Apply exactly one publishing channel: `release:stable` or `release:preview`. The optional `release:retry` label must accompany one of these; it cannot be used alone or with `release:none`.
3. Write user-visible changes in the PR description. The entire description becomes the GitHub Release body; no special markers are required. Put review-only details in PR comments instead.
4. Merge the pull request into `main` after validation succeeds.

Stable labels require a stable semantic version such as `1.2.0`. Preview labels require a prerelease version such as `1.3.0-preview.1`.

The merged workflow checks out the exact merge commit, rebuilds and tests the solution, creates the .NET tool package, pushes it to NuGet, and creates the corresponding GitHub Release. Existing tags are reused only when they already point to the same merge commit, which makes a partially failed release safe to rerun without moving a published tag.

## Non-publishing pull requests

Omit release labels or use `release:none`. Other labels (such as `bug`) do not trigger publishing. The project version does not need to change, and the merged workflow exits without accessing publishing secrets.
