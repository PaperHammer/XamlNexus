using Xunit;
using XamlNexus.Gallery.MainPanel.Services;
using XamlNexus.Gallery.MainPanel.ViewModels;
namespace XamlNexus.TemplateTests;
public sealed class FormLeaveSaveTests {
    private sealed class Source : IGalleryFormDataSource {
        public Func<GalleryFormDraft, CancellationToken, Task<GalleryFormDraft>> Save { get; set; } = (draft, _) => Task.FromResult(draft with { Id = "saved-id" });
        public int Saves { get; private set; }
        public CancellationToken SaveToken { get; private set; }
        public Task<GalleryFormDraft?> GetAsync(string id, CancellationToken cancellationToken) => Task.FromResult<GalleryFormDraft?>(new(id, "Original", "Notes"));
        public Task<GalleryFormDraft> SaveAsync(GalleryFormDraft draft, CancellationToken cancellationToken) { Saves++; SaveToken = cancellationToken; return Save(draft, cancellationToken); }
    }
    [Fact]
    public async Task Successful_save_allows_leave_only_after_completion() {
        var gate = new TaskCompletionSource<GalleryFormDraft>(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = new Source { Save = (_, _) => gate.Task }; var model = new GalleryFormViewModel(source);
        await model.ActivateAsync(null); model.Name = "Changed";
        Task<bool> saving = model.TrySaveAsync(); Assert.True(model.IsBusy); Assert.False(saving.IsCompleted);
        Assert.False(await model.TrySaveAsync()); Assert.Equal(1, source.Saves);
        gate.SetResult(new("saved-id", "Changed", "")); Assert.True(await saving);
        Assert.False(model.HasUnsavedChanges); Assert.False(model.IsBusy);
    }
    [Fact]
    public async Task Invalid_form_keeps_changes_and_never_calls_the_service() {
        var source = new Source(); var model = new GalleryFormViewModel(source);
        await model.ActivateAsync(null); model.Description = "Unsaved notes";
        Assert.False(await model.TrySaveAsync()); Assert.True(model.HasNameError); Assert.True(model.HasUnsavedChanges);
        Assert.Equal(0, source.Saves);
    }
    [Fact]
    public async Task Failed_save_keeps_changes_and_can_be_retried() {
        var source = new Source { Save = (_, _) => Task.FromException<GalleryFormDraft>(new IOException("unavailable")) };
        var model = new GalleryFormViewModel(source); await model.ActivateAsync(null); model.Name = "Draft";
        Assert.False(await model.TrySaveAsync()); Assert.True(model.HasError); Assert.True(model.HasUnsavedChanges);
        Assert.Equal("Draft", model.Name);
        source.Save = (draft, _) => Task.FromResult(draft with { Id = "saved-id" });
        Assert.True(await model.TrySaveAsync()); Assert.False(model.HasError); Assert.False(model.HasUnsavedChanges);
    }
    [Fact]
    public async Task Deactivation_during_save_cannot_authorize_leaving_on_a_stale_result() {
        var gate = new TaskCompletionSource<GalleryFormDraft>(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = new Source { Save = (_, _) => gate.Task }; var model = new GalleryFormViewModel(source);
        await model.ActivateAsync(null); model.Name = "Keep this draft";
        Task<bool> saving = model.TrySaveAsync(); model.Deactivate(); Assert.True(source.SaveToken.IsCancellationRequested);
        gate.SetResult(new("saved-id", "Unexpected stale value", "")); Assert.False(await saving);
        Assert.Equal("Keep this draft", model.Name); Assert.True(model.HasUnsavedChanges);
    }
}
