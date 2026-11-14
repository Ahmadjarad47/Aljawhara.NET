using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecom.Infrastructure.PostgreSqlMigrations
{
    public partial class P2CatalogIntegrity : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing URLs and review rows have no verified ownership metadata.
            migrationBuilder.AddColumn<Guid>("ImageOwnerId", "Products", type: "uuid", nullable: true);
            migrationBuilder.AddColumn<string>("ReviewerUserId", "Ratings", type: "text", nullable: true);
            migrationBuilder.CreateIndex("IX_Products_ImageOwnerId", "Products", "ImageOwnerId", unique: true);
            migrationBuilder.CreateIndex("IX_Ratings_ReviewerUserId", "Ratings", "ReviewerUserId");
            migrationBuilder.CreateIndex("UX_Ratings_Product_Reviewer", "Ratings", new[] { "ProductId", "ReviewerUserId" },
                unique: true, filter: "\"ReviewerUserId\" IS NOT NULL");
            migrationBuilder.AddForeignKey("FK_Ratings_AspNetUsers_ReviewerUserId", "Ratings", "ReviewerUserId",
                "AspNetUsers", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey("FK_Ratings_AspNetUsers_ReviewerUserId", "Ratings");
            migrationBuilder.DropIndex("UX_Ratings_Product_Reviewer", "Ratings");
            migrationBuilder.DropIndex("IX_Ratings_ReviewerUserId", "Ratings");
            migrationBuilder.DropIndex("IX_Products_ImageOwnerId", "Products");
            migrationBuilder.DropColumn("ReviewerUserId", "Ratings");
            migrationBuilder.DropColumn("ImageOwnerId", "Products");
        }
    }
}
