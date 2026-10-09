using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using XamlNexus.Common.Projects;
using XamlNexus.Recipes.BuiltIn;
using XamlNexus.Gallery.UIComponent.Templates;
using XamlNexus.Gallery.UIComponent.Utils;
namespace XamlNexus.Gallery.MainPanel.Gallery;

public sealed partial class ComponentsGalleryPage : ArcPage {
    public override Type ArcType => typeof(ComponentsGalleryPage);
    private bool ready;
    public ComponentsGalleryPage() {
        InitializeComponent();
        ready = true;
        UpdatePlan();
        Loaded += (_, _) => LanguageUtil.LanguageUpdated += LanguageChanged;
        Unloaded += (_, _) => LanguageUtil.LanguageUpdated -= LanguageChanged;
    }
    private void LanguageChanged(object? sender, EventArgs e) => UpdatePlan();
    private void SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdatePlan();
    private void FeatureChanged(object sender, RoutedEventArgs e) => UpdatePlan();
    private CheckBox[] Features => new[] { SettingsFeature, SqliteFeature, TrayFeature, UpdaterFeature, EditorFeature };
    private void UpdatePlan() {
        if (!ready) return;
        string preset = (string)((ComboBoxItem)Preset.SelectedItem).Tag;
        string profile = (string)((ComboBoxItem)Profile.SelectedItem).Tag;
        ready = false;
        bool hybrid = preset == "hybrid";
        TrayFeature.IsEnabled = UpdaterFeature.IsEnabled = !hybrid;
        SettingsFeature.IsEnabled = profile == "basic";
        if (hybrid) TrayFeature.IsChecked = UpdaterFeature.IsChecked = false;
        if (profile == "standard") SettingsFeature.IsChecked = false;
        ready = true;
        string[] included = hybrid ? (profile == "standard" ? new[] { "settings", "system-tray", "app-update" } : new[] { "system-tray", "app-update" }) : (profile == "standard" ? new[] { "settings" } : Array.Empty<string>());
        IncludedText.Text = GalleryStrings.Get(hybrid ? (profile == "standard" ? "CompositionHybridStandard" : "CompositionHybridBasic") : (profile == "standard" ? "CompositionWinuiStandard" : "CompositionWinuiBasic"));
        string[] selected = Features.Where(item => item.IsChecked == true).Select(item => (string)item.Tag).ToArray();
        try {
            var plan = CompositionPlanner.ResolveForCreation(preset, profile, selected, BuiltInRecipeCatalog.Create(), included);
            PlanItems.ItemsSource = plan.Select((recipe, index) => new GalleryRecipePlanItem(index + 1, recipe.Descriptor.Id, selected.Contains(recipe.Descriptor.Id))).ToArray();
            EmptyPlan.Visibility = plan.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            PlanError.IsOpen = false;
            string[] aliases = selected.Select(id => id switch { "system-tray" => "tray", "app-update" => "updater", _ => id }).ToArray();
            string features = aliases.Length == 0 ? string.Empty : " --features " + string.Join(",", aliases);
            CreateCommand.SetSource($"xamlnexus new MyApp --preset {preset} --profile {profile}{features}", "PowerShell");
        } catch (Exception exception) {
            PlanItems.ItemsSource = null;
            PlanError.Message = exception.Message;
            PlanError.IsOpen = true;
        }
    }
    private void Composition_SizeChanged(object sender, SizeChangedEventArgs e) {
        bool wide = e.NewSize.Width >= 880;
        PlanColumn.Width = wide ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        Grid.SetColumn(PlanPane, wide ? 1 : 0);
        Grid.SetRow(PlanPane, wide ? 0 : 1);
    }
    private void Navigate_Click(object sender, RoutedEventArgs e) => GalleryCatalog.Navigate((string)((ButtonBase)sender).Tag);
}
[Microsoft.UI.Xaml.Data.Bindable]
public sealed class GalleryRecipePlanItem(int position, string id, bool requested) {
    private string Key => id switch { "settings" => "Settings", "sqlite" => "Sqlite", "system-tray" => "Tray", "app-update" => "Updater", _ => "Editor" };
    public string Position => position.ToString("00");
    public string Title => GalleryStrings.Get("Recipe" + Key + "Title");
    public string Description => GalleryStrings.Get("Composition" + Key + "Outcome");
    public string Reason => GalleryStrings.Get(requested ? "CompositionRequested" : "CompositionDependency");
}
