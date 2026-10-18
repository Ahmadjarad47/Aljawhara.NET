using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecom.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase3SadadVerificationEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GatewayTransactionId",
                table: "PaymentAttempts",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastVerificationAtUtc",
                table: "PaymentAttempts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationEvidenceId",
                table: "PaymentAttempts",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VerifiedAmount",
                table: "PaymentAttempts",
                type: "decimal(18,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VerifiedRefundedAmount",
                table: "PaymentAttempts",
                type: "decimal(18,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VerifiedSubtotal",
                table: "PaymentAttempts",
                type: "decimal(18,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VerifiedTotal",
                table: "PaymentAttempts",
                type: "decimal(18,3)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GatewayTransactionId",
                table: "PaymentAttempts");

            migrationBuilder.DropColumn(
                name: "LastVerificationAtUtc",
                table: "PaymentAttempts");

            migrationBuilder.DropColumn(
                name: "VerificationEvidenceId",
                table: "PaymentAttempts");

            migrationBuilder.DropColumn(
                name: "VerifiedAmount",
                table: "PaymentAttempts");

            migrationBuilder.DropColumn(
                name: "VerifiedRefundedAmount",
                table: "PaymentAttempts");

            migrationBuilder.DropColumn(
                name: "VerifiedSubtotal",
                table: "PaymentAttempts");

            migrationBuilder.DropColumn(
                name: "VerifiedTotal",
                table: "PaymentAttempts");
        }
    }
}
