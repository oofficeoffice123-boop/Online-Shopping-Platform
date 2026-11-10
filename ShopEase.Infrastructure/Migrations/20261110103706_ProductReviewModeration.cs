using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopEase.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProductReviewModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsHidden",
                table: "Reviews",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModeratedAt",
                table: "Reviews",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ModeratedByUserId",
                table: "Reviews",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ModeratedByUserId",
                table: "Reviews",
                column: "ModeratedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ProductId_IsHidden_CreatedAt",
                table: "Reviews",
                columns: new[] { "ProductId", "IsHidden", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Reviews_Users_ModeratedByUserId",
                table: "Reviews",
                column: "ModeratedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reviews_Users_ModeratedByUserId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_ModeratedByUserId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_ProductId_IsHidden_CreatedAt",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "IsHidden",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "ModeratedAt",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "ModeratedByUserId",
                table: "Reviews");
        }
    }
}
