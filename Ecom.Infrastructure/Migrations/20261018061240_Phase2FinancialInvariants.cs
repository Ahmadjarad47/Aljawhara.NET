using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecom.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase2FinancialInvariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [Coupons] WHERE [UsedCount] < 0 OR [UsageLimit] < 0 OR ([UsageLimit] IS NOT NULL AND [UsedCount] > [UsageLimit]))
                    THROW 51001, 'Phase2 preflight failed: coupon usage data violates the new atomic usage invariant.', 1;
                IF EXISTS (SELECT 1 FROM [PaymentAttempts] WHERE [Amount] <= 0 OR [Currency] <> 'KWD')
                    THROW 51002, 'Phase2 preflight failed: payment attempt amount/currency data is invalid.', 1;
                IF EXISTS (SELECT 1 FROM [OutboxMessages] WHERE [AttemptCount] < 0)
                    THROW 51003, 'Phase2 preflight failed: outbox attempt count is negative.', 1;
                """);
            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentAttempts_Amount_Positive",
                table: "PaymentAttempts",
                sql: "[Amount] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentAttempts_Currency_KWD",
                table: "PaymentAttempts",
                sql: "[Currency] = 'KWD'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OutboxMessages_AttemptCount_NonNegative",
                table: "OutboxMessages",
                sql: "[AttemptCount] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Coupons_UsageLimit",
                table: "Coupons",
                sql: "[UsageLimit] IS NULL OR ([UsageLimit] >= 0 AND [UsedCount] <= [UsageLimit])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Coupons_UsedCount_NonNegative",
                table: "Coupons",
                sql: "[UsedCount] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentAttempts_Amount_Positive",
                table: "PaymentAttempts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentAttempts_Currency_KWD",
                table: "PaymentAttempts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OutboxMessages_AttemptCount_NonNegative",
                table: "OutboxMessages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Coupons_UsageLimit",
                table: "Coupons");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Coupons_UsedCount_NonNegative",
                table: "Coupons");
        }
    }
}
