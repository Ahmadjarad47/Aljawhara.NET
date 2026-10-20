using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Ecom.Infrastructure.PostgreSqlMigrations
{
    /// <inheritdoc />
    public partial class WhatsAppInvoicePdfOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "WhatsAppConsentAtUtc",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppConsentPhone",
                table: "Orders",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WhatsAppDeliveryEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EventKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AppName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProviderMessageId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Destination = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    WhatsAppMessageId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppDeliveryEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppInvoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PaymentAttemptId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Destination = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    AppName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProviderMessageId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    WhatsAppMessageId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Pdf = table.Column<byte[]>(type: "bytea", nullable: true),
                    MediaTokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    MediaExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LeaseOwner = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LeaseUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DispatchStartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastEventAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReconciliationEvidence = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WhatsAppInvoices_PaymentAttempts_PaymentAttemptId",
                        column: x => x.PaymentAttemptId,
                        principalTable: "PaymentAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppDeliveryEvents_EventKey",
                table: "WhatsAppDeliveryEvents",
                column: "EventKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppDeliveryEvents_ProviderMessageId",
                table: "WhatsAppDeliveryEvents",
                column: "ProviderMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppInvoices_PaymentAttemptId",
                table: "WhatsAppInvoices",
                column: "PaymentAttemptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppInvoices_ProviderMessageId",
                table: "WhatsAppInvoices",
                column: "ProviderMessageId",
                unique: true,
                filter: "\"ProviderMessageId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppInvoices_Status_NextAttemptAtUtc",
                table: "WhatsAppInvoices",
                columns: new[] { "Status", "NextAttemptAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WhatsAppDeliveryEvents");

            migrationBuilder.DropTable(
                name: "WhatsAppInvoices");

            migrationBuilder.DropColumn(
                name: "WhatsAppConsentAtUtc",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "WhatsAppConsentPhone",
                table: "Orders");
        }
    }
}
