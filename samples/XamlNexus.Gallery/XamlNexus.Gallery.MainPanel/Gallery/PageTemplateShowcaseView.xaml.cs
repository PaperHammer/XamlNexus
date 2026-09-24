using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XamlNexus.Gallery.UIComponent.Utils;

namespace XamlNexus.Gallery.MainPanel.Gallery;

public sealed partial class PageTemplateShowcaseView : UserControl {
    private int detailsRequest;
    private int formRequest;
    private bool transientDetailsFailed;
    private bool formReady;
    private bool formBusy;
    private string savedName = "Northwind";
    private string savedDescription = "Desktop customer workspace";

    public PageTemplateShowcaseView() {
        InitializeComponent();
        BlankSource.SetSource("""
            <arc:ArcPage
                x:Class="MyApp.MainPanel.WorkspacePage"
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:arc="using:MyApp.UIComponent.Templates">
                <StackPanel Padding="24" Spacing="12">
                    <TextBlock Text="Workspace" FontSize="28" />
                    <!-- Add page content here. -->
                </StackPanel>
            </arc:ArcPage>
            """, "WorkspacePage.xaml");
        formReady = true;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        UpdateSelection("blank");
        UpdateFormState();
    }

    private void OnLoaded(object sender, RoutedEventArgs e) {
        LanguageUtil.LanguageUpdated += LanguageChanged;
        UpdateSelection(SelectedKind.Tag as string ?? "blank");
        UpdateFormState();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) {
        LanguageUtil.LanguageUpdated -= LanguageChanged;
        detailsRequest++;
        formRequest++;
        formBusy = false;
        DetailsProgress.Visibility = Visibility.Collapsed;
        LoadDetailsButton.IsEnabled = true;
    }

    private void LanguageChanged(object? sender, EventArgs e) {
        UpdateSelection(SelectedKind.Tag as string ?? "blank");
        if (DetailsResult.Visibility == Visibility.Visible) {
            DetailsResultTitle.Text = GalleryStrings.Get("DetailsResultTitle");
            DetailsResultDescription.Text = GalleryStrings.Get("DetailsResultDescription");
        }
        UpdateFormState();
    }

    private void TemplateSelector_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args) {
        if (SelectedKind is null) return;
        UpdateSelection(sender.SelectedItem?.Tag as string ?? "blank");
    }

    private void UpdateSelection(string kind) {
        if (kind != "details") {
            detailsRequest++;
            DetailsProgress.Visibility = Visibility.Collapsed;
            LoadDetailsButton.IsEnabled = true;
        }
        SelectedKind.Tag = kind;
        SelectedKind.Text = kind.ToUpperInvariant();
        SelectedTitle.Text = GalleryStrings.Get("PageTemplate" + Capitalize(kind) + "Title");
        SelectedBody.Text = GalleryStrings.Get("PageTemplate" + Capitalize(kind) + "Body");
        SelectedFiles.Text = GalleryStrings.Get("PageTemplate" + Capitalize(kind) + "Files");
        SelectedFlow.Text = GalleryStrings.Get("PageTemplate" + Capitalize(kind) + "Flow");

        BlankPreview.Visibility = kind == "blank" ? Visibility.Visible : Visibility.Collapsed;
        ListPreview.Visibility = kind == "list" ? Visibility.Visible : Visibility.Collapsed;
        DetailsPreview.Visibility = kind == "details" ? Visibility.Visible : Visibility.Collapsed;
        FormPreview.Visibility = kind == "form" ? Visibility.Visible : Visibility.Collapsed;
        BlankSource.Visibility = kind == "blank" ? Visibility.Visible : Visibility.Collapsed;
        TemplateSource.Visibility = kind == "blank" ? Visibility.Collapsed : Visibility.Visible;

        string name = kind switch { "list" => "Projects", "details" => "OrderDetails", "form" => "OrderEditor", _ => "Workspace" };
        TemplateCommand.SetSource($"xamlnexus page add {name} --kind {kind} --dry-run\nxamlnexus page add {name} --kind {kind}", "commands");
        if (kind != "blank") {
            TemplateSource.Kind = kind switch { "details" => "DetailsTemplate", "form" => "FormTemplate", _ => "Template" };
            TemplateSource.PageName = name;
        }
    }

    private async void LoadDetails_Click(object sender, RoutedEventArgs e) {
        int request = ++detailsRequest;
        string id = string.IsNullOrWhiteSpace(DetailId.Text) ? "A-1042" : DetailId.Text.Trim();
        DetailsError.IsOpen = false;
        DetailsResult.Visibility = Visibility.Collapsed;
        DetailsProgress.Visibility = Visibility.Visible;
        LoadDetailsButton.IsEnabled = false;
        await Task.Delay(650);
        if (request != detailsRequest || !IsLoaded) return;
        DetailsProgress.Visibility = Visibility.Collapsed;
        LoadDetailsButton.IsEnabled = true;
        if (id.Equals("retry", StringComparison.OrdinalIgnoreCase) && !transientDetailsFailed) {
            transientDetailsFailed = true;
            DetailsError.IsOpen = true;
            return;
        }
        transientDetailsFailed = false;
        DetailsResultTitle.Text = GalleryStrings.Get("DetailsResultTitle");
        DetailsResultDescription.Text = GalleryStrings.Get("DetailsResultDescription");
        DetailsResultId.Text = "ID: " + id;
        DetailsResult.Visibility = Visibility.Visible;
    }

    private void OpenListExample_Click(object sender, RoutedEventArgs e) => GalleryCatalog.Navigate("list");

    private void FormField_TextChanged(object sender, TextChangedEventArgs e) {
        if (!formReady) return;
        FormNameError.Visibility = Visibility.Collapsed;
        UpdateFormState();
    }

    private async void SaveForm_Click(object sender, RoutedEventArgs e) {
        if (formBusy) return;
        if (string.IsNullOrWhiteSpace(FormName.Text)) {
            FormNameError.Visibility = Visibility.Visible;
            return;
        }
        formBusy = true;
        int request = ++formRequest;
        UpdateFormState();
        await Task.Delay(600);
        if (request != formRequest || !IsLoaded) return;
        savedName = FormName.Text.Trim();
        savedDescription = FormDescription.Text.Trim();
        formBusy = false;
        FormState.Tag = "saved";
        UpdateFormState();
    }

    private void ResetForm_Click(object sender, RoutedEventArgs e) {
        FormName.Text = savedName;
        FormDescription.Text = savedDescription;
        FormNameError.Visibility = Visibility.Collapsed;
        FormState.Tag = null;
        UpdateFormState();
    }

    private void UpdateFormState() {
        if (!formReady) return;
        bool dirty = FormName.Text != savedName || FormDescription.Text != savedDescription;
        SaveFormButton.IsEnabled = !formBusy && dirty;
        ResetFormButton.IsEnabled = !formBusy && dirty;
        FormName.IsEnabled = FormDescription.IsEnabled = !formBusy;
        FormState.Text = GalleryStrings.Get(formBusy ? "FormSaving" : FormState.Tag as string == "saved" && !dirty ? "FormSaved" : dirty ? "FormUnsaved" : "FormUnchanged");
        if (dirty) FormState.Tag = null;
    }

    private static string Capitalize(string value) => char.ToUpperInvariant(value[0]) + value[1..];
}
