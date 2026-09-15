using Microsoft.EntityFrameworkCore;
using SqliteShowcase.Data.Models;

namespace SqliteShowcase.Data.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options) {
    public DbSet<AppStateEntry> AppState => Set<AppStateEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.Entity<AppStateEntry>(entity => {
            entity.ToTable("AppState");
            entity.HasKey(value => value.Key);
            entity.Property(value => value.Key).HasMaxLength(200);
            entity.Property(value => value.Value).IsRequired();
        });
    }
}