using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using XamlNexus.Gallery.UIComponent.Templates;
namespace XamlNexus.Gallery.MainPanel.Gallery;
public sealed partial class ToolGuidePage : ArcPage {
    public override Type ArcType => typeof(ToolGuidePage);
    public ToolGuidePage() {
        InitializeComponent();
        WinuiCode.SetSource("xamlnexus new MyApp --preset winui", "PowerShell");
        HybridCode.SetSource("xamlnexus new HybridApp --preset hybrid", "PowerShell");
        BasicCode.SetSource("xamlnexus new MinimalApp --profile basic", "PowerShell");
        CreateOptionsCode.SetSource("xamlnexus new MyApp\nxamlnexus new DataApp --profile basic --features settings,sqlite\nxamlnexus new XmlApp --solution-format slnx", "PowerShell");
        PageCommandCode.SetSource("xamlnexus page add Dashboard --kind blank\nxamlnexus page add Projects --kind list\nxamlnexus page add OrderDetails --kind details\nxamlnexus page add OrderEditor --kind form", "PowerShell");
        RunCommandCode.SetSource("xamlnexus run --dry-run\nxamlnexus run\nxamlnexus run --no-build", "PowerShell");
        InspectCode.SetSource("xamlnexus doctor --environment\nxamlnexus status\nxamlnexus validate\nxamlnexus doctor", "PowerShell");
        ComponentMaintenanceCode.SetSource("xamlnexus add settings,sqlite --dry-run\nxamlnexus add settings,sqlite\nxamlnexus remove sqlite --dry-run\nxamlnexus update --all --dry-run", "PowerShell");
        ScaffoldMaintenanceCode.SetSource("xamlnexus upgrade --dry-run", "PowerShell");
        AutomationCode.SetSource("xamlnexus status --project . --json\nxamlnexus update --all --project . --dry-run --json\nxamlnexus validate --project . --json\nxamlnexus upgrade --project . --dry-run --json", "PowerShell");
        GalleryCommandCode.SetSource("xamlnexus --version\nxamlnexus gallery", "PowerShell");
    }
    private void Navigate_Click(object sender, RoutedEventArgs e) => GalleryCatalog.Navigate((string)((ButtonBase)sender).Tag);
}
