using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecom.Infrastructure.PostgreSqlMigrations
{
    /// <inheritdoc />
    public partial class P1AccountAndPaymentSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Legacy bearer material is deliberately not imported. Clients sign in
            // again after cutover; account, password and commerce records are retained.
            migrationBuilder.DropColumn(
                name: "RefreshToken",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "RefreshTokenExpiresAtUtc",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<DateTime>(
                name: "PublicVerificationRequestedAtUtc",
                table: "PaymentAttempts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "Coupons",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AccountChallenges",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Purpose = table.Column<int>(type: "integer", nullable: false),
                    CodeHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastIssuedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WindowStartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IssuedCount = table.Column<int>(type: "integer", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    ConsumedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountChallenges", x => new { x.UserId, x.Purpose });
                    table.CheckConstraint("CK_AccountChallenges_Attempts", "\"Attempts\" BETWEEN 0 AND 5");
                    table.CheckConstraint("CK_AccountChallenges_IssuedCount", "\"IssuedCount\" BETWEEN 1 AND 3");
                    table.CheckConstraint("CK_AccountChallenges_Purpose", "\"Purpose\" IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_AccountChallenges_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuthSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    SecurityStamp = table.Column<string>(type: "text", nullable: false),
                    RefreshTokenHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuthSessions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAttempts_PublicVerificationRequestedAtUtc",
                table: "PaymentAttempts",
                column: "PublicVerificationRequestedAtUtc",
                filter: "\"PublicVerificationRequestedAtUtc\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Coupons_PublicUnassigned",
                table: "Coupons",
                sql: "NOT \"IsPublic\" OR \"AppUserId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AuthSessions_ExpiresAtUtc",
                table: "AuthSessions",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AuthSessions_RefreshTokenHash",
                table: "AuthSessions",
                column: "RefreshTokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuthSessions_UserId",
                table: "AuthSessions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountChallenges");

            migrationBuilder.DropTable(
                name: "AuthSessions");

            migrationBuilder.DropIndex(
                name: "IX_PaymentAttempts_PublicVerificationRequestedAtUtc",
                table: "PaymentAttempts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Coupons_PublicUnassigned",
                table: "Coupons");

            migrationBuilder.DropColumn(
                name: "PublicVerificationRequestedAtUtc",
                table: "PaymentAttempts");

            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "Coupons");

            migrationBuilder.AddColumn<string>(
                name: "RefreshToken",
                table: "AspNetUsers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefreshTokenExpiresAtUtc",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}
