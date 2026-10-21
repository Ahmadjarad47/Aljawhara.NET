using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecom.Infrastructure.PostgreSqlMigrations
{
    /// <inheritdoc />
    public partial class InternationalWhatsAppPhones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "WhatsAppPhoneVerifications",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(15)",
                oldMaxLength: 15);

            migrationBuilder.AlterColumn<string>(
                name: "Destination",
                table: "WhatsAppInvoices",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(15)",
                oldMaxLength: 15,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Destination",
                table: "WhatsAppDeliveryEvents",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(15)",
                oldMaxLength: 15);

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "WhatsAppConsentAudits",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(15)",
                oldMaxLength: 15);

            migrationBuilder.AlterColumn<string>(
                name: "WhatsAppConsentPhone",
                table: "AspNetUsers",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(15)",
                oldMaxLength: 15,
                oldNullable: true);

            // Existing consent rows contain normalized international digits. Preserve their meaning; never create consent.
            migrationBuilder.Sql("""UPDATE "WhatsAppPhoneVerifications" SET "Phone" = '+' || "Phone" WHERE "Phone" ~ '^[1-9][0-9]{7,14}$';""");
            migrationBuilder.Sql("""UPDATE "WhatsAppInvoices" SET "Destination" = '+' || "Destination" WHERE "Destination" ~ '^[1-9][0-9]{7,14}$';""");
            migrationBuilder.Sql("""UPDATE "WhatsAppDeliveryEvents" SET "Destination" = '+' || "Destination" WHERE "Destination" ~ '^[1-9][0-9]{7,14}$';""");
            migrationBuilder.Sql("""UPDATE "WhatsAppConsentAudits" SET "Phone" = '+' || "Phone" WHERE "Phone" ~ '^[1-9][0-9]{7,14}$';""");
            migrationBuilder.Sql("""UPDATE "AspNetUsers" SET "WhatsAppConsentPhone" = '+' || "WhatsAppConsentPhone" WHERE "WhatsAppConsentPhone" ~ '^[1-9][0-9]{7,14}$';""");
            migrationBuilder.Sql("""UPDATE "Orders" SET "WhatsAppConsentPhone" = '+' || "WhatsAppConsentPhone" WHERE "WhatsAppConsentPhone" ~ '^[1-9][0-9]{7,14}$';""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""UPDATE "WhatsAppPhoneVerifications" SET "Phone" = substring("Phone" from 2) WHERE "Phone" ~ '^[+][1-9][0-9]{7,14}$';""");
            migrationBuilder.Sql("""UPDATE "WhatsAppInvoices" SET "Destination" = substring("Destination" from 2) WHERE "Destination" ~ '^[+][1-9][0-9]{7,14}$';""");
            migrationBuilder.Sql("""UPDATE "WhatsAppDeliveryEvents" SET "Destination" = substring("Destination" from 2) WHERE "Destination" ~ '^[+][1-9][0-9]{7,14}$';""");
            migrationBuilder.Sql("""UPDATE "WhatsAppConsentAudits" SET "Phone" = substring("Phone" from 2) WHERE "Phone" ~ '^[+][1-9][0-9]{7,14}$';""");
            migrationBuilder.Sql("""UPDATE "AspNetUsers" SET "WhatsAppConsentPhone" = substring("WhatsAppConsentPhone" from 2) WHERE "WhatsAppConsentPhone" ~ '^[+][1-9][0-9]{7,14}$';""");
            migrationBuilder.Sql("""UPDATE "Orders" SET "WhatsAppConsentPhone" = substring("WhatsAppConsentPhone" from 2) WHERE "WhatsAppConsentPhone" ~ '^[+][1-9][0-9]{7,14}$';""");

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "WhatsAppPhoneVerifications",
                type: "character varying(15)",
                maxLength: 15,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<string>(
                name: "Destination",
                table: "WhatsAppInvoices",
                type: "character varying(15)",
                maxLength: 15,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Destination",
                table: "WhatsAppDeliveryEvents",
                type: "character varying(15)",
                maxLength: 15,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "WhatsAppConsentAudits",
                type: "character varying(15)",
                maxLength: 15,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<string>(
                name: "WhatsAppConsentPhone",
                table: "AspNetUsers",
                type: "character varying(15)",
                maxLength: 15,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16,
                oldNullable: true);
        }
    }
}
