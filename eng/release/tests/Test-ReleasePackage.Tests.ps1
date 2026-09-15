$ErrorActionPreference = 'Stop'
$validator = Join-Path $PSScriptRoot '../Test-ReleasePackage.ps1'
$directory = Join-Path ([IO.Path]::GetTempPath()) ('xamlnexus-package-test-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $directory | Out-Null
try {
    $cases = @(
        @{ Id = 'XamlNexus'; Version = '1.0.4'; Count = 1; Spec = $true; Pass = $true },
        @{ Id = 'XamlNexus'; Version = '1.0.5-preview.1'; Count = 1; Spec = $true; Pass = $true },
        @{ Id = 'Other'; Version = '1.0.4'; Count = 1; Spec = $true; Pass = $false },
        @{ Id = 'XamlNexus'; Version = '1.0.3'; Count = 1; Spec = $true; Pass = $false },
        @{ Id = 'XamlNexus'; Version = '1.0.4'; Count = 0; Spec = $true; Pass = $false },
        @{ Id = 'XamlNexus'; Version = '1.0.4'; Count = 2; Spec = $true; Pass = $false },
        @{ Id = 'XamlNexus'; Version = '1.0.4'; Count = 1; Spec = $false; Pass = $false }
    )
    foreach ($case in $cases) {
        $caseDirectory = Join-Path $directory ([guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $caseDirectory | Out-Null
        for ($index = 0; $index -lt $case.Count; $index++) {
            $zip = [IO.Compression.ZipFile]::Open((Join-Path $caseDirectory "$index.nupkg"), 'Create')
            try {
                if ($case.Spec) {
                    $writer = [IO.StreamWriter]::new($zip.CreateEntry('package.nuspec').Open())
                    try { $writer.Write("<package xmlns='http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd'><metadata><id>$($case.Id)</id><version>$($case.Version)</version></metadata></package>") }
                    finally { $writer.Dispose() }
                }
            }
            finally { $zip.Dispose() }
        }
        $expectedVersion = if ($case.Version -like '*preview*') { $case.Version } else { '1.0.4' }
        $passed = $false
        try {
            & $validator -Directory $caseDirectory -PackageId XamlNexus -Version $expectedVersion
            $passed = $true
        }
        catch { if ($case.Pass) { throw } }
        if ($passed -ne $case.Pass) { throw "Unexpected validation result: $($case | ConvertTo-Json -Compress)" }
    }
    Write-Host "Passed $($cases.Count) release package checks."
}
finally {
    # 仅删除本测试创建的包和空目录，不执行递归删除。
    Get-ChildItem -LiteralPath $directory -File -Recurse | Remove-Item
    Get-ChildItem -LiteralPath $directory -Directory | Remove-Item
    Remove-Item -LiteralPath $directory
}
