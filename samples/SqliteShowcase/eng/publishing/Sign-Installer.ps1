param([Parameter(Mandatory)][string]$InstallerPath)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$root = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$config = Get-Content -LiteralPath (Join-Path $root ".github/release.json") -Raw | ConvertFrom-Json
if (-not [bool]$config.requireSigning) {
    Write-Warning "Installer signing is disabled by release configuration."
    return
}
if ([string]::IsNullOrWhiteSpace($env:WINDOWS_SIGNING_PFX_BASE64) -or
    [string]::IsNullOrWhiteSpace($env:WINDOWS_SIGNING_PASSWORD)) {
    throw "WINDOWS_SIGNING_PFX_BASE64 and WINDOWS_SIGNING_PASSWORD are required."
}

$signTool = Get-ChildItem -Path "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" |
    Sort-Object FullName -Descending |
    Select-Object -First 1
if ($null -eq $signTool) { throw "Windows SDK signtool.exe was not found." }

$temporaryRoot = if ([string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)) {
    [IO.Path]::GetTempPath()
} else {
    $env:RUNNER_TEMP
}
$certificatePath = Join-Path $temporaryRoot "xamlnexus-signing-$([Guid]::NewGuid().ToString('N')).pfx"
try {
    [IO.File]::WriteAllBytes(
        $certificatePath,
        [Convert]::FromBase64String($env:WINDOWS_SIGNING_PFX_BASE64))
    & $signTool.FullName sign `
        /fd SHA256 `
        /td SHA256 `
        /tr ([string]$config.timestampUrl) `
        /f $certificatePath `
        /p $env:WINDOWS_SIGNING_PASSWORD `
        $InstallerPath
    if ($LASTEXITCODE -ne 0) { throw "Authenticode signing failed." }

    & $signTool.FullName verify /pa $InstallerPath
    if ($LASTEXITCODE -ne 0) { throw "Authenticode signature verification failed." }
}
finally {
    if (Test-Path -LiteralPath $certificatePath) {
        Remove-Item -LiteralPath $certificatePath -Force
    }
}
