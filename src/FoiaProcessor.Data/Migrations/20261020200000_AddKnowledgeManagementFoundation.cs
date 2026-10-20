using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoiaProcessor.Data.Migrations;

[DbContext(typeof(FoiaDbContext))]
[Migration("20261020200000_AddKnowledgeManagementFoundation")]
public partial class AddKnowledgeManagementFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "KnowledgeProfiles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                State = table.Column<string>(type: "nvarchar(max)", nullable: false),
                CreatedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                Revision = table.Column<int>(type: "int", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_KnowledgeProfiles", x => x.Id));

        migrationBuilder.CreateTable(
            name: "KnowledgeSources",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Kind = table.Column<string>(type: "nvarchar(max)", nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                State = table.Column<string>(type: "nvarchar(max)", nullable: false),
                CapabilityState = table.Column<string>(type: "nvarchar(max)", nullable: false),
                CreatedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                Revision = table.Column<int>(type: "int", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_KnowledgeSources", x => x.Id));

        migrationBuilder.CreateTable(
            name: "KnowledgeAdminAudits",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Actor = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                Operation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                ResourceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Revision = table.Column<int>(type: "int", nullable: true),
                Outcome = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                TraceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_KnowledgeAdminAudits", x => x.Id));

        migrationBuilder.CreateTable(
            name: "KnowledgeOperations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ResourceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                OperationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                RequestedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                ErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                TraceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_KnowledgeOperations", x => x.Id));

        migrationBuilder.CreateTable(
            name: "KnowledgeProfileSources",
            columns: table => new
            {
                KnowledgeProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                KnowledgeSourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_KnowledgeProfileSources", x => new { x.KnowledgeProfileId, x.KnowledgeSourceId });
                table.ForeignKey(
                    name: "FK_KnowledgeProfileSources_KnowledgeProfiles_KnowledgeProfileId",
                    column: x => x.KnowledgeProfileId,
                    principalTable: "KnowledgeProfiles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_KnowledgeProfileSources_KnowledgeSources_KnowledgeSourceId",
                    column: x => x.KnowledgeSourceId,
                    principalTable: "KnowledgeSources",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_KnowledgeAdminAudits_Timestamp",
            table: "KnowledgeAdminAudits",
            column: "Timestamp");
        migrationBuilder.CreateIndex(
            name: "IX_KnowledgeOperations_ResourceId_CreatedAt",
            table: "KnowledgeOperations",
            columns: new[] { "ResourceId", "CreatedAt" });
        migrationBuilder.CreateIndex(
            name: "IX_KnowledgeProfiles_Name",
            table: "KnowledgeProfiles",
            column: "Name",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_KnowledgeProfileSources_KnowledgeSourceId",
            table: "KnowledgeProfileSources",
            column: "KnowledgeSourceId");
        migrationBuilder.CreateIndex(
            name: "IX_KnowledgeSources_Name",
            table: "KnowledgeSources",
            column: "Name",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "KnowledgeAdminAudits");
        migrationBuilder.DropTable(name: "KnowledgeOperations");
        migrationBuilder.DropTable(name: "KnowledgeProfileSources");
        migrationBuilder.DropTable(name: "KnowledgeProfiles");
        migrationBuilder.DropTable(name: "KnowledgeSources");
    }
}
