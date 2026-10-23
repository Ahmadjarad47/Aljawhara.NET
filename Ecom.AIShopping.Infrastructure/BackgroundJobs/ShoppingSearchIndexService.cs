using System.Security.Cryptography;
using System.Text;
using Ecom.AIShopping.Abstractions;
using Ecom.AIShopping.Infrastructure.DependencyInjection;
using Ecom.AIShopping.Infrastructure.Persistence;
using Ecom.AIShopping.Infrastructure.ProductCatalog;
using Ecom.AIShopping.Infrastructure.Retrieval;
using Ecom.Domain.Entity;
using Ecom.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Ecom.AIShopping.Infrastructure.BackgroundJobs;

public sealed record IndexBatchResult(int Scanned, int Changed, int Embedded, int Failed, bool SweepCompleted);

public sealed class ShoppingSearchIndexService(EcomDbContext commerce, AIShoppingDbContext db, IEmbeddingService embeddings,
    IOptions<AIShoppingOptions> options, ILogger<ShoppingSearchIndexService> logger)
{
    public async Task<IndexBatchResult> RunBatchAsync(bool resetCheckpoint, CancellationToken ct)
    {
        var o = options.Value;
        await db.Database.OpenConnectionAsync(ct);
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await using var claim = new NpgsqlCommand("SELECT pg_try_advisory_lock(916027135)", connection);
        if (!Equals(await claim.ExecuteScalarAsync(ct), true)) return new(0, 0, 0, 0, false);
        try
        {
            var checkpoint = await db.IndexCheckpoints.SingleOrDefaultAsync(x => x.Id == 1, ct);
            if (checkpoint is null) { checkpoint = new() { Id = 1 }; db.IndexCheckpoints.Add(checkpoint); }
            if (resetCheckpoint) checkpoint.LastProductId = 0;
            var rows = await commerce.Products.IgnoreQueryFilters().AsNoTracking().AsSplitQuery()
                .Where(p => p.Id > checkpoint.LastProductId).OrderBy(p => p.Id).Take(o.IndexBatchSize)
                .Include(p => p.subCategory).ThenInclude(s => s.Category)
                .Include(p => p.productDetails).Include(p => p.Variants).ThenInclude(v => v.Values).ToListAsync(ct);
            int changed = 0, embedded = 0, failed = 0;
            foreach (var p in rows)
            {
                var doc = await db.SearchDocuments.SingleOrDefaultAsync(x => x.ProductId == p.Id, ct);
                if (p.IsDeleted || !p.IsActive || p.subCategory.IsDeleted || !p.subCategory.IsActive ||
                    p.subCategory.Category.IsDeleted || !p.subCategory.Category.IsActive)
                {
                    if (doc != null) { db.SearchDocuments.Remove(doc); changed++; }
                    continue;
                }
                var text = BuildDocument(p);
                var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
                if (doc is null) { doc = new() { ProductId = p.Id }; db.SearchDocuments.Add(doc); }
                if (doc.ContentHash != hash)
                {
                    doc.Text = text; doc.NormalizedText = ArabicSearch.Normalize(text); doc.ContentHash = hash;
                    doc.EmbeddingModel = null; doc.IndexedAtUtc = null; doc.NextAttemptAtUtc = DateTime.UtcNow;
                    doc.Attempts = 0; changed++;
                }
                // Lexical content survives provider outages. The projection contains NO prices or stock quantities.
                await db.SaveChangesAsync(ct);
                if (o.SemanticEnabled && (doc.EmbeddingModel != o.EmbeddingModel || doc.IndexedAtUtc == null) &&
                    doc.NextAttemptAtUtc <= DateTime.UtcNow)
                {
                    try
                    {
                        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        timeout.CancelAfter(TimeSpan.FromSeconds(o.TimeoutSeconds));
                        var vector = await embeddings.EmbedAsync(text, timeout.Token);
                        var literal = PostgresProductRetrieval.VectorLiteral(vector);
                        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE ai_shopping.\"SearchDocuments\" SET \"Embedding\" = CAST({literal} AS vector) WHERE \"ProductId\" = {p.Id}", ct);
                        doc.EmbeddingModel = o.EmbeddingModel; doc.IndexedAtUtc = DateTime.UtcNow;
                        doc.Attempts = 0; doc.LastErrorCode = null; embedded++;
                    }
                    catch (Exception ex) when (!ct.IsCancellationRequested && ex is not OutOfMemoryException)
                    {
                        doc.Attempts = Math.Min(doc.Attempts + 1, 20);
                        doc.NextAttemptAtUtc = DateTime.UtcNow.AddSeconds(Math.Min(3600, Math.Pow(2, doc.Attempts) * 15) + Random.Shared.Next(1, 15));
                        doc.LastErrorCode = "INDEX_EMBEDDING_FAILED"; failed++;
                    }
                }
                await db.SaveChangesAsync(ct);
            }
            var complete = rows.Count < o.IndexBatchSize;
            checkpoint.LastProductId = complete ? 0 : rows[^1].Id;
            if (complete) checkpoint.LastSweepAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            if (complete)
            {
                // Remove hard-deleted products and expire private state. Normal deployments use soft delete.
                await db.Database.ExecuteSqlRawAsync("DELETE FROM ai_shopping.\"SearchDocuments\" d WHERE NOT EXISTS (SELECT 1 FROM public.\"Products\" p WHERE p.\"Id\" = d.\"ProductId\")", ct);
                await db.Conversations.Where(c => c.ExpiresAtUtc <= DateTime.UtcNow).ExecuteDeleteAsync(ct);
                await db.ConfirmationReceipts.Where(c => c.ExpiresAtUtc <= DateTime.UtcNow).ExecuteDeleteAsync(ct);
            }
            logger.LogInformation("AI index batch. Scanned={Scanned}, Changed={Changed}, Embedded={Embedded}, Failed={Failed}, Completed={Completed}", rows.Count, changed, embedded, failed, complete);
            return new(rows.Count, changed, embedded, failed, complete);
        }
        finally
        {
            await using var release = new NpgsqlCommand("SELECT pg_advisory_unlock(916027135)", connection);
            await release.ExecuteScalarAsync(CancellationToken.None);
            await db.Database.CloseConnectionAsync();
        }
    }

    public static string BuildDocument(Product p)
    {
        var details = p.productDetails.Where(d => d.IsActive && !d.IsDeleted).OrderBy(d => d.Id)
            .Select(d => $"{d.Label} {d.LabelAr}: {d.Value} {d.ValueAr}");
        var variants = p.Variants.Where(v => v.IsActive && !v.IsDeleted).OrderBy(v => v.Id).Select(v =>
            $"{v.Name} {v.NameAr}: {string.Join(' ', v.Values.Where(x => x.IsActive && !x.IsDeleted).OrderBy(x => x.Id).Select(x => x.Value + " " + x.ValueAr))}");
        var text = string.Join('\n', new[] { p.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            p.Title, p.TitleAr, p.Description, p.DescriptionAr, p.subCategory.Name, p.subCategory.NameAr,
            p.subCategory.Category.Name, p.subCategory.Category.NameAr }.Concat(details).Concat(variants));
        // UTF-8 bytes conservatively bound embedding tokens, including long Arabic descriptions.
        var bounded = new StringBuilder(); var bytes = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (bytes + rune.Utf8SequenceLength > 8000) break;
            bounded.Append(rune.ToString()); bytes += rune.Utf8SequenceLength;
        }
        return bounded.ToString();
    }
}
