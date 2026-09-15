using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SqliteShowcase.Data.Persistence;

#nullable disable

namespace SqliteShowcase.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260831000000_InitialCreate")]
public sealed class InitialCreate : Migration {
    protected override void Up(MigrationBuilder migrationBuilder) {
        migrationBuilder.CreateTable(
            name: "AppState",
            columns: table => new {
                Key = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                Value = table.Column<string>(type: "TEXT", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_AppState", value => value.Key));
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "AppState");
}