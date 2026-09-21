param(
    [Parameter(Mandatory)][string]$Directory,
    [Parameter(Mandatory)][string]$Version,
    [Parameter(Mandatory)][string]$Tag,
    [Parameter(Mandatory)][string]$OutputPath
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($Version -notmatch '^\d+\.\d+\.\d+(-[A-Za-z0-9.-]+)?$' -or $Tag -cne "v$Version") {
    throw 'Gallery requires the same version and v-prefixed tag as the tool.'
}
$assets = foreach ($rid in @('win-x64')) {
    $name = "XamlNexus Gallery v$Version.zip"
    $path = Join-Path $Directory $name
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing Gallery asset: $name" }
    @{
        runtimeIdentifier = $rid
        url = "https://github.com/PaperHammer/XamlNexus/releases/download/$Tag/$([Uri]::EscapeDataString($name))"
        sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}
$manifest = @{ schemaVersion = 1; version = $Version; assets = @($assets) } | ConvertTo-Json -Depth 5
[IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath), $manifest, [Text.UTF8Encoding]::new($false))
Write-Host "Gallery manifest: $OutputPath"
