using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Ecom.Infrastructure.PostgreSqlMigrations
{
    /// <inheritdoc />
    public partial class DynamicShippingSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShippingSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    DefaultFee = table.Column<decimal>(type: "numeric(18,3)", nullable: false),
                    FreeShippingThreshold = table.Column<decimal>(type: "numeric(18,3)", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShippingSettings", x => x.Id);
                    table.CheckConstraint("CK_ShippingSettings_DefaultFee", "\"DefaultFee\" BETWEEN 0 AND 1000000");
                    table.CheckConstraint("CK_ShippingSettings_Singleton", "\"Id\" = 1");
                    table.CheckConstraint("CK_ShippingSettings_Threshold", "\"FreeShippingThreshold\" IS NULL OR \"FreeShippingThreshold\" BETWEEN 0 AND 1000000");
                    table.CheckConstraint("CK_ShippingSettings_Version", "\"Version\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "ShippingAreaRates",
                columns: table => new
                {
                    ShippingSettingsId = table.Column<int>(type: "integer", nullable: false),
                    CityKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Fee = table.Column<decimal>(type: "numeric(18,3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShippingAreaRates", x => new { x.ShippingSettingsId, x.CityKey });
                    table.CheckConstraint("CK_ShippingAreaRates_Fee", "\"Fee\" BETWEEN 0 AND 1000000");
                    table.ForeignKey(
                        name: "FK_ShippingAreaRates_ShippingSettings_ShippingSettingsId",
                        column: x => x.ShippingSettingsId,
                        principalTable: "ShippingSettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "ShippingSettings",
                columns: new[] { "Id", "DefaultFee", "FreeShippingThreshold", "Version" },
                values: new object[] { 1, 2m, 20m, 1L });

            migrationBuilder.InsertData(
                table: "ShippingAreaRates",
                columns: new[] { "CityKey", "ShippingSettingsId", "City", "Fee" },
                values: new object[,]
                {
                    { "الأحمدي", 1, "الأحمدي", 3m },
                    { "الخيران", 1, "الخيران", 6m },
                    { "العبدلي", 1, "العبدلي", 6m },
                    { "المطلاع", 1, "المطلاع", 4m },
                    { "الوفرة", 1, "الوفرة", 6m },
                    { "صباح السالم", 1, "صباح السالم", 3m },
                    { "مدينة صباح الأحمد", 1, "مدينة صباح الأحمد", 6m }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShippingAreaRates");

            migrationBuilder.DropTable(
                name: "ShippingSettings");
        }
    }
}
