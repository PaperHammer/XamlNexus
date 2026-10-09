using System;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XamlNexus.Common.Projects;
using XamlNexus.Gallery.UIComponent.Templates;
using XamlNexus.Gallery.UIComponent.Utils;
namespace XamlNexus.Gallery.MainPanel.Gallery;
public sealed partial class ProjectEvolutionGalleryPage : ArcPage {
    public override Type ArcType => typeof(ProjectEvolutionGalleryPage);
    private const string Baseline = "public sealed class WorkspaceOptions {\n    public string Title { get; } = \"Workspace\";\n    public int PageSize { get; } = 20;\n}\n";
    private static string DefaultLocal => Baseline.Replace("Workspace\"", "AssetDesk\"");
    private bool ready;
    public ProjectEvolutionGalleryPage() {
        InitializeComponent();
        BaselineText.SetSource(Baseline, "WorkspaceOptions.cs");
        LocalText.Text = DefaultLocal;
        EvolutionCommands.SetSource("xamlnexus status\nxamlnexus update --all --dry-run\nxamlnexus upgrade --dry-run", "PowerShell");
        ready = true;
        UpdateMerge();
        Loaded += (_, _) => LanguageUtil.LanguageUpdated += LanguageChanged;
        Unloaded += (_, _) => LanguageUtil.LanguageUpdated -= LanguageChanged;
    }
    private void LanguageChanged(object? sender, EventArgs e) => UpdateMerge();
    private void ScenarioChanged(object sender, SelectionChangedEventArgs e) => UpdateMerge();
    private void LocalChanged(object sender, TextChangedEventArgs e) => UpdateMerge();
    private void Reset_Click(object sender, RoutedEventArgs e) { LocalText.Text = DefaultLocal; UpdateMerge(); }
    private void UpdateMerge() {
        if (!ready) return;
        bool overlap = ((ComboBoxItem)Scenario.SelectedItem).Tag as string == "overlap";
        string target = overlap ? Baseline.Replace("Workspace\"", "Dashboard\"") : Baseline.Replace("= 20", "= 50");
        TargetText.SetSource(target, "WorkspaceOptions.cs");
        byte[] baseline = Encoding.UTF8.GetBytes(Baseline), local = Encoding.UTF8.GetBytes(LocalText.Text.Replace("\r\n", "\n").Replace('\r', '\n')), updated = Encoding.UTF8.GetBytes(target);
        var result = XamlNexusThreeWayTextMerge.Merge(baseline, local, updated);
        bool merged = result.Status == XamlNexusMergeStatus.Merged;
        MergeStatus.Severity = merged ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
        MergeStatus.Title = GalleryStrings.Get(merged ? "MergeSuccessTitle" : result.Status == XamlNexusMergeStatus.Conflict ? "MergeConflictTitle" : "MergeUnsupportedTitle");
        MergeStatus.Message = GalleryStrings.Get(merged ? "MergeSuccessBody" : result.Status == XamlNexusMergeStatus.Conflict ? "MergeConflictBody" : "MergeUnsupportedBody");
        string content = merged ? Encoding.UTF8.GetString(result.Content!) : XamlNexusThreeWayTextMerge.CreateConflictDocument("WorkspaceOptions.cs", "baseline", "target", baseline, local, updated);
        MergeResult.SetSource(content, merged ? "WorkspaceOptions.cs" : "WorkspaceOptions.conflict.cs");
    }
    private void Inputs_SizeChanged(object sender, SizeChangedEventArgs e) {
        bool wide = e.NewSize.Width >= 760;
        TargetColumn.Width = wide ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        Grid.SetColumn(TargetPane, wide ? 1 : 0);
        Grid.SetRow(TargetPane, wide ? 0 : 1);
    }
}
