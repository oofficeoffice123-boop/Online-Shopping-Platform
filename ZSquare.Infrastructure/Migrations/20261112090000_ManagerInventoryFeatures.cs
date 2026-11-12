using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ZSquare.Infrastructure;

#nullable disable

namespace ZSquare.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261112090000_ManagerInventoryFeatures")]
public partial class ManagerInventoryFeatures : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(name: "DiscountPercent", table: "ProductVariants", type: "decimal(5,2)", precision: 5, scale: 2, nullable: true);
        migrationBuilder.AddCheckConstraint(name: "CK_ProductVariants_DiscountPercent", table: "ProductVariants", sql: "[DiscountPercent] IS NULL OR ([DiscountPercent] >= 0 AND [DiscountPercent] <= 100)");
        migrationBuilder.CreateTable(name: "InventoryRestockLogs", columns: table => new
        {
            InventoryRestockLogId = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
            VariantId = table.Column<int>(type: "int", nullable: false),
            UserId = table.Column<int>(type: "int", nullable: false),
            QuantityAdded = table.Column<int>(type: "int", nullable: false),
            PreviousStock = table.Column<int>(type: "int", nullable: false),
            RestockedAt = table.Column<DateTime>(type: "datetime2", precision: 0, nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_InventoryRestockLogs", x => x.InventoryRestockLogId);
            table.ForeignKey("FK_InventoryRestockLogs_ProductVariants_VariantId", x => x.VariantId, "ProductVariants", "VariantId", onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_InventoryRestockLogs_Users_UserId", x => x.UserId, "Users", "UserId", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex(name: "IX_InventoryRestockLogs_RestockedAt", table: "InventoryRestockLogs", column: "RestockedAt");
        migrationBuilder.CreateIndex(name: "IX_InventoryRestockLogs_UserId", table: "InventoryRestockLogs", column: "UserId");
        migrationBuilder.CreateIndex(name: "IX_InventoryRestockLogs_VariantId", table: "InventoryRestockLogs", column: "VariantId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(name: "CK_ProductVariants_DiscountPercent", table: "ProductVariants");
        migrationBuilder.DropColumn(name: "DiscountPercent", table: "ProductVariants");
        migrationBuilder.DropTable(name: "InventoryRestockLogs");
    }
}
