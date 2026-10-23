using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Ecom.AIShopping.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialAIShopping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ai_shopping");

            migrationBuilder.CreateTable(
                name: "ConfirmationReceipts",
                schema: "ai_shopping",
                columns: table => new
                {
                    RecommendationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfirmationReceipts", x => x.RecommendationId);
                });

            migrationBuilder.CreateTable(
                name: "Conversations",
                schema: "ai_shopping",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IndexCheckpoints",
                schema: "ai_shopping",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LastProductId = table.Column<int>(type: "integer", nullable: false),
                    LastSweepAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndexCheckpoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SearchDocuments",
                schema: "ai_shopping",
                columns: table => new
                {
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    NormalizedText = table.Column<string>(type: "text", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EmbeddingModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IndexedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastErrorCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchDocuments", x => x.ProductId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConfirmationReceipts_ExpiresAtUtc",
                schema: "ai_shopping",
                table: "ConfirmationReceipts",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_ExpiresAtUtc",
                schema: "ai_shopping",
                table: "Conversations",
                column: "ExpiresAtUtc");

            migrationBuilder.Sql("CREATE INDEX \"IX_SearchDocuments_FTS\" ON ai_shopping.\"SearchDocuments\" USING GIN (to_tsvector('simple', \"NormalizedText\"));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfirmationReceipts",
                schema: "ai_shopping");

            migrationBuilder.DropTable(
                name: "Conversations",
                schema: "ai_shopping");

            migrationBuilder.DropTable(
                name: "IndexCheckpoints",
                schema: "ai_shopping");

            migrationBuilder.DropTable(
                name: "SearchDocuments",
                schema: "ai_shopping");
        }
    }
}
