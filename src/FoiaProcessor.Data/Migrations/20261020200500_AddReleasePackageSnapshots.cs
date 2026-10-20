using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoiaProcessor.Data.Migrations;

public partial class AddReleasePackageSnapshots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ReleasePackages_FoiaRequestId",
            table: "ReleasePackages");

        migrationBuilder.AddColumn<int>(
            name: "Version",
            table: "ReleasePackages",
            type: "int",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.CreateTable(
            name: "ReleasePackageDocuments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ReleasePackageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FileName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                PackageFileName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                Included = table.Column<bool>(type: "bit", nullable: false),
                ContentSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ReleasePackageDocuments", x => x.Id);
                table.ForeignKey(
                    name: "FK_ReleasePackageDocuments_ReleasePackages_ReleasePackageId",
                    column: x => x.ReleasePackageId,
                    principalTable: "ReleasePackages",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ReleasePackageDocuments_ReleasePackageId",
            table: "ReleasePackageDocuments",
            column: "ReleasePackageId");

        migrationBuilder.CreateIndex(
            name: "IX_ReleasePackages_FoiaRequestId_Version",
            table: "ReleasePackages",
            columns: new[] { "FoiaRequestId", "Version" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ReleasePackageDocuments");

        migrationBuilder.DropIndex(
            name: "IX_ReleasePackages_FoiaRequestId_Version",
            table: "ReleasePackages");

        migrationBuilder.Sql(
            """
            WITH VersionedPackages AS
            (
                SELECT Id, ROW_NUMBER() OVER (PARTITION BY FoiaRequestId ORDER BY Version DESC) AS RowNumber
                FROM ReleasePackages
            )
            DELETE FROM VersionedPackages WHERE RowNumber > 1;
            """);

        migrationBuilder.DropColumn(name: "Version", table: "ReleasePackages");

        migrationBuilder.CreateIndex(
            name: "IX_ReleasePackages_FoiaRequestId",
            table: "ReleasePackages",
            column: "FoiaRequestId",
            unique: true);
    }
}
