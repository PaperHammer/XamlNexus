param(
    [ValidateSet('x64', 'arm64')]
    [string]$Architecture = 'x64',
    [string]$Version,
    [string]$AssetOutputDirectory
)

$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot -Parent
$project = Join-Path $repository 'samples/XamlNexus.Gallery/XamlNexus.Gallery.UI/XamlNexus.Gallery.UI.csproj'
[xml]$properties = Get-Content (Join-Path $repository 'samples/XamlNexus.Gallery/Directory.Build.props')
if ([string]::IsNullOrWhiteSpace($Version)) { $Version = $properties.Project.PropertyGroup.Version }
if ($Version -notmatch '^\d+\.\d+\.\d+(-[A-Za-z0-9.-]+)?$') { throw 'Invalid Gallery version.' }
$assemblyVersion = ($Version -split '-')[0] + '.0'
# A fresh directory prevents stale binaries from entering a portable package.
$output = Join-Path $repository ".artifacts/gallery/$version-win-$Architecture-$(Get-Date -Format 'yyyyMMddHHmmssfff')"
$publish = Join-Path $output 'app'
$platform = if ($Architecture -eq 'arm64') { 'ARM64' } else { 'x64' }

# The XAML compiler can reuse generated resource URIs after publish properties change.
# Clean this configuration so a previous build cannot leak app-only settings into libraries.
dotnet clean $project -c Release -r "win-$Architecture" "-p:Platform=$platform" -m:1 --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw 'Gallery clean failed.' }
# WindowsPackageType and WindowsAppSDKSelfContained belong to the UI project only.
# Passing them globally also changes how referenced libraries compile XAML resource paths.
dotnet publish $project -c Release -r "win-$Architecture" --self-contained true -m:1 `
    -o $publish "-p:Platform=$platform" -p:PublishProfile= `
    -p:PublishSingleFile=false `
    -p:PublishTrimmed=false -p:PublishReadyToRun=false -p:UseSharedCompilation=false -nr:false `
    "-p:Version=$Version" "-p:AssemblyVersion=$assemblyVersion" "-p:FileVersion=$assemblyVersion" "-p:InformationalVersion=$Version"
if ($LASTEXITCODE -ne 0) { throw 'Gallery publish failed.' }
if (!(Test-Path (Join-Path $publish 'XamlNexus.Gallery.exe'))) { throw 'Gallery executable was not produced.' }
foreach ($resource in @('XamlNexus.Gallery.pri', 'App.xbf', 'MainWindow.xbf', 'GalleryShell.xbf', 'GallerySettingsPage.xbf',
    'XamlNexus.Gallery.MainPanel/MainPage.xbf', 'Strings/en-US/Resources.resw', 'Strings/zh-CN/Resources.resw',
    'Assets/ui_components/Light/theme_light.png', 'Assets/ui_components/Dark/theme_dark.png',
    'Assets/ui_components/Light/theme_auto.png', 'Assets/ui_components/Dark/theme_auto.png')) {
    if (!(Test-Path (Join-Path $publish $resource))) { throw "Missing Gallery resource: $resource" }
}
Get-ChildItem (Join-Path $repository 'samples/XamlNexus.Gallery/XamlNexus.Gallery.MainPanel/Gallery') -Filter '*.xaml' | ForEach-Object {
    $resource = Join-Path $publish "XamlNexus.Gallery.MainPanel/Gallery/$($_.BaseName).xbf"
    if (!(Test-Path -LiteralPath $resource)) { throw "Missing Gallery XAML resource: $resource" }
}
# Shared XAML must keep its original resource URI after intermediate source generation.
[xml]$sharedUi = Get-Content (Join-Path $repository 'samples/XamlNexus.Gallery/XamlNexus.Gallery.UIComponent/SharedSources.props')
foreach ($page in $sharedUi.Project.ItemGroup.GallerySharedPage) {
    $resource = Join-Path $publish ('XamlNexus.Gallery.UIComponent/' + [IO.Path]::ChangeExtension($page.Include, '.xbf'))
    if (!(Test-Path -LiteralPath $resource)) { throw "Missing shared Gallery XAML resource: $resource" }
}
Copy-Item (Join-Path $repository 'samples/XamlNexus.Gallery/README.md') (Join-Path $publish 'README.md')
$license = Join-Path $repository 'LICENSE'
if (Test-Path $license) { Copy-Item $license (Join-Path $publish 'LICENSE') }
$archive = Join-Path $output "XamlNexus Gallery v$version.zip"
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $archive
if ($AssetOutputDirectory) {
    New-Item -ItemType Directory -Path $AssetOutputDirectory -Force | Out-Null
    Copy-Item -LiteralPath $archive -Destination $AssetOutputDirectory
}
Write-Host "Portable Gallery: $archive"
