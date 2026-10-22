using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Ecom.Infrastructure.PostgreSqlMigrations
{
    /// <inheritdoc />
    public partial class WhatsAppPreparationRetryAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WhatsAppInvoiceRetryAudits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceId = table.Column<int>(type: "integer", nullable: false),
                    OrderId = table.Column<int>(type: "integer", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    PreviousAttemptCount = table.Column<int>(type: "integer", nullable: false),
                    PreviousErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PdfSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PdfValidatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppInvoiceRetryAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WhatsAppInvoiceRetryAudits_WhatsAppInvoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "WhatsAppInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppInvoiceRetryAudits_InvoiceId",
                table: "WhatsAppInvoiceRetryAudits",
                column: "InvoiceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppInvoiceRetryAudits_RequestId",
                table: "WhatsAppInvoiceRetryAudits",
                column: "RequestId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WhatsAppInvoiceRetryAudits");
        }
    }
}
