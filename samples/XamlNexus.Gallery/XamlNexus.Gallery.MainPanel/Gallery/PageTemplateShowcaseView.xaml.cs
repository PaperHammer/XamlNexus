using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XamlNexus.Gallery.Common.Utils.DI;
using XamlNexus.Gallery.Data.Persistence;
using XamlNexus.Gallery.MainPanel.Services;
using XamlNexus.Gallery.MainPanel.ViewModels;
using XamlNexus.Gallery.UIComponent.Utils;
namespace XamlNexus.Gallery.MainPanel.Gallery;

public sealed partial class PageTemplateShowcaseView : UserControl {
    public GalleryListViewModel ListModel { get; }
    public GalleryDetailsViewModel DetailsModel { get; }
    public GalleryFormViewModel FormModel { get; }
    private readonly GalleryBusinessDataSource source;
    private string selectedKind = "list";
    private string? selectedRecordId;
    private string? editingRecordId;
    private bool ready;
    private bool switching;
    public PageTemplateShowcaseView() {
        source = new(new GalleryBusinessStore(AppServiceLocator.Services.GetRequiredService<IDbContextFactory<AppDbContext>>()));
        ListModel = new(source);
        DetailsModel = new(source);
        FormModel = new(source);
        InitializeComponent();
        BlankSource.SetSource("<arc:ArcPage x:Class=\"MyApp.MainPanel.WorkspacePage\">\n    <!-- Add page content here. -->\n</arc:ArcPage>", "WorkspacePage.xaml");
        ready = true;
        UpdateSelection("list");
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }
    private async void OnLoaded(object sender, RoutedEventArgs e) {
        LanguageUtil.LanguageUpdated -= LanguageChanged;
        LanguageUtil.LanguageUpdated += LanguageChanged;
        FormModel.PropertyChanged -= FormChanged;
        FormModel.PropertyChanged += FormChanged;
        UpdateSelection(selectedKind);
        await ActivateSelectionAsync();
    }
    private void OnUnloaded(object sender, RoutedEventArgs e) {
        LanguageUtil.LanguageUpdated -= LanguageChanged;
        FormModel.PropertyChanged -= FormChanged;
        Deactivate();
    }
    private void LanguageChanged(object? sender, EventArgs e) { UpdateSelection(selectedKind); UpdateFormState(); }
    private void FormChanged(object? sender, PropertyChangedEventArgs e) {
        if (FormModel.StateText == "Saved") selectedRecordId = editingRecordId = source.LastSavedId;
        UpdateFormState();
    }
    private async void TemplateSelector_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args) {
        if (!ready) return;
        string target = sender.SelectedItem?.Tag as string ?? "list";
        if (switching) { RestoreSelector(); return; }
        switching = true;
        try {
            if (!await ConfirmDiscardAsync()) { RestoreSelector(); return; }
            Deactivate();
            if (target == "form") editingRecordId = null;
            UpdateSelection(target);
            if (IsLoaded) await ActivateSelectionAsync();
        }
        finally { switching = false; }
    }
    private async Task<bool> ConfirmDiscardAsync() {
        if (selectedKind != "form") return true;
        if (FormModel.IsBusy) return false;
        if (!FormModel.HasUnsavedChanges) return true;
        var dialog = new ContentDialog {
            XamlRoot = XamlRoot,
            Title = GalleryStrings.Get("BusinessUnsavedTitle"),
            Content = GalleryStrings.Get("BusinessUnsavedBody"),
            PrimaryButtonText = GalleryStrings.Get("BusinessDiscard"),
            CloseButtonText = GalleryStrings.Get("BusinessKeepEditing"),
            DefaultButton = ContentDialogButton.Close,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
    private void RestoreSelector() {
        ready = false;
        TemplateSelector.SelectedItem = TemplateSelector.Items.OfType<SelectorBarItem>().First(item => (string)item.Tag == selectedKind);
        ready = true;
    }
    private async Task SelectAsync(string kind, string? editId = null) {
        if (switching) return;
        switching = true;
        try {
            if (!await ConfirmDiscardAsync()) return;
            Deactivate();
            editingRecordId = editId;
            UpdateSelection(kind);
            RestoreSelector();
            await ActivateSelectionAsync();
        }
        finally { switching = false; }
    }
    private Task ActivateSelectionAsync() => selectedKind switch {
        "list" => ActivateListAsync(),
        "details" when selectedRecordId is not null => DetailsModel.LoadAsync(selectedRecordId),
        "form" => FormModel.ActivateAsync(editingRecordId),
        _ => Task.CompletedTask,
    };
    private async Task ActivateListAsync() {
        await ListModel.ActivateAsync();
        if (source.NeedsRefresh) await ListModel.RefreshAsync();
    }
    private void Deactivate() { ListModel.Deactivate(); DetailsModel.Deactivate(); FormModel.Deactivate(); }
    private void UpdateSelection(string kind) {
        selectedKind = kind;
        string key = char.ToUpperInvariant(kind[0]) + kind[1..];
        SelectedTitle.Text = GalleryStrings.Get("PageTemplate" + key + "Title");
        SelectedBody.Text = GalleryStrings.Get("PageTemplate" + key + "Body");
        SelectedFiles.Text = GalleryStrings.Get("PageTemplate" + key + "Files");
        SelectedFlow.Text = GalleryStrings.Get("PageTemplate" + key + "Flow");
        BlankPreview.Visibility = kind == "blank" ? Visibility.Visible : Visibility.Collapsed;
        ListPreview.Visibility = kind == "list" ? Visibility.Visible : Visibility.Collapsed;
        DetailsPreview.Visibility = kind == "details" ? Visibility.Visible : Visibility.Collapsed;
        FormPreview.Visibility = kind == "form" ? Visibility.Visible : Visibility.Collapsed;
        BlankSource.Visibility = kind == "blank" ? Visibility.Visible : Visibility.Collapsed;
        TemplateSource.Visibility = kind == "blank" ? Visibility.Collapsed : Visibility.Visible;
        DetailId.Text = selectedRecordId ?? GalleryStrings.Get("BusinessSelectRecord");
        EditRecordButton.IsEnabled = selectedRecordId is not null;
        FailReadButton.IsEnabled = selectedRecordId is not null;
        string name = kind switch { "list" => "Projects", "details" => "OrderDetails", "form" => "OrderEditor", _ => "Workspace" };
        TemplateCommand.SetSource($"xamlnexus page add {name} --kind {kind} --dry-run\nxamlnexus page add {name} --kind {kind}", "PowerShell");
        if (kind != "blank") {
            TemplateSource.Kind = kind switch { "details" => "DetailsTemplate", "form" => "FormTemplate", _ => "Template" };
            TemplateSource.PageName = name;
        }
        UpdateFormState();
    }
    private async void Record_Click(object sender, ItemClickEventArgs e) {
        if (e.ClickedItem is not GalleryListItem item || source.GetId(item) is not string id) return;
        selectedRecordId = id;
        await SelectAsync("details");
    }
    private async void NewRecord_Click(object sender, RoutedEventArgs e) => await SelectAsync("form");
    private async void EditRecord_Click(object sender, RoutedEventArgs e) => await SelectAsync("form", selectedRecordId);
    private async void BackToList_Click(object sender, RoutedEventArgs e) => await SelectAsync("list");
    private async void FailRead_Click(object sender, RoutedEventArgs e) {
        if (selectedRecordId is null || DetailsModel.IsBusy) return;
        source.FailNextRead = true;
        await DetailsModel.LoadAsync(selectedRecordId);
    }
    private void OpenListExample_Click(object sender, RoutedEventArgs e) => GalleryCatalog.Navigate("list");
    private void UpdateFormState() {
        FormState.Text = GalleryStrings.Get(FormModel.IsBusy ? "FormSaving" : FormModel.HasUnsavedChanges ? "FormUnsaved" : FormModel.StateText == "Saved" ? "BusinessSaved" : "FormUnchanged");
        BackToListButton.IsEnabled = !FormModel.IsBusy;
    }
    public Visibility ToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
}
