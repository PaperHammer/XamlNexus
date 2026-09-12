# XamlNexus release policy

Releases are driven only by pull requests merged into `main`. Pushes to feature branches and direct pushes to `main` do not trigger the release workflow.

## Repository setup

Create these labels:

- `release:stable`
- `release:preview`
- `release:none`

Add a `NUGET_API_KEY` Actions secret with permission to publish the package named in `.github/release.json`.

Protect `main` and require:

- changes to arrive through pull requests;
- the `Validate pull request` status check;
- resolved review conversations;
- no force pushes or branch deletion.

The optional GitHub `production` environment can be added later if releases should require a manual approval. The default workflow publishes automatically after merge.

## Publishing pull requests

1. Increase `Version`, `AssemblyVersion`, and `PackageVersion` in `src/XamlNexus/XamlNexus.csproj`.
2. Apply exactly one release label.
3. Put user-visible changes between the release-note markers in the pull request template.
4. Merge the pull request into `main` after validation succeeds.

Stable labels require a stable semantic version such as `1.2.0`. Preview labels require a prerelease version such as `1.3.0-preview.1`.

The merged workflow checks out the exact merge commit, rebuilds and tests the solution, creates the .NET tool package, pushes it to NuGet, and creates the corresponding GitHub Release. Existing tags are reused only when they already point to the same merge commit, which makes a partially failed release safe to rerun without moving a published tag.

## Non-publishing pull requests

Use `release:none`. The project version does not need to change, and the merged workflow exits without accessing publishing secrets.
