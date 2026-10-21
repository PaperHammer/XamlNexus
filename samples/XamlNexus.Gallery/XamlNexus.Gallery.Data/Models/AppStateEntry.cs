namespace XamlNexus.Gallery.Data.Models;

public sealed class AppStateEntry {
    public required string Key { get; set; }

    public required string Value { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
