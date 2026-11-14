using Microsoft.EntityFrameworkCore.Migrations;

namespace Ecom.Infrastructure.PostgreSqlMigrations;

public partial class AllowMultipleCustomerShippingAddresses : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_ShippingAddresses_AppUserId", "ShippingAddresses");
        migrationBuilder.CreateIndex("IX_ShippingAddresses_AppUserId", "ShippingAddresses", "AppUserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // If a customer now has several addresses, PostgreSQL rejects the rollback
        // and restores the previous index as part of the migration transaction.
        migrationBuilder.DropIndex("IX_ShippingAddresses_AppUserId", "ShippingAddresses");
        migrationBuilder.CreateIndex("IX_ShippingAddresses_AppUserId", "ShippingAddresses", "AppUserId", unique: true);
    }
}
