# Generate a standalone pure WinUI application from the current CLI plus business-only overlays.
[CmdletBinding()]
param(
    [string]$Output = (Join-Path ([IO.Path]::GetTempPath()) ('xamlnexus-business-' + [guid]::NewGuid().ToString('N'))),
    [switch]$SkipCliBuild,
    [string]$LogDirectory
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $IsWindows) { throw 'This sample requires the Windows WinUI build toolchain.' }
$repo = Split-Path $PSScriptRoot -Parent
$artifacts = if ($LogDirectory) { [IO.Path]::GetFullPath($LogDirectory) } else { Join-Path $repo ('.artifacts/business-create-' + [guid]::NewGuid().ToString('N')) }
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
. (Join-Path $PSScriptRoot 'BusinessWorkflow.Common.ps1')
if (-not $SkipCliBuild) {
    Invoke-BusinessCommand 'cli-build' @('build', "$repo/src/XamlNexus/XamlNexus.csproj", '-m:1', '-nr:false', '-p:UseSharedCompilation=false', '-p:NuGetAudit=false') | Out-Null
}
$cli = "$repo/src/XamlNexus/bin/Debug/net8.0/XamlNexus.dll"
$app = 'BusinessWorkflow'
$parent = [IO.Path]::GetFullPath($Output)
$root = Join-Path $parent $app
Invoke-BusinessCommand 'new' @($cli, 'new', $app, '--preset', 'winui', '--profile', 'basic', '--output', $parent) | Out-Null
Invoke-BusinessCommand 'sqlite' @($cli, 'add', 'sqlite', '--project', $root, '--json') | Out-Null
foreach ($page in @(@('Orders','list'), @('OrderDetails','details'), @('OrderEditor','form'))) {
    Invoke-BusinessCommand ("page-" + $page[0]) @($cli, 'page', 'add', $page[0], '--kind', $page[1], '--project', $root, '--json') | Out-Null
}
$overlay = Join-Path $repo 'samples/XamlNexus.BusinessWorkflow/Overlay'
foreach ($file in Get-ChildItem -LiteralPath $overlay -File -Recurse) {
    $relative = [IO.Path]::GetRelativePath($overlay, $file.FullName).Replace('__APP__', $app)
    $destination = Join-Path $root $relative.Substring(0, $relative.Length - 4)
    if (Test-Path -LiteralPath $destination) { throw "Business overlay collision: $destination" }
    New-Item -ItemType Directory -Force -Path (Split-Path $destination -Parent) | Out-Null
    [IO.File]::WriteAllText($destination, [IO.File]::ReadAllText($file.FullName).Replace('__APP__', $app))
}
# Reuse the proven store source; the generated application has no Gallery reference.
$store = [IO.File]::ReadAllText("$repo/samples/XamlNexus.Gallery/XamlNexus.Gallery.Data/Persistence/GalleryBusinessStore.cs")
$store = $store.Replace('XamlNexus.Gallery', $app).Replace('GalleryBusiness', 'Order').Replace('gallery.business/', 'orders/')
[IO.File]::WriteAllText("$root/$app.Data/Persistence/OrderStore.cs", $store)

function Replace-Required([string]$Relative, [string]$Old, [string]$New) {
    $path = Join-Path $root $Relative
    $text = [IO.File]::ReadAllText($path)
    if (-not $text.Contains($Old)) { throw "Generated source contract changed: $Relative ($Old)" }
    [IO.File]::WriteAllText($path, $text.Replace($Old, $New))
}
foreach ($page in @('Orders','OrderDetails','OrderEditor')) {
    $file = "$app.MainPanel/$($page)Page.xaml.cs"
    Replace-Required $file 'ViewModel { get; } = new();' 'ViewModel { get; }'
    $constructor = "public $($page)Page(I$($page)DataSource source, IOrderNavigation navigation)"
    Replace-Required $file ("using $app.MainPanel.ViewModels;") ("using $app.MainPanel.Services;" + [Environment]::NewLine + "using $app.MainPanel.ViewModels;")
    if ($page -eq 'Orders') {
        Replace-Required $file ("public $($page)Page() {") ($constructor + ' {')
        Replace-Required $file 'InitializeComponent();' 'ViewModel = new(source); this.navigation = navigation; InitializeComponent();'
    }
    else {
        Replace-Required $file ("public $($page)Page() => InitializeComponent();") ($constructor + ' { ViewModel = new(source); this.navigation = navigation; InitializeComponent(); }')
    }
}
Replace-Required "$app.MainPanel/Services/OrdersDataSource.cs" 'OrdersItem(string Title, string Description)' 'OrdersItem(string Title, string Description, string? Id = null)'
Replace-Required "$app.MainPanel/OrderDetailsPage.xaml.cs" 'await ViewModel.LoadAsync(id);' 'recordId = id; await ViewModel.LoadAsync(id);'
Replace-Required "$app.MainPanel/OrdersPage.xaml" '<ListView ItemsSource=' '<ListView IsItemClickEnabled="True" ItemClick="Record_Click" ItemsSource='
Replace-Required "$app.MainPanel/OrdersPage.xaml" '<TextBlock Text="{x:Bind ViewModel.Title}" FontSize="28" />' '<StackPanel Orientation="Horizontal" Spacing="16"><TextBlock Text="{x:Bind ViewModel.Title}" FontSize="28"/><Button Content="New order" Click="New_Click"/></StackPanel>'
Replace-Required "$app.MainPanel/OrderDetailsPage.xaml" '<TextBlock Text="{x:Bind ViewModel.Identifier, Mode=OneWay}"' '<StackPanel Orientation="Horizontal" Spacing="12"><Button Content="Edit" Click="Edit_Click" IsEnabled="{x:Bind ViewModel.HasItem, Mode=OneWay}"/><Button Content="Back to orders" Click="List_Click"/></StackPanel><TextBlock Text="{x:Bind ViewModel.Identifier, Mode=OneWay}"'
Replace-Required "$app.MainPanel/OrderEditorPage.xaml" '<Button Content="{x:Bind ViewModel.SaveText, Mode=OneWay}" Command="{x:Bind ViewModel.SaveCommand}" />' '<Button Content="Save and return to orders" Click="SaveAndList_Click" IsEnabled="{x:Bind ViewModel.CanEdit, Mode=OneWay}"/><Button Content="Back to orders" Click="List_Click"/>'
Replace-Required "$app.UI/MainWindow.xaml.cs" '_navigationMenu.Select("home");' '_navigationMenu.Select("Orders"); // Business application default page.'
Invoke-BusinessCommand 'validate' @($cli, 'validate', '--project', $root, '--json') | Out-Null
Write-Host "Standalone application: $root"
Write-Output $root
