using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace WorkItems.Api.Migrations;

// Additive: existing rows start at version 0.
[DbContext(typeof(WorkItemsDb))]
[Migration("20260918000000_AddVersion")]
public sealed class AddVersion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<int>(
            name: "Version", table: "WorkItems", type: "INTEGER", nullable: false, defaultValue: 0);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "Version", table: "WorkItems");
}
