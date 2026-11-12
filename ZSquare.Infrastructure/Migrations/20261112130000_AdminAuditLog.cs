using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZSquare.Infrastructure.Migrations;

public partial class AdminAuditLog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AuditLogs",
            columns: table => new
            {
                AuditLogId = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                ActorUserId = table.Column<int>(type: "int", nullable: true),
                Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                EntityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                DetailsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuditLogs", x => x.AuditLogId);
                table.ForeignKey("FK_AuditLogs_Users_ActorUserId", x => x.ActorUserId, "Users", "UserId", onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex("IX_AuditLogs_ActorUserId", "AuditLogs", "ActorUserId");
        migrationBuilder.CreateIndex("IX_AuditLogs_CreatedAt", "AuditLogs", "CreatedAt");
        migrationBuilder.CreateIndex("IX_AuditLogs_EntityType_EntityId", "AuditLogs", new[] { "EntityType", "EntityId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("AuditLogs");
}
