param(
    [ValidateSet("Validate", "Release")]
    [string]$Mode = "Validate"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Get-AppVersion([string]$Revision, [string]$VersionFile) {
    if ([string]::IsNullOrWhiteSpace($Revision)) {
        $content = Get-Content -LiteralPath $VersionFile -Raw
    }
    else {
        $gitPath = $VersionFile.Replace("\", "/")
        $content = (& git show "${Revision}:$gitPath") -join "`n"
        if ($LASTEXITCODE -ne 0) { throw "Unable to read $VersionFile at revision $Revision." }
    }

    [xml]$project = $content
    $versionNode = @($project.Project.PropertyGroup.Version) |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -First 1
    if ($null -eq $versionNode) { throw "$VersionFile must declare a Version property." }

    $version = ([string]$versionNode).Trim()
    if ($version -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:\.(0|[1-9]\d*))?$') {
        throw "Desktop update version '$version' must contain three or four numeric components."
    }
    return $version
}

function Compare-NumericVersion([string]$Left, [string]$Right) {
    $leftVersion = [Version]$Left
    $rightVersion = [Version]$Right
    return $leftVersion.CompareTo($rightVersion)
}

function Write-GitHubOutput([string]$Name, [string]$Value) {
    if ([string]::IsNullOrWhiteSpace($env:GITHUB_OUTPUT)) { return }
    Add-Content -LiteralPath $env:GITHUB_OUTPUT -Value "$Name=$Value"
}

$root = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
Set-Location $root
$config = Get-Content -LiteralPath ".github/release.json" -Raw | ConvertFrom-Json
$event = Get-Content -LiteralPath $env:GITHUB_EVENT_PATH -Raw | ConvertFrom-Json
if ($null -eq $event.pull_request) { throw "This policy only supports pull request events." }

$labels = @($event.pull_request.labels | ForEach-Object { [string]$_.name } |
    Where-Object { $_ -in @("release:stable", "release:preview", "release:none") })
if ($labels.Count -ne 1) {
    throw "Apply exactly one release label: release:stable, release:preview, or release:none."
}

$label = $labels[0]
$shouldPublish = $label -ne "release:none"
$channel = if ($label -eq "release:preview") { "preview" } elseif ($shouldPublish) { "stable" } else { "none" }
$version = Get-AppVersion "" ([string]$config.versionFile)
$body = [string]$event.pull_request.body
$notesMatch = [regex]::Match($body, '(?s)<!--\s*release-notes:start\s*-->(.*?)<!--\s*release-notes:end\s*-->')
$notes = if ($notesMatch.Success) { $notesMatch.Groups[1].Value.Trim() } else { "" }

if ($shouldPublish) {
    if ([string]::IsNullOrWhiteSpace($notes)) {
        throw "Publishing pull requests must include release notes between the release-note markers."
    }
    $baseVersion = Get-AppVersion ([string]$event.pull_request.base.sha) ([string]$config.versionFile)
    if ((Compare-NumericVersion $version $baseVersion) -le 0) {
        throw "Release version '$version' must be greater than base version '$baseVersion'."
    }

    if ([string]::IsNullOrWhiteSpace($env:GITHUB_REPOSITORY)) {
        throw "GITHUB_REPOSITORY is required to validate the update feed URL."
    }
    $expectedManifestUrl = "https://github.com/$env:GITHUB_REPOSITORY/releases/download/update-feed/update-manifest.json"
    $manifestSource = Get-Content -LiteralPath ([string]$config.manifestSource) -Raw
    if (-not $manifestSource.Contains($expectedManifestUrl, [StringComparison]::Ordinal)) {
        throw "Configure Consts.Updates.ManifestUrl as '$expectedManifestUrl' before publishing."
    }
}

$tag = if ($shouldPublish) { "$($config.tagPrefix)$version" } else { "" }
if ($shouldPublish -and $Mode -eq "Release") {
    $existingTag = ((& git tag --list $tag) -join "").Trim()
    if (-not [string]::IsNullOrWhiteSpace($existingTag)) {
        $tagCommit = ((& git rev-list -n 1 $tag) -join "").Trim()
        $mergeCommit = [string]$event.pull_request.merge_commit_sha
        if ([string]::IsNullOrWhiteSpace($mergeCommit) -or $tagCommit -ne $mergeCommit) {
            throw "Tag '$tag' already points to another commit."
        }
    }
}

$notesPath = Join-Path $root ".release-notes.md"
Set-Content -LiteralPath $notesPath -Value $notes
Write-GitHubOutput "should_publish" $shouldPublish.ToString().ToLowerInvariant()
Write-GitHubOutput "channel" $channel
Write-GitHubOutput "version" $version
Write-GitHubOutput "tag" $tag
Write-GitHubOutput "notes_path" $notesPath
Write-GitHubOutput "app_name" ([string]$config.appName)
Write-Host "Release policy valid: label=$label version=$version channel=$channel publish=$shouldPublish"
