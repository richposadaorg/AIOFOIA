using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoiaProcessor.Data.Migrations;

public partial class AddReviewControls : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IncludeInRelease",
            table: "Documents",
            type: "bit",
            nullable: false,
            defaultValue: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "IncludeInRelease",
            table: "Documents");
    }
}
