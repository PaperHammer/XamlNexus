using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using XamlNexus.Gallery.Data.Persistence;

#nullable disable

namespace XamlNexus.Gallery.Data.Migrations;

[DbContext(typeof(AppDbContext))]
public sealed class AppDbContextModelSnapshot : ModelSnapshot {
    protected override void BuildModel(ModelBuilder modelBuilder) {
        modelBuilder.HasAnnotation("ProductVersion", "8.0.30");

        modelBuilder.Entity("XamlNexus.Gallery.Data.Models.AppStateEntry", entity => {
            entity.Property<string>("Key")
                .HasMaxLength(200)
                .HasColumnType("TEXT");
            entity.Property<DateTime>("UpdatedAtUtc")
                .HasColumnType("TEXT");
            entity.Property<string>("Value")
                .IsRequired()
                .HasColumnType("TEXT");
            entity.HasKey("Key");
            entity.ToTable("AppState");
        });
    }
}