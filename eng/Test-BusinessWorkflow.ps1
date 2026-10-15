# End-to-end acceptance of a CLI-generated standalone business application; no desktop control.
[CmdletBinding()]
param([ValidateRange(1, 60)][int]$TimeoutMinutes = 15)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $IsWindows) { throw 'WinUI compilation requires Windows.' }
$repo = Split-Path $PSScriptRoot -Parent
$artifacts = Join-Path $repo ('.artifacts/business-workflow-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $artifacts | Out-Null
. (Join-Path $PSScriptRoot 'BusinessWorkflow.Common.ps1')
$results = [Collections.Generic.List[object]]::new()
$root = $null; $database = $null
try {
    $root = & (Join-Path $PSScriptRoot 'New-BusinessWorkflow.ps1') -LogDirectory $artifacts
    $cli = "$repo/src/XamlNexus/bin/Debug/net8.0/XamlNexus.dll"
    $database = Join-Path $artifacts 'database/app.db'
    $probeProject = Join-Path $PSScriptRoot 'diagnostics/BusinessWorkflowProbe/BusinessWorkflowProbe.csproj'
    $probe = Join-Path $PSScriptRoot 'diagnostics/BusinessWorkflowProbe/bin/Debug/net8.0-windows10.0.19041.0/BusinessWorkflowProbe.dll'
    Invoke-BusinessCommand 'probe-build' @('build', $probeProject, "-p:BusinessProjectRoot=$root", '-m:1', '-nr:false', '-p:UseSharedCompilation=false', '-p:NuGetAudit=false') -TimeoutMinutes $TimeoutMinutes | Out-Null

    function Test-UiBuild([string]$Stage) {
        Invoke-BusinessCommand "$Stage-build" @('build', "$root/BusinessWorkflow.UI/BusinessWorkflow.UI.csproj", '-m:1', '-nr:false', '-p:Platform=x64', '-p:RuntimeIdentifier=win-x64', '-p:WindowsPackageType=None', '-p:UseSharedCompilation=false', '-p:NuGetAudit=false') -TimeoutMinutes $TimeoutMinutes | Out-Null
        $results.Add([ordered]@{ stage = $Stage; passed = $true; type = 'WinUI compile; application not launched' })
    }
    Test-UiBuild 'before-upgrade'
    foreach ($stage in @('seed', 'reopen')) {
        $evidence = Invoke-BusinessCommand $stage @($probe, $stage, $root, $database) | ConvertFrom-Json
        $results.Add($evidence)
    }
    # A second clean scaffold provides authoritative old baseline files. Compatible
    # controlled source revisions are then tested through the production upgrade API.
    $targetParent = Join-Path ([IO.Path]::GetTempPath()) ('xamlnexus-business-target-' + [guid]::NewGuid().ToString('N'))
    Invoke-BusinessCommand 'target-new' @($cli, 'new', 'BusinessWorkflow', '--preset', 'winui', '--profile', 'basic', '--output', $targetParent) | Out-Null
    $target = Join-Path $targetParent 'BusinessWorkflow'
    $evidence = Invoke-BusinessCommand 'upgrade' @($probe, 'upgrade', $root, $database, $target) | ConvertFrom-Json
    $results.Add($evidence)
    Invoke-BusinessCommand 'post-upgrade-validate' @($cli, 'validate', '--project', $root, '--json') | Out-Null
    Invoke-BusinessCommand 'cli-upgrade-preview' @($cli, 'upgrade', '--project', $root, '--dry-run', '--json') | Out-Null
    Test-UiBuild 'after-upgrade'
    $results.Add((Invoke-BusinessCommand 'post-upgrade-reopen' @($probe, 'reopen', $root, $database) | ConvertFrom-Json))
}
catch {
    $results.Add([ordered]@{ passed = $false; error = $_.Exception.Message })
    throw
}
finally {
    [ordered]@{ project = $root; database = $database; results = @($results.ToArray()) } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $artifacts 'results.json') -Encoding utf8
    Write-Host "Acceptance evidence: $artifacts"
    Write-Host "Generated application retained: $root"
}
