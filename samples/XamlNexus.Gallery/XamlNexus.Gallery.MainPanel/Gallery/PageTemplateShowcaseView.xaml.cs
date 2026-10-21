using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Text.Json;
using XamlNexus.Gallery.UIComponent.Activation;
using XamlNexus.Gallery.UIComponent.Navigation;
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
    private bool confirmingLeave;
    private readonly BusinessActivationNavigator activationNavigator;
    private CancellationTokenSource? activationCancellation;
    private string? activationStatusKey;
    private string? activationError;

    public PageTemplateShowcaseView() {
        source = new(new GalleryBusinessStore(AppServiceLocator.Services.GetRequiredService<IDbContextFactory<AppDbContext>>()));
        ListModel = new(source);
        DetailsModel = new(source);
        FormModel = new(source);
        var activationRoutes = new NavigationRegistry();
        // The Gallery adapts these registered example routes to its existing details/form preview.
        activationRoutes.Register(new("OrderDetails", typeof(BusinessPagesGalleryPage), "Details"));
        activationRoutes.Register(new("OrderEditor", typeof(BusinessPagesGalleryPage), "Editor"));
        activationNavigator = new(activationRoutes);
        InitializeComponent();
        ActivationCommand.SetSource("& .\\MyApp.UI.exe --route OrderEditor --data '{}'", "PowerShell 7");
        BlankSource.SetSource("<arc:ArcPage x:Class=\"MyApp.MainPanel.WorkspacePage\">\n    <!-- Add page content here. -->\n</arc:ArcPage>", "WorkspacePage.xaml");
        ready = true;
        UpdateSelection("list");
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e) {
        activationCancellation ??= new();
        ActivationApplyButton.IsEnabled = true;
        LanguageUtil.LanguageUpdated -= LanguageChanged;
        LanguageUtil.LanguageUpdated += LanguageChanged;
        FormModel.PropertyChanged -= FormChanged;
        FormModel.PropertyChanged += FormChanged;
        UpdateSelection(selectedKind);
        await ActivateSelectionAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) {
        activationCancellation?.Cancel();
        activationCancellation?.Dispose();
        activationCancellation = null;
        LanguageUtil.LanguageUpdated -= LanguageChanged;
        FormModel.PropertyChanged -= FormChanged;
        Deactivate();
    }

    private void LanguageChanged(object? sender, EventArgs e) { UpdateSelection(selectedKind); UpdateFormState(); UpdateActivationStatus(); }

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

    public Task<bool> CanLeaveAsync(ArcLeaveReason reason, CancellationToken cancellationToken = default) =>
        switching ? Task.FromResult(false) : ConfirmDiscardAsync(cancellationToken);

    private async Task<bool> ConfirmDiscardAsync(CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        if (selectedKind != "form") return true;
        if (confirmingLeave || FormModel.IsBusy) return false;
        if (!FormModel.HasUnsavedChanges) return true;
        confirmingLeave = true;
        try {
            var dialog = new ContentDialog {
                XamlRoot = XamlRoot,
                RequestedTheme = ActualTheme,
                Title = GalleryStrings.Get("BusinessUnsavedTitle"),
                Content = GalleryStrings.Get("BusinessUnsavedBody"),
                PrimaryButtonText = GalleryStrings.Get("BusinessSaveAndLeave"),
                SecondaryButtonText = GalleryStrings.Get("BusinessDiscard"),
                CloseButtonText = GalleryStrings.Get("BusinessKeepEditing"),
                DefaultButton = ContentDialogButton.Close,
            };
            using var registration = cancellationToken.Register(() => DispatcherQueue.TryEnqueue(() => dialog.Hide()));
            var result = await dialog.ShowAsync();
            cancellationToken.ThrowIfCancellationRequested();
            if (result == ContentDialogResult.Secondary) return true;
            return result == ContentDialogResult.Primary && await FormModel.TrySaveAsync();
        }
        finally { confirmingLeave = false; }
    }

    private void RestoreSelector() {
        ready = false;
        TemplateSelector.SelectedItem = TemplateSelector.Items.OfType<SelectorBarItem>().First(item => (string)item.Tag == selectedKind);
        ready = true;
    }

    private async Task<bool> SelectAsync(string kind, string? editId = null,
        string? recordId = null, CancellationToken cancellationToken = default) {
        if (switching) return false;
        switching = true;
        try {
            if (!await ConfirmDiscardAsync(cancellationToken)) return false;
            cancellationToken.ThrowIfCancellationRequested();
            Deactivate();
            if (recordId is not null) selectedRecordId = recordId;
            editingRecordId = editId;
            UpdateSelection(kind);
            RestoreSelector();
            await ActivateSelectionAsync();
            return true;
        }
        finally { switching = false; }
    }

    private void ActivationSelectedRecord_Click(object sender, RoutedEventArgs e) {
        if (selectedRecordId is null) return;
        ActivationRoute.Text = (string)((Button)sender).Tag;
        ActivationData.Text = JsonSerializer.Serialize(new { id = selectedRecordId }, new JsonSerializerOptions { WriteIndented = true });
    }

    private async void ActivationApply_Click(object sender, RoutedEventArgs e) {
        var lifetime = activationCancellation;
        if (lifetime is null || switching || !ActivationApplyButton.IsEnabled) return;
        ActivationApplyButton.IsEnabled = false;
        try {
            var request = BusinessActivationRequest.FromArguments(new[] { "--route", ActivationRoute.Text, "--data", ActivationData.Text })!;
            // Copyable command only: never launch a process or run the CLI from the Gallery.
            string Quote(string text) => "'" + text.Replace("'", "''") + "'";
            ActivationCommand.SetSource("& .\\MyApp.UI.exe --route " + Quote(request.Route) + " --data " + Quote(request.Data.GetRawText()), "PowerShell 7");
            var result = await activationNavigator.NavigateAsync(request, async (_, payload, token) => {
                string? id = payload.GetBusinessString("id");
                if (request.Route == "OrderDetails" && string.IsNullOrWhiteSpace(id))
                    throw new FormatException(GalleryStrings.Get("ActivationNeedsId"));
                return await SelectAsync(request.Route == "OrderDetails" ? "details" : "form",
                    editId: id, recordId: id, cancellationToken: token);
            }, lifetime.Token);
            if (!ReferenceEquals(lifetime, activationCancellation)) return;
            activationStatusKey = result switch {
                ActivationNavigationResult.Navigated => "ActivationNavigated",
                ActivationNavigationResult.Cancelled => "ActivationCancelled",
                _ => "ActivationUnknownRoute",
            };
            activationError = null;
            ActivationFeedback.Severity = result == ActivationNavigationResult.RouteNotFound
                ? InfoBarSeverity.Error : InfoBarSeverity.Informational;
            UpdateActivationStatus();
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception exception) {
            if (!ReferenceEquals(lifetime, activationCancellation)) return;
            activationStatusKey = "ActivationInvalid";
            activationError = exception.Message;
            ActivationFeedback.Severity = InfoBarSeverity.Error;
            UpdateActivationStatus();
        }
        finally {
            if (ReferenceEquals(lifetime, activationCancellation)) ActivationApplyButton.IsEnabled = true;
        }
    }

    private void UpdateActivationStatus() {
        ActivationFeedback.IsOpen = activationStatusKey is not null;
        if (activationStatusKey is not null) {
            ActivationFeedback.Title = GalleryStrings.Get(activationStatusKey);
            ActivationFeedback.Message = activationError ?? string.Empty;
        }
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
        ActivationDetailsButton.IsEnabled = ActivationEditorButton.IsEnabled = selectedRecordId is not null;
        FormState.Text = GalleryStrings.Get(FormModel.IsBusy ? "FormSaving" : FormModel.HasUnsavedChanges ? "FormUnsaved" : FormModel.StateText == "Saved" ? "BusinessSaved" : "FormUnchanged");
        BackToListButton.IsEnabled = !FormModel.IsBusy;
    }

    public Visibility ToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
}
