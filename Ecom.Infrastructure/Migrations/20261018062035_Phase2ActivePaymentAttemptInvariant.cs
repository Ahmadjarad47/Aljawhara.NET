using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecom.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase2ActivePaymentAttemptInvariant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT [OrderId] FROM [PaymentAttempts]
                    WHERE [Status] IN (0, 1, 2, 3)
                    GROUP BY [OrderId] HAVING COUNT(*) > 1)
                    THROW 51004, 'Phase2 preflight failed: an order has multiple active or unreconciled payment attempts.', 1;
                """);
            migrationBuilder.DropIndex(
                name: "IX_PaymentAttempts_OrderId",
                table: "PaymentAttempts");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAttempts_OrderId",
                table: "PaymentAttempts",
                column: "OrderId",
                unique: true,
                filter: "[Status] IN (0, 1, 2, 3)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PaymentAttempts_OrderId",
                table: "PaymentAttempts");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAttempts_OrderId",
                table: "PaymentAttempts",
                column: "OrderId");
        }
    }
}
