using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XamlNexus.Gallery.MainPanel.Services;

public sealed record GalleryItemsItem(string Title, string Description);

public interface IGalleryItemsDataSource {
    Task<IReadOnlyList<GalleryItemsItem>> LoadAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Runnable sample data. Replace this implementation with an application service.
/// For hybrid applications, call the host through a client instead of opening its database here.
/// </summary>
public sealed class GalleryItemsSampleDataSource : IGalleryItemsDataSource {
    public Task<IReadOnlyList<GalleryItemsItem>> LoadAsync(CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<GalleryItemsItem>>(new[] {
            new GalleryItemsItem("XamlNexus", "WinUI desktop application"),
            new GalleryItemsItem("GalleryItems", "Replace the sample data source with your service"),
        });
    }
}
