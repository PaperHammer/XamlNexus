param(
    [ValidateSet("Validate", "Release")]
    [string]$Mode = "Validate"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Get-ProjectVersion([string]$Revision, [string]$ProjectPath) {
    if ([string]::IsNullOrWhiteSpace($Revision)) {
        $content = Get-Content -LiteralPath $ProjectPath -Raw
    }
    else {
        $gitPath = $ProjectPath.Replace("\", "/")
        $content = (& git show "${Revision}:$gitPath") -join "`n"
        if ($LASTEXITCODE -ne 0) {
            throw "Unable to read $ProjectPath at revision $Revision."
        }
    }

    # git show 会保留文件开头的 BOM；字符串转 XML 前需要移除它。
    [xml]$project = $content.TrimStart([char]0xFEFF)
    $versionNode = @($project.Project.PropertyGroup.Version) |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -First 1
    if ($null -eq $versionNode) {
        throw "The project must declare a Version property: $ProjectPath"
    }

    return ([string]$versionNode).Trim()
}

function ConvertFrom-SemVer([string]$Version) {
    if ($Version -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-([0-9A-Za-z.-]+))?(?:\+[0-9A-Za-z.-]+)?$') {
        throw "Version '$Version' is not a supported semantic version."
    }

    return [pscustomobject]@{
        Major = [int]$Matches[1]
        Minor = [int]$Matches[2]
        Patch = [int]$Matches[3]
        Prerelease = [string]$Matches[4]
    }
}

function Compare-SemVer([string]$Left, [string]$Right) {
    $leftVersion = ConvertFrom-SemVer $Left
    $rightVersion = ConvertFrom-SemVer $Right
    foreach ($component in @("Major", "Minor", "Patch")) {
        if ($leftVersion.$component -gt $rightVersion.$component) { return 1 }
        if ($leftVersion.$component -lt $rightVersion.$component) { return -1 }
    }

    if ([string]::IsNullOrEmpty($leftVersion.Prerelease) -and -not [string]::IsNullOrEmpty($rightVersion.Prerelease)) { return 1 }
    if (-not [string]::IsNullOrEmpty($leftVersion.Prerelease) -and [string]::IsNullOrEmpty($rightVersion.Prerelease)) { return -1 }

    $leftIdentifiers = $leftVersion.Prerelease.Split('.')
    $rightIdentifiers = $rightVersion.Prerelease.Split('.')
    $count = [Math]::Min($leftIdentifiers.Count, $rightIdentifiers.Count)
    for ($index = 0; $index -lt $count; $index++) {
        $leftNumber = 0L
        $rightNumber = 0L
        $leftIsNumber = [long]::TryParse($leftIdentifiers[$index], [ref]$leftNumber)
        $rightIsNumber = [long]::TryParse($rightIdentifiers[$index], [ref]$rightNumber)
        if ($leftIsNumber -and $rightIsNumber) {
            if ($leftNumber -gt $rightNumber) { return 1 }
            if ($leftNumber -lt $rightNumber) { return -1 }
            continue
        }
        if ($leftIsNumber -and -not $rightIsNumber) { return -1 }
        if (-not $leftIsNumber -and $rightIsNumber) { return 1 }

        $comparison = [string]::CompareOrdinal($leftIdentifiers[$index], $rightIdentifiers[$index])
        if ($comparison -ne 0) { return $comparison }
    }

    return $leftIdentifiers.Count.CompareTo($rightIdentifiers.Count)
}

function Write-GitHubOutput([string]$Name, [string]$Value) {
    if ([string]::IsNullOrWhiteSpace($env:GITHUB_OUTPUT)) { return }

    if ($Value.Contains("`n")) {
        $delimiter = "XAMLNEXUS_$([Guid]::NewGuid().ToString('N'))"
        Add-Content -LiteralPath $env:GITHUB_OUTPUT -Value "$Name<<$delimiter"
        Add-Content -LiteralPath $env:GITHUB_OUTPUT -Value $Value
        Add-Content -LiteralPath $env:GITHUB_OUTPUT -Value $delimiter
    }
    else {
        Add-Content -LiteralPath $env:GITHUB_OUTPUT -Value "$Name=$Value"
    }
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
Set-Location $repositoryRoot

$config = Get-Content -LiteralPath ".github/release.json" -Raw | ConvertFrom-Json
$event = Get-Content -LiteralPath $env:GITHUB_EVENT_PATH -Raw | ConvertFrom-Json
if ($null -eq $event.pull_request) {
    throw "This release policy only supports pull_request events."
}

$releaseLabels = @($event.pull_request.labels |
    ForEach-Object { [string]$_.name } |
    Where-Object { $_ -in @("release:stable", "release:preview", "release:none") })
if ($releaseLabels.Count -ne 1) {
    throw "Apply exactly one release label: release:stable, release:preview, or release:none."
}

$releaseLabel = $releaseLabels[0]
$shouldPublish = $releaseLabel -ne "release:none"
$channel = if ($releaseLabel -eq "release:preview") { "preview" } elseif ($shouldPublish) { "stable" } else { "none" }
$version = Get-ProjectVersion "" ([string]$config.project)
$parsedVersion = ConvertFrom-SemVer $version

# 与 VirtualPaper 一致：整份 PR 描述直接作为 Release 正文，不截取标记区间。
$releaseNotes = [string]$event.pull_request.body

if ($shouldPublish) {
    if ([string]::IsNullOrWhiteSpace($releaseNotes)) {
        throw "Publishing pull requests must have a non-empty description for the release notes."
    }
    if ($channel -eq "stable" -and -not [string]::IsNullOrEmpty($parsedVersion.Prerelease)) {
        throw "A stable release cannot use prerelease version '$version'."
    }
    if ($channel -eq "preview" -and [string]::IsNullOrEmpty($parsedVersion.Prerelease)) {
        throw "A preview release must use a prerelease version such as 1.2.0-preview.1."
    }

    $baseVersion = Get-ProjectVersion ([string]$event.pull_request.base.sha) ([string]$config.project)
    if ((Compare-SemVer $version $baseVersion) -le 0) {
        throw "Release version '$version' must be greater than base version '$baseVersion'."
    }

    $tag = "$($config.tagPrefix)$version"
    if ($Mode -eq "Release") {
        $existingTag = & git tag --list $tag
        if (-not [string]::IsNullOrWhiteSpace(($existingTag -join ""))) {
            $tagCommit = ((& git rev-list -n 1 $tag) -join "").Trim()
            $mergeCommit = [string]$event.pull_request.merge_commit_sha
            if ([string]::IsNullOrWhiteSpace($mergeCommit) -or $tagCommit -ne $mergeCommit) {
                throw "Tag '$tag' already points to another commit. Refusing to replace it."
            }
        }
    }
}
else {
    $tag = ""
}

$notesPath = Join-Path $repositoryRoot ".release-notes.md"
Set-Content -LiteralPath $notesPath -Value $releaseNotes

Write-GitHubOutput "should_publish" $shouldPublish.ToString().ToLowerInvariant()
Write-GitHubOutput "channel" $channel
Write-GitHubOutput "version" $version
Write-GitHubOutput "tag" $tag
Write-GitHubOutput "project" ([string]$config.project)
Write-GitHubOutput "solution" ([string]$config.solution)
Write-GitHubOutput "package_id" ([string]$config.packageId)
Write-GitHubOutput "notes_path" $notesPath

Write-Host "Release policy valid: label=$releaseLabel version=$version channel=$channel publish=$shouldPublish"
