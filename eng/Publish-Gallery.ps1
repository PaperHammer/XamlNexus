param(
    [ValidateSet('x64', 'arm64')]
    [string]$Architecture = 'x64'
)

$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot -Parent
$project = Join-Path $repository 'samples/XamlNexus.Gallery/XamlNexus.Gallery.UI/XamlNexus.Gallery.UI.csproj'
[xml]$properties = Get-Content (Join-Path $repository 'samples/XamlNexus.Gallery/Directory.Build.props')
$version = $properties.Project.PropertyGroup.Version
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
    -p:PublishTrimmed=false -p:PublishReadyToRun=false -p:UseSharedCompilation=false -nr:false
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
Copy-Item (Join-Path $repository 'samples/XamlNexus.Gallery/README.md') (Join-Path $publish 'README.md')
$license = Join-Path $repository 'LICENSE'
if (Test-Path $license) { Copy-Item $license (Join-Path $publish 'LICENSE') }
$archive = Join-Path $output "XamlNexus.Gallery-$version-win-$Architecture.zip"
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $archive
Write-Host "Portable Gallery: $archive"
