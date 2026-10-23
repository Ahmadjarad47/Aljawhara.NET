using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ecom.AIShopping.Infrastructure.Persistence;

public sealed class AIShoppingDbContext(DbContextOptions<AIShoppingDbContext> options) : DbContext(options)
{
    public DbSet<ConversationRow> Conversations => Set<ConversationRow>();
    public DbSet<SearchDocument> SearchDocuments => Set<SearchDocument>();
    public DbSet<IndexCheckpoint> IndexCheckpoints => Set<IndexCheckpoint>();
    public DbSet<ConfirmationReceipt> ConfirmationReceipts => Set<ConfirmationReceipt>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema("ai_shopping");
        b.Entity<ConversationRow>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.OwnerHash).HasMaxLength(64);
            e.Property(x => x.Payload).HasColumnType("jsonb");
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasIndex(x => x.ExpiresAtUtc);
        });
        b.Entity<SearchDocument>(e =>
        {
            e.HasKey(x => x.ProductId);
            e.Property(x => x.ProductId).ValueGeneratedNever();
            e.Property(x => x.ContentHash).HasMaxLength(64);
            e.Property(x => x.EmbeddingModel).HasMaxLength(100);
            e.Property(x => x.LastErrorCode).HasMaxLength(80);
        });
        b.Entity<IndexCheckpoint>().HasKey(x => x.Id);
        b.Entity<ConfirmationReceipt>(e =>
        {
            e.HasKey(x => x.RecommendationId);
            e.Property(x => x.OwnerHash).HasMaxLength(64);
            e.Property(x => x.Payload).HasColumnType("jsonb");
            e.HasIndex(x => x.ExpiresAtUtc);
        });
    }
}

public sealed class ConversationRow
{
    public Guid Id { get; set; }
    public string OwnerHash { get; set; } = "";
    public int Version { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public string Payload { get; set; } = "{}";
}

public sealed class SearchDocument
{
    public int ProductId { get; set; }
    public string Text { get; set; } = "";
    public string NormalizedText { get; set; } = "";
    public string ContentHash { get; set; } = "";
    public string? EmbeddingModel { get; set; }
    public DateTime? IndexedAtUtc { get; set; }
    public int Attempts { get; set; }
    public DateTime NextAttemptAtUtc { get; set; }
    public string? LastErrorCode { get; set; }
}

public sealed class IndexCheckpoint
{
    public int Id { get; set; }
    public int LastProductId { get; set; }
    public DateTime LastSweepAtUtc { get; set; }
}

public sealed class ConfirmationReceipt
{
    public Guid RecommendationId { get; set; }
    public Guid ConversationId { get; set; }
    public string OwnerHash { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
    public string Payload { get; set; } = "{}";
}

public sealed class AIShoppingDbContextFactory : IDesignTimeDbContextFactory<AIShoppingDbContext>
{
    public AIShoppingDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? throw new InvalidOperationException("Set ConnectionStrings__DefaultConnection for explicit AI migration tooling.");
        return new(new DbContextOptionsBuilder<AIShoppingDbContext>().UseNpgsql(connection,
            o => o.MigrationsHistoryTable("__EFMigrationsHistory", "ai_shopping")).Options);
    }
}
