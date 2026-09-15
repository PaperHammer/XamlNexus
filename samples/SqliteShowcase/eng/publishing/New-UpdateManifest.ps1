param(
    [Parameter(Mandatory)][ValidateSet("stable", "preview")][string]$Channel,
    [Parameter(Mandatory)][string]$Version,
    [Parameter(Mandatory)][string]$InstallerPath,
    [Parameter(Mandatory)][string]$NotesPath,
    [string]$ExistingManifestPath
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
if ([string]::IsNullOrWhiteSpace($env:GITHUB_REPOSITORY)) {
    throw "GITHUB_REPOSITORY is required."
}

$installer = Get-Item -LiteralPath $InstallerPath
$hashPath = "$($installer.FullName).sha256"
$hash = (Get-FileHash -LiteralPath $installer.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath $hashPath -Value "$hash  $($installer.Name)" -Encoding utf8

$manifest = [ordered]@{ stable = $null; preview = $null }
if (-not [string]::IsNullOrWhiteSpace($ExistingManifestPath) -and
    (Test-Path -LiteralPath $ExistingManifestPath)) {
    $existing = Get-Content -LiteralPath $ExistingManifestPath -Raw | ConvertFrom-Json
    $manifest.stable = $existing.stable
    $manifest.preview = $existing.preview
}

$escapedInstallerName = [Uri]::EscapeDataString($installer.Name)
$hashName = "$($installer.Name).sha256"
$escapedHashName = [Uri]::EscapeDataString($hashName)
$tag = "v$Version"
$baseUrl = "https://github.com/$env:GITHUB_REPOSITORY/releases/download/$tag"
$release = [ordered]@{
    version = $Version
    downloadUrl = "$baseUrl/$escapedInstallerName"
    sha256Url = "$baseUrl/$escapedHashName"
    changelog = (Get-Content -LiteralPath $NotesPath -Raw).Trim()
}
$manifest[$Channel] = $release

$manifestPath = Join-Path $installer.Directory.FullName "update-manifest.json"
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding utf8

if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_OUTPUT)) {
    Add-Content -LiteralPath $env:GITHUB_OUTPUT -Value "hash_path=$hashPath"
    Add-Content -LiteralPath $env:GITHUB_OUTPUT -Value "manifest_path=$manifestPath"
}
Write-Host "Update manifest generated: $manifestPath"
