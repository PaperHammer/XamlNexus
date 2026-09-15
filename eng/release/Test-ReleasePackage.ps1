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
}
finally { $archive.Dispose() }

Write-Host "Release package valid: $PackageId $Version"
