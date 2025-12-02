using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecom.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class updateshippingaddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AlDor",
                table: "ShippingAddresses",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AlJada",
                table: "ShippingAddresses",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AlManzil",
                table: "ShippingAddresses",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AlQataa",
                table: "ShippingAddresses",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AlShakka",
                table: "ShippingAddresses",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AlSharee",
                table: "ShippingAddresses",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AlDor",
                table: "ShippingAddresses");

            migrationBuilder.DropColumn(
                name: "AlJada",
                table: "ShippingAddresses");

            migrationBuilder.DropColumn(
                name: "AlManzil",
                table: "ShippingAddresses");

            migrationBuilder.DropColumn(
                name: "AlQataa",
                table: "ShippingAddresses");

            migrationBuilder.DropColumn(
                name: "AlShakka",
                table: "ShippingAddresses");

            migrationBuilder.DropColumn(
                name: "AlSharee",
                table: "ShippingAddresses");
        }
    }
}
