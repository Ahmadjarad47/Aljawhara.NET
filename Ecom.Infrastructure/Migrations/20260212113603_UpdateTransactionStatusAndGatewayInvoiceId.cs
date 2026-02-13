using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecom.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTransactionStatusAndGatewayInvoiceId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add GatewayInvoiceId column
            migrationBuilder.AddColumn<string>(
                name: "GatewayInvoiceId",
                table: "Transactions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            // Add temporary int column for Status
            migrationBuilder.AddColumn<int>(
                name: "StatusNew",
                table: "Transactions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Migrate Status: Completed->1(Paid), Pending->0, Failed->2, Refunded->4, Cancelled->3
            migrationBuilder.Sql(@"
                UPDATE Transactions SET StatusNew = CASE 
                    WHEN Status = 'Completed' THEN 1
                    WHEN Status = 'Paid' THEN 1
                    WHEN Status = 'Pending' THEN 0
                    WHEN Status = 'Failed' THEN 2
                    WHEN Status = 'Refunded' THEN 4
                    WHEN Status = 'Cancelled' THEN 3
                    ELSE 0
                END");

            migrationBuilder.DropColumn(name: "Status", table: "Transactions");
            migrationBuilder.RenameColumn(name: "StatusNew", table: "Transactions", newName: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GatewayInvoiceId",
                table: "Transactions");

            // Revert Status to string
            migrationBuilder.AddColumn<string>(
                name: "StatusOld",
                table: "Transactions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.Sql(@"
                UPDATE Transactions SET StatusOld = CASE 
                    WHEN Status = 1 THEN 'Completed'
                    WHEN Status = 0 THEN 'Pending'
                    WHEN Status = 2 THEN 'Failed'
                    WHEN Status = 4 THEN 'Refunded'
                    WHEN Status = 3 THEN 'Cancelled'
                    ELSE 'Pending'
                END");

            migrationBuilder.DropColumn(name: "Status", table: "Transactions");
            migrationBuilder.RenameColumn(name: "StatusOld", table: "Transactions", newName: "Status");
        }
    }
}
