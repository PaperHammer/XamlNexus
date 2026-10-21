# Headless Windows App SDK identity/redirection probe. No windows or input automation.
[CmdletBinding()]
param(
    [ValidateRange(1, 10)][int]$Repetitions = 2,
    [string]$SdkVersion = '',
    [switch]$SkipBuild,
    [switch]$RawSdk
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $IsWindows) { throw 'AppInstance requires Windows.' }
$repo = Split-Path $PSScriptRoot -Parent
$project = Join-Path $PSScriptRoot 'diagnostics/AppInstanceProbe/AppInstanceProbe.csproj'
if (-not $SdkVersion) {
    [xml]$template = Get-Content -LiteralPath (Join-Path $repo 'src/Templates/Winui3/Winui3_XamlNexus.UI/Winui3_XamlNexus.UI.csproj') -Raw
    $SdkVersion = [string]$template.SelectSingleNode("//PackageReference[@Include='Microsoft.WindowsAppSDK']").Version
}
$artifacts = Join-Path $repo ('.artifacts/app-instance-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $artifacts | Out-Null
if (-not $SkipBuild) {
    & dotnet build $project -c Release -m:1 -nr:false -p:UseSharedCompilation=false "-p:WindowsAppSdkVersion=$SdkVersion" *> (Join-Path $artifacts 'build.log')
    if ($LASTEXITCODE -ne 0) { throw "Probe build failed. See $artifacts/build.log" }
}
$exe = Join-Path $PSScriptRoot 'diagnostics/AppInstanceProbe/bin/Release/net8.0-windows10.0.19041.0/win-x64/AppInstanceProbe.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw "Probe executable not found: $exe" }
$workingDirectory = Split-Path $exe -Parent
$cases = @(
    @{ Name = 'same-path'; Owner = $exe; Secondary = $exe },
    @{ Name = 'mixed-to-lower'; Owner = $exe; Secondary = $exe.ToLowerInvariant() },
    @{ Name = 'lower-to-mixed'; Owner = $exe.ToLowerInvariant(); Secondary = $exe },
    @{ Name = 'mixed-to-upper'; Owner = $exe; Secondary = $exe.ToUpperInvariant() },
    @{ Name = 'upper-to-mixed'; Owner = $exe.ToUpperInvariant(); Secondary = $exe }
)

function Start-Probe([string]$Path, [string[]]$Arguments) {
    $info = [Diagnostics.ProcessStartInfo]::new($Path)
    $info.WorkingDirectory = $workingDirectory
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $info
    if (-not $process.Start()) { throw 'Could not start probe.' }
    return @{ Process = $process; Stdout = $process.StandardOutput.ReadToEndAsync(); Stderr = $process.StandardError.ReadToEndAsync() }
}

function Finish-Probe($Child, [string]$LogPrefix) {
    if (-not $Child.Process.HasExited) { $Child.Process.Kill($true); $Child.Process.WaitForExit(5000) | Out-Null }
    foreach ($stream in @(@('stdout', $Child.Stdout), @('stderr', $Child.Stderr))) {
        if ($stream[1].Wait(5000)) { [IO.File]::WriteAllText($LogPrefix + '.' + $stream[0] + '.log', $stream[1].Result) }
    }
    $Child.Process.Dispose()
}

$results = [Collections.Generic.List[object]]::new()
for ($iteration = 1; $iteration -le $Repetitions; $iteration++) {
    foreach ($case in $cases) {
        $directory = Join-Path $artifacts ($case.Name + '-' + $iteration)
        New-Item -ItemType Directory -Path $directory | Out-Null
        $key = 'XamlNexus.HeadlessProbe.' + [guid]::NewGuid().ToString('N')
        $owner = $null; $secondary = $null
        try {
            $rawArguments = @(); if ($RawSdk) { $rawArguments = @('--raw-sdk') }
            $owner = Start-Probe $case.Owner (@('owner', $key, $directory) + $rawArguments)
            $timer = [Diagnostics.Stopwatch]::StartNew()
            while (-not (Test-Path -LiteralPath (Join-Path $directory 'ready'))) {
                if ($owner.Process.HasExited -or $timer.Elapsed.TotalSeconds -gt 15) { throw 'Owner failed to become ready.' }
                Start-Sleep -Milliseconds 50
            }
            $marker = 'activation-' + [guid]::NewGuid().ToString('N')
            $businessData = @{ id = '订单 ' + $marker; title = 'A "quoted" title'; path = 'C:\business files\draft.json' } | ConvertTo-Json -Compress
            $secondary = Start-Probe $case.Secondary (@('secondary', $key, $directory, $marker, 'argument with spaces', '--route', 'probe/orders', '--data', $businessData) + $rawArguments)
            if (-not $secondary.Process.WaitForExit(20000)) { throw 'Secondary activation timed out.' }
            $ownerData = Get-Content -LiteralPath (Join-Path $directory 'owner.json') -Raw | ConvertFrom-Json
            $secondaryData = Get-Content -LiteralPath (Join-Path $directory 'secondary.json') -Raw | ConvertFrom-Json
            $eventPath = Join-Path $directory 'activations.jsonl'
            $delivery = [Diagnostics.Stopwatch]::StartNew()
            while (-not (Test-Path -LiteralPath $eventPath) -and $secondary.Process.ExitCode -eq 0 -and $delivery.Elapsed.TotalSeconds -lt 5) { Start-Sleep -Milliseconds 50 }
            $events = @(if (Test-Path -LiteralPath $eventPath) { Get-Content -LiteralPath $eventPath | ForEach-Object { $_ | ConvertFrom-Json } })
            $sameContext = $ownerData.userSid -eq $secondaryData.userSid -and $ownerData.elevated -eq $secondaryData.elevated -and $ownerData.workingDirectory -ceq $secondaryData.workingDirectory -and (($ownerData.runtime | Select-Object version, sha256 | ConvertTo-Json -Compress) -ceq ($secondaryData.runtime | Select-Object version, sha256 | ConvertTo-Json -Compress))
            $passed = $sameContext -and -not $secondaryData.isCurrent -and $secondaryData.targetPid -eq $ownerData.pid -and $secondary.Process.ExitCode -eq 0 -and $events.Count -eq 1 -and $events[0].pid -eq $ownerData.pid -and $events[0].arguments.Contains($marker) -and $events[0].arguments.Contains('argument with spaces') -and $events[0].businessRoute -ceq 'probe/orders' -and $events[0].businessId -ceq ('订单 ' + $marker) -and $events[0].businessResult -ceq 'Navigated' -and $events[0].businessData.title -ceq 'A "quoted" title' -and $events[0].businessData.path -ceq 'C:\business files\draft.json'
            $results.Add([pscustomobject]@{
                Case = $case.Name; Iteration = $iteration; Passed = $passed; SameContext = $sameContext
                IdentitySeparated = $secondaryData.isCurrent; OwnerPid = $ownerData.pid
                TargetPid = $secondaryData.targetPid; SecondaryExit = $secondary.Process.ExitCode
                ActivationCount = $events.Count; Owner = $ownerData; Secondary = $secondaryData
            })
        }
        finally {
            [IO.File]::WriteAllText((Join-Path $directory 'stop'), 'stop')
            if ($null -ne $secondary) { Finish-Probe $secondary (Join-Path $directory 'secondary') }
            if ($null -ne $owner) {
                $owner.Process.WaitForExit(5000) | Out-Null
                Finish-Probe $owner (Join-Path $directory 'owner')
            }
        }
    }
}
# Lifecycle scenarios run against the same production helpers as the case matrix.
if (-not $RawSdk) {
    foreach ($scenario in @('during-initialize', 'concurrent-start', 'owner-exit')) {
        $directory = Join-Path $artifacts $scenario
        New-Item -ItemType Directory -Path $directory | Out-Null
        $key = 'XamlNexus.HeadlessProbe.' + [guid]::NewGuid().ToString('N')
        $marker = 'scenario-' + [guid]::NewGuid().ToString('N')
        $first = $null; $second = $null
        try {
            if ($scenario -eq 'concurrent-start') {
                $first = Start-Probe $exe.ToLowerInvariant() @('candidate-a', $key, $directory, $marker)
                $second = Start-Probe $exe.ToUpperInvariant() @('candidate-b', $key, $directory, $marker)
            }
            else {
                $first = Start-Probe $exe @('owner', $key, $directory, '--delay-initialize')
                $timer = [Diagnostics.Stopwatch]::StartNew()
                while (-not (Test-Path -LiteralPath (Join-Path $directory 'ready'))) {
                    if ($first.Process.HasExited -or $timer.Elapsed.TotalSeconds -gt 15) { throw 'Scenario owner failed to become ready.' }
                    Start-Sleep -Milliseconds 25
                }
                $extra = @(); if ($scenario -eq 'owner-exit') { $extra = @('--owner-exit') }
                $second = Start-Probe $exe.ToLowerInvariant() (@('secondary', $key, $directory, $marker) + $extra)
            }
            $timer = [Diagnostics.Stopwatch]::StartNew()
            $passed = $false
            if ($scenario -eq 'owner-exit') {
                $passed = $second.Process.WaitForExit(10000) -and $second.Process.ExitCode -eq 0
            }
            else {
                $eventPath = Join-Path $directory 'activations.jsonl'
                while (-not (Test-Path -LiteralPath $eventPath)) {
                    if ($timer.Elapsed.TotalSeconds -gt 15) { throw 'Scenario activation was not delivered.' }
                    Start-Sleep -Milliseconds 25
                }
                $event = Get-Content -LiteralPath $eventPath | Select-Object -First 1 | ConvertFrom-Json
                if ($scenario -eq 'concurrent-start') {
                    $a = Get-Content -LiteralPath (Join-Path $directory 'candidate-a.json') -Raw | ConvertFrom-Json
                    $b = Get-Content -LiteralPath (Join-Path $directory 'candidate-b.json') -Raw | ConvertFrom-Json
                    $ownerData = if ($a.isCurrent) { $a } else { $b }
                    $secondaryData = if ($a.isCurrent) { $b } else { $a }
                    $loser = if ($a.isCurrent) { $second } else { $first }
                    $passed = ($a.isCurrent -xor $b.isCurrent) -and $loser.Process.WaitForExit(5000) -and $loser.Process.ExitCode -eq 0 -and $secondaryData.targetPid -eq $ownerData.pid -and $event.pid -eq $ownerData.pid -and $event.arguments.Contains($marker)
                }
                else {
                    $ownerData = Get-Content -LiteralPath (Join-Path $directory 'owner.json') -Raw | ConvertFrom-Json
                    $passed = $second.Process.WaitForExit(5000) -and $second.Process.ExitCode -eq 0 -and $event.pid -eq $ownerData.pid -and $event.arguments.Contains($marker)
                }
            }
            $results.Add([pscustomobject]@{ Case = $scenario; Iteration = 1; Passed = $passed })
        }
        finally {
            [IO.File]::WriteAllText((Join-Path $directory 'stop'), 'stop')
            foreach ($child in @($first, $second)) {
                if ($null -ne $child) {
                    $child.Process.WaitForExit(5000) | Out-Null
                    Finish-Probe $child (Join-Path $directory ('child-' + $child.Process.Id))
                }
            }
        }
    }
}

# Verify the first launch's actual SDK payload, held until initialization is complete.
if (-not $RawSdk) {
    $directory = Join-Path $artifacts 'initial-business-route'
    New-Item -ItemType Directory -Path $directory | Out-Null
    $key = 'XamlNexus.HeadlessProbe.' + [guid]::NewGuid().ToString('N')
    $child = $null
    try {
        $json = @{ id = '初次 42' } | ConvertTo-Json -Compress
        $child = Start-Probe $exe @('owner', $key, $directory, '--capture-initial', '--delay-initialize', '--route', 'probe/orders', '--data', $json)
        $eventPath = Join-Path $directory 'activations.jsonl'
        $timer = [Diagnostics.Stopwatch]::StartNew()
        while (-not (Test-Path -LiteralPath $eventPath)) {
            if ($child.Process.HasExited -or $timer.Elapsed.TotalSeconds -gt 15) { throw 'Initial business activation was not delivered.' }
            Start-Sleep -Milliseconds 25
        }
        $events = @(Get-Content -LiteralPath $eventPath | ForEach-Object { $_ | ConvertFrom-Json })
        $ownerData = Get-Content -LiteralPath (Join-Path $directory 'owner.json') -Raw | ConvertFrom-Json
        $passed = $ownerData.isCurrent -and $events.Count -eq 1 -and $events[0].pid -eq $ownerData.pid -and $events[0].businessId -ceq '初次 42' -and $events[0].businessRoute -ceq 'probe/orders' -and $events[0].businessResult -ceq 'Navigated'
        $results.Add([pscustomobject]@{ Case = 'initial-business-route'; Iteration = 1; Passed = $passed })
    }
    finally {
        [IO.File]::WriteAllText((Join-Path $directory 'stop'), 'stop')
        if ($null -ne $child) {
            $child.Process.WaitForExit(5000) | Out-Null
            Finish-Probe $child (Join-Path $directory 'owner')
        }
    }
}
[pscustomobject]@{ SdkVersion = $SdkVersion; RawSdk = [bool]$RawSdk; Results = @($results.ToArray()) } | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $artifacts 'Results.json') -Encoding utf8
$results | Select-Object Case, Iteration, Passed, SameContext, IdentitySeparated, OwnerPid, TargetPid, SecondaryExit, ActivationCount | Format-Table -AutoSize
Write-Host "Results: $artifacts/Results.json"
if (@($results | Where-Object { -not $_.Passed }).Count) { exit 1 }
