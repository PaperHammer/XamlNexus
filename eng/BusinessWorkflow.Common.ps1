# Shared command runner for the reproducible business sample.
function Invoke-BusinessCommand {
    param([string]$Label, [string[]]$Arguments, [string]$Directory = $repo, [int]$TimeoutMinutes = 15)
    Write-Host "[$Label] dotnet $($Arguments -join ' ')"
    $info = [Diagnostics.ProcessStartInfo]::new('dotnet')
    $info.WorkingDirectory = $Directory
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $info
    $stdout = $null; $stderr = $null; $started = $false
    try {
        $started = $process.Start()
        if (-not $started) { throw "Could not start $Label" }
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $timer = [Diagnostics.Stopwatch]::StartNew()
        while (-not $process.WaitForExit(1000)) {
            if ($timer.Elapsed.TotalMinutes -gt $TimeoutMinutes) { throw "$Label timed out." }
        }
        if (-not [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($stdout, $stderr), 10000)) { throw "$Label streams did not close." }
        if ($process.ExitCode -ne 0) { throw "$Label failed. Logs: $artifacts" }
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
