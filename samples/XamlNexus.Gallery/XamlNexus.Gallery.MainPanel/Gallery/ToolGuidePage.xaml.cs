using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XamlNexus.Gallery.UIComponent.Templates;
namespace XamlNexus.Gallery.MainPanel.Gallery;

public sealed partial class ToolGuidePage : ArcPage
{
    public override Type ArcType => typeof(ToolGuidePage);
    public ToolGuidePage()
    {
        InitializeComponent();
        WinuiCode.SetSource("xamlnexus new MyApp --preset winui", "commands");
        HybridCode.SetSource("xamlnexus new HybridApp --preset hybrid", "commands");
        BasicCode.SetSource("xamlnexus new MinimalApp --profile basic\nxamlnexus new DataApp --profile basic --features settings,sqlite", "commands");
        RecipeListCode.SetSource("xamlnexus recipes\nxamlnexus list\nxamlnexus add settings,sqlite --dry-run", "commands");
        SettingsCode.SetSource("xamlnexus add settings --dry-run", "commands");
        SqliteCode.SetSource("xamlnexus add sqlite --dry-run", "commands");
        TrayCode.SetSource("xamlnexus add tray --dry-run", "commands");
        UpdaterCode.SetSource("xamlnexus add updater --dry-run", "commands");
        EditorCode.SetSource("xamlnexus add editorconfig --dry-run", "commands");
        InspectCode.SetSource("xamlnexus doctor --environment\nxamlnexus validate\nxamlnexus doctor", "commands");
        ComponentCode.SetSource("xamlnexus update sqlite --dry-run\nxamlnexus remove sqlite --dry-run", "commands");
        ScaffoldCode.SetSource("xamlnexus upgrade --dry-run", "commands");
        CreateOptionsCode.SetSource("xamlnexus\nxamlnexus new MyApp --preset winui --profile standard --language zh-CN --solution-format slnx --output D:\\Projects", "commands");
        PageCommandCode.SetSource("xamlnexus page add Dashboard --kind blank --dry-run\nxamlnexus page add Projects --kind list\nxamlnexus page add EmbeddedPanel --kind blank --no-navigation", "commands");
        RunCommandCode.SetSource("xamlnexus run --dry-run\nxamlnexus run\nxamlnexus run --no-build", "commands");
        AutomationCode.SetSource("xamlnexus list --project . --json\nxamlnexus validate --project . --json\nxamlnexus doctor --project . --json\nxamlnexus upgrade --project . --dry-run --json", "commands");
        GalleryCommandCode.SetSource("xamlnexus --version\nxamlnexus gallery", "commands");
    }
    private void GuideTabs_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (ArchitecturePanel is null || RecipesPanel is null || MaintenancePanel is null || CommandsPanel is null) return;
        string? tab = sender.SelectedItem?.Tag as string;
        ArchitecturePanel.Visibility = tab == "architecture" ? Visibility.Visible : Visibility.Collapsed;
        RecipesPanel.Visibility = tab == "recipes" ? Visibility.Visible : Visibility.Collapsed;
        MaintenancePanel.Visibility = tab == "maintenance" ? Visibility.Visible : Visibility.Collapsed;
        CommandsPanel.Visibility = tab == "commands" ? Visibility.Visible : Visibility.Collapsed;
    }
}
