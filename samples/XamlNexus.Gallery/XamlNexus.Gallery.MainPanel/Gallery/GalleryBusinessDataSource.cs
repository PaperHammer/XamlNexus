using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using XamlNexus.Gallery.Data.Persistence;
using XamlNexus.Gallery.MainPanel.Services;

namespace XamlNexus.Gallery.MainPanel.Gallery;

public sealed class GalleryBusinessDataSource(GalleryBusinessStore store) : IGalleryListDataSource, IGalleryDetailsDataSource, IGalleryFormDataSource {
    private readonly Dictionary<GalleryListItem, string> identifiers = new(ReferenceEqualityComparer.Instance);
    public bool FailNextRead { get; set; }
    public bool NeedsRefresh { get; private set; }
    public string? LastSavedId { get; private set; }
    public string? GetId(GalleryListItem item) => identifiers.TryGetValue(item, out var id) ? id : null;
    public async Task<IReadOnlyList<GalleryListItem>> LoadAsync(CancellationToken cancellationToken) {
        var records = await store.ListAsync(cancellationToken);
        var result = new List<GalleryListItem>();
        // Duplicate names remain distinguishable through the displayed item instance.
        identifiers.Clear();
        foreach (var record in records) {
            var item = new GalleryListItem(record.Name, record.Description);
            identifiers.Add(item, record.Id);
            result.Add(item);
        }
        NeedsRefresh = false;
        return result;
    }
    async Task<GalleryDetailsDetails> IGalleryDetailsDataSource.GetAsync(string id, CancellationToken cancellationToken) {
        await Task.Delay(200, cancellationToken);
        if (FailNextRead) {
            FailNextRead = false;
            throw new InvalidOperationException(GalleryStrings.Get("DetailsErrorMessage"));
        }
        var record = await store.GetAsync(id, cancellationToken)
            ?? throw new InvalidOperationException(GalleryStrings.Get("BusinessRecordMissing"));
        return new(record.Id, record.Name, record.Description);
    }
    async Task<GalleryFormDraft?> IGalleryFormDataSource.GetAsync(string id, CancellationToken cancellationToken) {
        var record = await store.GetAsync(id, cancellationToken);
        return record is null ? null : new(record.Id, record.Name, record.Description);
    }
    public async Task<GalleryFormDraft> SaveAsync(GalleryFormDraft draft, CancellationToken cancellationToken) {
        var record = await store.SaveAsync(draft.Id, draft.Name, draft.Description, cancellationToken);
        LastSavedId = record.Id;
        NeedsRefresh = true;
        return new(record.Id, record.Name, record.Description);
    }
}
