param([Parameter(Mandatory)][string]$Version)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Write-GitHubOutput([string]$Name, [string]$Value) {
    if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_OUTPUT)) {
        Add-Content -LiteralPath $env:GITHUB_OUTPUT -Value "$Name=$Value"
    }
}

$root = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
Set-Location $root
$releaseConfigPath = Join-Path $PSScriptRoot "release.json"
if (-not (Test-Path -LiteralPath $releaseConfigPath)) { $releaseConfigPath = Join-Path $root ".github/release.json" }
$config = Get-Content -LiteralPath $releaseConfigPath -Raw | ConvertFrom-Json
$assemblyVersion = if ($Version.Split('.').Count -eq 3) { "$Version.0" } else { $Version }
$artifactRoot = Join-Path $root "artifacts/release"
$publishDirectory = Join-Path $artifactRoot "publish"
if (Test-Path -LiteralPath $artifactRoot) {
    Remove-Item -LiteralPath $artifactRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

& dotnet restore ([string]$config.solution) -r ([string]$config.runtimeIdentifier) -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) { throw "Restore failed." }

$pluginProject = $config.PSObject.Properties["pluginProject"]
if ($null -ne $pluginProject -and -not [string]::IsNullOrWhiteSpace([string]$pluginProject.Value)) {
    & dotnet build ([string]$pluginProject.Value) `
        -c Release `
        -r ([string]$config.runtimeIdentifier) `
        --no-restore `
        -p:Platform=x64 `
        -p:UseSharedCompilation=false `
        -p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) { throw "UI plugin build failed." }
}

& dotnet publish ([string]$config.project) `
    -c Release `
    -r ([string]$config.runtimeIdentifier) `
    --no-restore `
    --self-contained true `
    -o $publishDirectory `
    -p:Platform=x64 `
    -p:PublishProfile= `
    -p:PublishTrimmed=false `
    -p:PublishReadyToRun=false `
    -p:PublishDesktopInstaller=true `
    -p:UseSharedCompilation=false `
    -p:NuGetAudit=false `
    "-p:Version=$Version" `
    "-p:AssemblyVersion=$assemblyVersion" `
    "-p:FileVersion=$assemblyVersion"
if ($LASTEXITCODE -ne 0) { throw "Application publish failed." }

$expectedExecutable = Join-Path $publishDirectory ([string]$config.executable)
if (-not (Test-Path -LiteralPath $expectedExecutable)) {
    throw "Published executable was not found: $expectedExecutable"
}

$iscc = Get-Command ISCC.exe -ErrorAction SilentlyContinue
if ($null -eq $iscc) {
    $iscc = Get-ChildItem -Path "${env:ProgramFiles(x86)}\Inno Setup *\ISCC.exe" -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending |
        Select-Object -First 1
}
if ($null -eq $iscc) { throw "Inno Setup compiler ISCC.exe is not installed." }

$safeName = ([string]$config.appName) -replace '[^0-9A-Za-z._-]', '-'
$installerBaseName = "$safeName-$Version-$($config.runtimeIdentifier)"
$compilerPath = if ($iscc -is [System.Management.Automation.CommandInfo]) { $iscc.Source } else { $iscc.FullName }
& $compilerPath `
    "/DAppName=$($config.appName)" `
    "/DAppVersion=$Version" `
    "/DExecutableName=$($config.executable)" `
    "/DSourceDir=$publishDirectory" `
    "/DOutputDir=$artifactRoot" `
    "/DOutputBaseFilename=$installerBaseName" `
    (Join-Path $PSScriptRoot "installer.iss")
if ($LASTEXITCODE -ne 0) { throw "Installer compilation failed." }

$installerPath = Join-Path $artifactRoot "$installerBaseName.exe"
if (-not (Test-Path -LiteralPath $installerPath)) { throw "Installer output was not found." }

Write-GitHubOutput "installer_path" $installerPath
Write-GitHubOutput "installer_name" (Split-Path -Leaf $installerPath)
Write-Host "Installer created: $installerPath"
