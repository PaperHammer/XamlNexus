# Application release setup

This generated project publishes only when a pull request is merged into `main`. Branch pushes, direct pushes, and closed-but-unmerged pull requests do not publish.

## One-time repository setup

1. Create the labels `release:stable`, `release:preview`, and `release:none`.
2. Protect `main`, require pull requests, and require the `Validate pull request` check.
3. Obtain an Authenticode code-signing certificate exported as PFX.
4. Base64-encode the PFX and create these Actions secrets:
   - `WINDOWS_SIGNING_PFX_BASE64`
   - `WINDOWS_SIGNING_PASSWORD`
5. Replace `Consts.Updates.ManifestUrl` with:

   `https://github.com/OWNER/REPOSITORY/releases/download/update-feed/update-manifest.json`

The release workflow validates this URL against the repository executing the workflow. Signing is required by default. It may be disabled in `.github/release.json` only for internal development builds; unsigned public installers are not recommended.

## Publishing

For a publishing pull request:

1. Increase the numeric `Version` in `Directory.Build.props`.
2. Keep `AssemblyVersion` and `FileVersion` aligned with it.
3. Apply either `release:stable` or `release:preview`.
4. Fill the release-note section in the pull request template.
5. Merge into `main` after validation succeeds.

Desktop update versions contain only three or four numeric components because the runtime updater compares `System.Version` values. Every preview and stable release must therefore receive a unique, increasing numeric version. For example, publish preview `1.2.0`, then stable `1.2.1`.

The workflow publishes a self-contained `win-x64` application, builds an Inno Setup installer, signs it, verifies the signature, calculates SHA-256, creates an immutable version Release, and finally replaces `update-manifest.json` in the permanent `update-feed` Release.

Use `release:none` for merges that should not publish. No signing secrets are read in that path.
