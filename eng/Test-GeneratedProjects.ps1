# Requires PowerShell 7 and the Windows .NET/WinUI build toolchain.
[CmdletBinding()]
param(
    [ValidateSet('winui', 'hybrid')][string[]]$Preset = @('winui', 'hybrid'),
    [ValidateSet('basic', 'standard')][string]$Profile = 'basic',
    [ValidateSet('sln', 'slnx')][string]$SolutionFormat = 'sln',
    [ValidateRange(1, 60)][int]$TimeoutMinutes = 15
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $IsWindows) { throw 'Generated WinUI projects must be built on Windows.' }
$repo = Split-Path $PSScriptRoot -Parent
$artifacts = Join-Path $repo ('.artifacts/generated-build-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $artifacts | Out-Null
# Keep generated solutions outside the open workspace so IDE project discovery
# cannot race this acceptance build or hold its generated resource files open.
$projects = Join-Path ([IO.Path]::GetTempPath()) ('xamlnexus-build-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $projects | Out-Null
$results = [System.Collections.Generic.List[object]]::new()

function Invoke-Checked([string]$Label, [string[]]$Arguments, [string]$Directory = $repo) {
    Write-Host "[$Label] dotnet $($Arguments -join ' ')"
    $info = [System.Diagnostics.ProcessStartInfo]::new('dotnet')
    $info.WorkingDirectory = $Directory
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $info
    $stdout = $null
    $stderr = $null
    $started = $false
    try {
        $started = $process.Start()
        if (-not $started) { throw "Could not start $Label" }
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $elapsed = [System.Diagnostics.Stopwatch]::StartNew()
        while (-not $process.WaitForExit(1000)) {
            if ($elapsed.Elapsed.TotalMinutes -ge $TimeoutMinutes) {
                throw "$Label exceeded $TimeoutMinutes minutes. See logs in $artifacts."
            }
        }
        if (-not [System.Threading.Tasks.Task]::WaitAll([System.Threading.Tasks.Task[]]@($stdout, $stderr), 10000)) {
            throw "$Label exited but its output streams did not close."
        }
        if ($process.ExitCode -ne 0) { throw "$Label failed (exit $($process.ExitCode)). See logs in $artifacts." }
        return $stdout.Result
    }
    finally {
        if ($started -and -not $process.HasExited) { $process.Kill($true); $process.WaitForExit(10000) | Out-Null }
        foreach ($stream in @(@('stdout', $stdout), @('stderr', $stderr))) {
            if ($null -ne $stream[1] -and $stream[1].IsCompletedSuccessfully) {
                [IO.File]::WriteAllText((Join-Path $artifacts "$Label.$($stream[0]).log"), $stream[1].Result)
            }
        }
        $process.Dispose()
    }
}

function Test-Build([string]$Name, [string]$Root, [string]$Stage) {
    $label = "$Name-$Stage"
    $release = Get-Content -LiteralPath (Join-Path $Root 'eng/publishing/release.json') -Raw | ConvertFrom-Json
    $expectedSolution = "$Name.$SolutionFormat"
    if ([string]$release.solution -cne $expectedSolution) {
        throw "$label release configuration points to '$($release.solution)', expected '$expectedSolution'."
    }
    if (-not (Test-Path -LiteralPath (Join-Path $Root $expectedSolution) -PathType Leaf)) {
        throw "$label release solution does not exist: $expectedSolution"
    }
    Invoke-Checked "$label-solution-list" @('sln', [string]$release.solution, 'list') $Root | Out-Null

    $validation = Invoke-Checked "$label-validate" @($cli, 'validate', '--project', $Root, '--json') | ConvertFrom-Json
    if (-not $validation.isValid) { throw "$label failed project validation." }
    $preview = Invoke-Checked "$label-plan" @($cli, 'run', '--project', $Root, '--dry-run', '--json') | ConvertFrom-Json
    $plan = $preview.plan
    $arguments = @($plan.build.arguments) + @('-p:UseSharedCompilation=false', '-nr:false', "-bl:$artifacts/$label.binlog", '-fl', "-flp:logfile=$artifacts/$label.build.log;verbosity=normal")
    Invoke-Checked "$label-build" $arguments $plan.build.workingDirectory | Out-Null
    $target = (Invoke-Checked "$label-target" @($plan.resolveTarget.arguments) $plan.resolveTarget.workingDirectory).Trim()
    $executable = [IO.Path]::ChangeExtension($target, '.exe')
    if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) { throw "Missing startup executable: $executable" }
    if ($plan.hybrid) {
        $ui = Join-Path (Split-Path $executable -Parent) "Plugins/UI/$Name.UI.exe"
        if (-not (Test-Path -LiteralPath $ui -PathType Leaf)) { throw "Missing host UI plugin: $ui" }
    }
    $results.Add([ordered]@{ project = $Name; profile = $Profile; stage = $Stage; status = 'passed'; executable = $executable })
    ConvertTo-Json -InputObject @($results.ToArray()) -Depth 5 | Set-Content (Join-Path $artifacts 'results.json')
    Write-Host "[$label] PASSED"
}

try {
    Invoke-Checked 'cli-build' @('build', (Join-Path $repo 'src/XamlNexus/XamlNexus.csproj'), '-m:1', '-nr:false', '-p:UseSharedCompilation=false') | Out-Null
    $cli = Join-Path $repo 'src/XamlNexus/bin/Debug/net8.0/XamlNexus.dll'
    foreach ($architecture in $Preset) {
        $name = if ($architecture -eq 'winui') { 'BuildPure' } else { 'BuildHybrid' }
        $root = Join-Path $projects $name
        Invoke-Checked "$name-new" @($cli, 'new', $name, '--preset', $architecture, '--profile', $Profile, '--solution-format', $SolutionFormat, '--output', $projects) | Out-Null
        Test-Build $name $root 'baseline'
        Invoke-Checked "$name-page" @($cli, 'page', 'add', 'Workspace', '--project', $root, '--json') | Out-Null
        $features = if ($Profile -eq 'basic') { 'settings,sqlite' } else { 'sqlite' }
        Invoke-Checked "$name-add" @($cli, 'add', $features, '--project', $root, '--json') | Out-Null
        Test-Build $name $root 'composed'
    }
}
catch {
    $results.Add([ordered]@{ status = 'failed'; error = $_.Exception.Message })
    throw
}
finally {
    ConvertTo-Json -InputObject @($results.ToArray()) -Depth 5 | Set-Content (Join-Path $artifacts 'results.json')
    Write-Host "Build acceptance artifacts: $artifacts"
    Write-Host "Generated projects (retained for inspection): $projects"
}
