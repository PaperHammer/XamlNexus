# Requires PowerShell 7 and the Windows .NET/WinUI build toolchain.
[CmdletBinding()]
param(
    [switch]$RunGuiSmoke,
    [ValidateRange(1, 60)][int]$TimeoutMinutes = 20,
    [ValidateRange(2, 30)][int]$SmokeSeconds = 5
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $IsWindows) { throw 'XamlNexus package acceptance requires Windows.' }

$repo = Split-Path $PSScriptRoot -Parent
$stamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8)
$artifacts = Join-Path $repo ".artifacts/package-acceptance-$stamp"
$packages = Join-Path $artifacts 'packages'
$tool = Join-Path $artifacts 'tool'
$projects = Join-Path ([IO.Path]::GetTempPath()) "xamlnexus-package-$stamp"
New-Item -ItemType Directory -Path $packages, $tool, $projects -Force | Out-Null
$results = [System.Collections.Generic.List[object]]::new()

function Invoke-Checked([string]$Label, [string]$FileName, [string[]]$Arguments, [string]$Directory = $repo) {
    Write-Host "[$Label] $FileName $($Arguments -join ' ')"
    $info = [Diagnostics.ProcessStartInfo]::new($FileName)
    $info.WorkingDirectory = $Directory
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $info
    $started = $false
    try {
        $started = $process.Start()
        if (-not $started) { throw "Could not start $Label." }
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $elapsed = [Diagnostics.Stopwatch]::StartNew()
        while (-not $process.WaitForExit(1000)) {
            if ($elapsed.Elapsed.TotalMinutes -ge $TimeoutMinutes) { throw "$Label exceeded $TimeoutMinutes minutes." }
        }
        [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($stdout, $stderr))
        [IO.File]::WriteAllText((Join-Path $artifacts "$Label.stdout.log"), $stdout.Result)
        [IO.File]::WriteAllText((Join-Path $artifacts "$Label.stderr.log"), $stderr.Result)
        if ($process.ExitCode -ne 0) {
            throw "$Label failed with exit code $($process.ExitCode): $($stderr.Result.Trim())"
        }
        return $stdout.Result
    }
    finally {
        if ($started -and -not $process.HasExited) { $process.Kill($true); $process.WaitForExit() }
        $process.Dispose()
    }
}

function Test-Runtime([string]$Label, [string]$Executable, [bool]$Hybrid) {
    $info = [Diagnostics.ProcessStartInfo]::new($Executable)
    $info.WorkingDirectory = Split-Path $Executable -Parent
    $info.UseShellExecute = $false
    if ($Hybrid) { $info.ArgumentList.Add('--xamlnexus-run') }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $info
    try {
        if (-not $process.Start()) { throw "Could not start $Label." }
        $deadline = [DateTime]::UtcNow.AddSeconds($SmokeSeconds)
        while ([DateTime]::UtcNow -lt $deadline) {
            if ($process.HasExited) { throw "$Label exited during startup with code $($process.ExitCode)." }
            Start-Sleep -Milliseconds 250
        }
        $results.Add([ordered]@{ stage = 'runtime'; project = $Label; status = 'passed'; seconds = $SmokeSeconds })
    }
    finally {
        if (-not $process.HasExited) { $process.Kill($true); $process.WaitForExit() }
        $process.Dispose()
    }
}

try {
    [xml]$projectFile = Get-Content -LiteralPath (Join-Path $repo 'src/XamlNexus/XamlNexus.csproj') -Raw
    $version = [string]$projectFile.Project.PropertyGroup.Version
    Invoke-Checked 'pack' 'dotnet' @('pack', 'src/XamlNexus/XamlNexus.csproj', '-c', 'Release', '-o', $packages, '-p:ContinuousIntegrationBuild=true', '-p:NuGetAudit=false') | Out-Null
    $escapedPackages = [Security.SecurityElement]::Escape($packages)
    $nugetConfig = Join-Path $artifacts 'NuGet.Config'
    Set-Content -LiteralPath $nugetConfig -Value "<?xml version=`"1.0`" encoding=`"utf-8`"?><configuration><packageSources><clear /><add key=`"packed-tool`" value=`"$escapedPackages`" /></packageSources></configuration>"
    Invoke-Checked 'tool-install' 'dotnet' @('tool', 'install', 'XamlNexus', '--tool-path', $tool, '--configfile', $nugetConfig, '--version', $version) | Out-Null
    $cli = Join-Path $tool 'xamlnexus.exe'
    $installedVersion = (Invoke-Checked 'tool-version' $cli @('--version')).Trim()
    if ($installedVersion -cne $version) { throw "Installed version '$installedVersion' does not match '$version'." }

    foreach ($preset in @('winui', 'hybrid')) {
        $name = if ($preset -eq 'winui') { 'PackagePure' } else { 'PackageHybrid' }
        $root = Join-Path $projects $name
        Invoke-Checked "$name-new" $cli @('new', $name, '--preset', $preset, '--profile', 'basic', '--output', $projects) | Out-Null
        Invoke-Checked "$name-details" $cli @('page', 'add', 'OrderDetails', '--kind', 'details', '--project', $root, '--json') | Out-Null
        Invoke-Checked "$name-form" $cli @('page', 'add', 'OrderEditor', '--kind', 'form', '--project', $root, '--json') | Out-Null
        Invoke-Checked "$name-add" $cli @('add', 'settings,sqlite', '--project', $root, '--json') | Out-Null
        $validation = Invoke-Checked "$name-validate" $cli @('validate', '--project', $root, '--json') | ConvertFrom-Json
        if (-not $validation.isValid) { throw "$name failed validation." }
        $status = Invoke-Checked "$name-status" $cli @('status', '--project', $root, '--json') | ConvertFrom-Json
        if ($status.status -ne 'healthy' -or $status.scaffoldState -ne 'current') { throw "$name status is not healthy/current." }
        $updates = Invoke-Checked "$name-update-all" $cli @('update', '--all', '--project', $root, '--dry-run', '--json') | ConvertFrom-Json
        if ($updates.status -ne 'upToDate' -or -not $updates.all) { throw "$name update --all did not report the expected up-to-date state." }
        $plan = (Invoke-Checked "$name-plan" $cli @('run', '--project', $root, '--dry-run', '--json') | ConvertFrom-Json).plan
        Invoke-Checked "$name-build" 'dotnet' @($plan.build.arguments) $plan.build.workingDirectory | Out-Null
        $target = (Invoke-Checked "$name-target" 'dotnet' @($plan.resolveTarget.arguments) $plan.resolveTarget.workingDirectory).Trim()
        $executable = [IO.Path]::ChangeExtension($target, '.exe')
        if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) { throw "Missing startup executable: $executable" }
        $results.Add([ordered]@{ stage = 'package'; project = $name; preset = $preset; status = 'passed'; executable = $executable })
        if ($RunGuiSmoke) { Test-Runtime $name $executable ($preset -eq 'hybrid') }
    }
}
catch {
    $results.Add([ordered]@{ stage = 'acceptance'; status = 'failed'; error = $_.Exception.Message })
    throw
}
finally {
    ConvertTo-Json -InputObject @($results.ToArray()) -Depth 5 | Set-Content -LiteralPath (Join-Path $artifacts 'results.json')
    if (Test-Path -LiteralPath $projects) { Remove-Item -LiteralPath $projects -Recurse -Force }
    Write-Host "Package acceptance artifacts: $artifacts"
}
