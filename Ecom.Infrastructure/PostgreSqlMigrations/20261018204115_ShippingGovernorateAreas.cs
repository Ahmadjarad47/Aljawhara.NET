using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecom.Infrastructure.PostgreSqlMigrations;

public partial class ShippingGovernorateAreas : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_ShippingAreaRates_ShippingSettings_ShippingSettingsId", table: "ShippingAreaRates");
        migrationBuilder.DropPrimaryKey(name: "PK_ShippingAreaRates", table: "ShippingAreaRates");
        migrationBuilder.AddColumn<string>(name: "StateKey", table: "ShippingAreaRates", type: "character varying(100)",
            maxLength: 100, nullable: false, defaultValue: "مناطق غير مصنفة");
        migrationBuilder.CreateTable(
            name: "ShippingGovernorates",
            columns: table => new
            {
                ShippingSettingsId = table.Column<int>(type: "integer", nullable: false),
                StateKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                State = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ShippingGovernorates", x => new { x.ShippingSettingsId, x.StateKey });
                table.ForeignKey(name: "FK_ShippingGovernorates_ShippingSettings_ShippingSettingsId",
                    column: x => x.ShippingSettingsId, principalTable: "ShippingSettings", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            });
        migrationBuilder.InsertData(table: "ShippingGovernorates", columns: new[] { "ShippingSettingsId", "StateKey", "State" },
            values: new object[,]
            {
                { 1, "العاصمة", "محافظة العاصمة" },
                { 1, "حولي", "محافظة حولي" },
                { 1, "الفروانية", "محافظة الفروانية" },
                { 1, "الجهراء", "محافظة الجهراء" },
                { 1, "الأحمدي", "محافظة الأحمدي" },
                { 1, "مبارك الكبير", "محافظة مبارك الكبير" }
            });
        // Assign the existing rows in place. Never delete/reseed administrator-edited prices.
        migrationBuilder.Sql("""
            UPDATE "ShippingAreaRates"
            SET "StateKey" = CASE
                    WHEN "CityKey" IN ('مدينة الكويت', 'دسمان', 'شرق', 'الصوابر', 'المرقاب', 'القبلة', 'الصالحية', 'بنيد القار', 'الدسمة', 'الدوحة', 'الشامية', 'الشويخ', 'الصليبيخات', 'الروضة', 'الخالدية', 'العديلية', 'القادسية', 'الفيحاء', 'النزهة', 'قرطبة', 'غرناطة', 'مدينة جابر الأحمد') THEN 'العاصمة'
                    WHEN "CityKey" IN ('حولي', 'السالمية', 'الجابرية', 'مشرف', 'بيان', 'الرميثية', 'سلوى', 'الشعب', 'البدع', 'النقرة', 'الصديق', 'السلام', 'الزهراء', 'حطين') THEN 'حولي'
                    WHEN "CityKey" IN ('الفروانية', 'خيطان', 'جليب الشيوخ', 'أشبيلية', 'الأندلس', 'العباسية', 'الرابية', 'العمرية', 'العارضية', 'الرحاب', 'الرقعي', 'الفردوس', 'ضاحية صباح الناصر', 'ضاحية عبدالله المبارك') THEN 'الفروانية'
                    WHEN "CityKey" IN ('الجهراء', 'الواحة', 'القصر', 'النعيم', 'العيون', 'النسيم', 'تيماء', 'أمغرة', 'الصليبية', 'المطلاع', 'العبدلي', 'السالمي') THEN 'الجهراء'
                    WHEN "CityKey" IN ('الأحمدي', 'الفحيحيل', 'المنقف', 'المهبولة', 'الرقة', 'الصباحية', 'الفنطاس', 'أبو حليفة', 'ميناء عبدالله', 'الزور', 'الخيران', 'الوفرة', 'مدينة صباح الأحمد') THEN 'الأحمدي'
                    WHEN "CityKey" IN ('مبارك الكبير', 'صباح السالم', 'العدان', 'القرين', 'القصور', 'المسايل', 'الفنطيس', 'أبو فطيرة', 'صبحان') THEN 'مبارك الكبير'
                    ELSE 'مناطق غير مصنفة'
                END;
            INSERT INTO "ShippingGovernorates" ("ShippingSettingsId", "StateKey", "State")
            SELECT DISTINCT "ShippingSettingsId", 'مناطق غير مصنفة', 'مناطق غير مصنفة'
            FROM "ShippingAreaRates" WHERE "StateKey" = 'مناطق غير مصنفة';
            ALTER TABLE "ShippingAreaRates" ALTER COLUMN "StateKey" DROP DEFAULT;
            """);
        migrationBuilder.AddPrimaryKey(name: "PK_ShippingAreaRates", table: "ShippingAreaRates", columns: new[] { "ShippingSettingsId", "StateKey", "CityKey" });
        migrationBuilder.AddForeignKey(name: "FK_ShippingAreaRates_ShippingGovernorates_ShippingSettingsId_S~",
            table: "ShippingAreaRates", columns: new[] { "ShippingSettingsId", "StateKey" },
            principalTable: "ShippingGovernorates", principalColumns: new[] { "ShippingSettingsId", "StateKey" }, onDelete: ReferentialAction.Cascade);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The previous schema cannot represent the same area name in two governorates.
        migrationBuilder.Sql("""
            DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM "ShippingAreaRates" GROUP BY "ShippingSettingsId", "CityKey" HAVING COUNT(*) > 1) THEN
                    RAISE EXCEPTION 'Cannot downgrade shipping: duplicate area names exist across governorates.';
                END IF;
            END $$;
            """);
        migrationBuilder.DropForeignKey(name: "FK_ShippingAreaRates_ShippingGovernorates_ShippingSettingsId_S~", table: "ShippingAreaRates");
        migrationBuilder.DropPrimaryKey(name: "PK_ShippingAreaRates", table: "ShippingAreaRates");
        migrationBuilder.DropColumn(name: "StateKey", table: "ShippingAreaRates");
        migrationBuilder.DropTable(name: "ShippingGovernorates");
        migrationBuilder.AddPrimaryKey(name: "PK_ShippingAreaRates", table: "ShippingAreaRates", columns: new[] { "ShippingSettingsId", "CityKey" });
        migrationBuilder.AddForeignKey(name: "FK_ShippingAreaRates_ShippingSettings_ShippingSettingsId", table: "ShippingAreaRates", column: "ShippingSettingsId",
            principalTable: "ShippingSettings", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
    }
}

