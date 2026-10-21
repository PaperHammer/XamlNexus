using System.Collections.Generic;
using System.Linq;
namespace XamlNexus.Gallery.MainPanel.Gallery;
public static class GalleryHomeContent {
    public static IReadOnlyList<GalleryEntry> Workflows { get; } = GalleryCatalog.Entries.Where(entry => entry.Category == "workflow").ToArray();
    public static IReadOnlyList<GalleryEntry> Examples { get; } = GalleryCatalog.Entries.Where(entry => entry.Category is "mechanics" or "data").ToArray();
}
