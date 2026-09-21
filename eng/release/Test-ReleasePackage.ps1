param(
    [Parameter(Mandatory)][string]$Directory,
    [Parameter(Mandatory)][string]$PackageId,
    [Parameter(Mandatory)][string]$Version
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$packages = @(Get-ChildItem -LiteralPath $Directory -Filter '*.nupkg' -File)
if ($packages.Count -ne 1) {
    throw "Expected exactly one NuGet package, found $($packages.Count)."
}

# 检查包内真实元数据，不能仅凭文件名判断发布版本。
$archive = [System.IO.Compression.ZipFile]::OpenRead($packages[0].FullName)
try {
    $specs = @($archive.Entries | Where-Object { $_.FullName -match '^[^/\\]+\.nuspec$' })
    if ($specs.Count -ne 1) { throw 'Expected exactly one root nuspec in the release package.' }
    $reader = [System.IO.StreamReader]::new($specs[0].Open())
    try { [xml]$spec = $reader.ReadToEnd() }
    finally { $reader.Dispose() }

    $actualId = [string]$spec.package.metadata.id
    $actualVersion = [string]$spec.package.metadata.version
    if ($actualId -cne $PackageId -or $actualVersion -cne $Version) {
        throw "Release package mismatch: expected '$PackageId $Version', found '$actualId $actualVersion'."
    }
    $entries = @($archive.Entries | Where-Object { $_.FullName -match '^tools/[^/]+/any/gallery-manifest.json$' })
    if ($entries.Count -ne 1) { throw 'Expected a Gallery manifest beside the tool executable.' }
    $reader = [IO.StreamReader]::new($entries[0].Open())
    try { $manifest = $reader.ReadToEnd() | ConvertFrom-Json }
    finally { $reader.Dispose() }
    if ($manifest.schemaVersion -ne 1 -or $manifest.version -cne $Version) { throw 'Gallery/tool version mismatch.' }
    if (@($manifest.assets).Count -ne 1) { throw 'Expected one x64 Gallery asset.' }
    foreach ($rid in @('win-x64')) {
        $assets = @($manifest.assets | Where-Object { $_.runtimeIdentifier -ceq $rid })
        if ($assets.Count -ne 1) { throw "Missing or duplicate Gallery architecture: $rid" }
        $asset = $assets[0]
        $name = "XamlNexus Gallery v$Version.zip"
        $expectedUrl = "https://github.com/PaperHammer/XamlNexus/releases/download/v$Version/$([Uri]::EscapeDataString($name))"
        if ($asset.url -cne $expectedUrl -or $asset.sha256 -notmatch '^[a-fA-F0-9]{64}$') { throw 'Invalid Gallery asset metadata.' }
        $path = Join-Path $Directory $name
        if (!(Test-Path -LiteralPath $path) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ine $asset.sha256) {
            throw "Gallery archive does not match the packaged manifest: $name"
        }
    }
}
finally { $archive.Dispose() }

Write-Host "Release package valid: $PackageId $Version"
