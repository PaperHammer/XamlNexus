using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using XamlNexus.Gallery.Data.Models;

namespace XamlNexus.Gallery.Data.Persistence;

public sealed record GalleryBusinessRecord(string Id, string Name, string Description);

/// <summary>Gallery-only business data, stored independently from generated page behavior.</summary>
public sealed class GalleryBusinessStore(IDbContextFactory<AppDbContext> factory) {
    private const string Prefix = "gallery.business/";
    public async Task<IReadOnlyList<GalleryBusinessRecord>> ListAsync(CancellationToken cancellationToken = default) {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var entries = await database.AppState.AsNoTracking().Where(row => row.Key.StartsWith(Prefix))
            .OrderByDescending(row => row.UpdatedAtUtc).ThenBy(row => row.Key).ToArrayAsync(cancellationToken);
        return entries.Select(Read).ToArray();
    }
    public async Task<GalleryBusinessRecord?> GetAsync(string id, CancellationToken cancellationToken = default) {
        ValidateId(id);
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var entry = await database.AppState.AsNoTracking().SingleOrDefaultAsync(row => row.Key == Prefix + id, cancellationToken);
        return entry is null ? null : Read(entry);
    }
    public async Task<GalleryBusinessRecord> SaveAsync(string? id, string name, string description, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A record name is required.", nameof(name));
        id ??= Guid.NewGuid().ToString("N");
        ValidateId(id);
        var record = new GalleryBusinessRecord(id, name.Trim(), description.Trim());
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var entry = await database.AppState.FindAsync([Prefix + id], cancellationToken);
        if (entry is null) database.AppState.Add(new AppStateEntry { Key = Prefix + id, Value = JsonSerializer.Serialize(record) });
        else {
            entry.Value = JsonSerializer.Serialize(record);
            entry.UpdatedAtUtc = DateTime.UtcNow;
        }
        await database.SaveChangesAsync(cancellationToken);
        return record;
    }
    private static GalleryBusinessRecord Read(AppStateEntry entry) {
        var record = JsonSerializer.Deserialize<GalleryBusinessRecord>(entry.Value)
            ?? throw new InvalidDataException("The business record is empty.");
        if (entry.Key != Prefix + record.Id || string.IsNullOrWhiteSpace(record.Name))
            throw new InvalidDataException("The business record is invalid.");
        return record;
    }
    private static void ValidateId(string id) {
        if (!Guid.TryParseExact(id, "N", out _)) throw new ArgumentException("Invalid business record identifier.", nameof(id));
    }
}
