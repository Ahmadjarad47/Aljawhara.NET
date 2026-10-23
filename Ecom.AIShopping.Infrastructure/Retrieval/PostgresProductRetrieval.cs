using System.Globalization;
using Ecom.AIShopping.Abstractions;
using Ecom.AIShopping.Contracts;
using Ecom.AIShopping.Infrastructure.DependencyInjection;
using Ecom.AIShopping.Validation;
using Ecom.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Ecom.AIShopping.Infrastructure.Retrieval;

public sealed class PostgresProductRetrieval(EcomDbContext commerce, IProductCatalogReader catalog,
    IEmbeddingService embeddings, IOptions<AIShoppingOptions> options, ILogger<PostgresProductRetrieval> logger)
    : IProductRetrievalService
{
    private bool _semanticUnavailable;
    public async Task<IReadOnlyList<CatalogProduct>> SearchAsync(string query, string? material, int limit, CancellationToken ct)
    {
        if (!ShoppingValidation.Text(query, 160) || material?.Length > 80 || limit is < 1 or > 12)
            throw new ShoppingException("INVALID_SEARCH_ARGUMENTS");
        var expanded = ArabicSearch.Expand(query);
        var tsquery = string.Join(" OR ", expanded.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(40));
        await using var connection = new NpgsqlConnection(commerce.Database.GetConnectionString());
        await connection.OpenAsync(ct);
        var lexical = new List<int>();
        // All dynamic data is bound. FTS uses simple tokenization for Arabic, with normalized indexed text.
        await using (var command = new NpgsqlCommand("""
            SELECT d."ProductId" FROM ai_shopping."SearchDocuments" d
            JOIN public."Products" p ON p."Id" = d."ProductId"
            WHERE p."IsActive" AND NOT p."IsDeleted" AND p."IsInStock" AND p."TotalInStock" > 0
              AND (to_tsvector('simple', d."NormalizedText") @@ websearch_to_tsquery('simple', @q)
                   OR strpos(d."NormalizedText", @exact) > 0 OR d."ProductId"::text = @exact)
            ORDER BY (d."ProductId"::text = @exact) DESC, (strpos(d."NormalizedText", @exact) > 0) DESC,
                ts_rank_cd(to_tsvector('simple', d."NormalizedText"), websearch_to_tsquery('simple', @q)) DESC, d."ProductId"
            LIMIT @limit
            """, connection))
        {
            command.Parameters.AddWithValue("q", tsquery);
            command.Parameters.AddWithValue("exact", ArabicSearch.Normalize(query));
            command.Parameters.AddWithValue("limit", options.Value.CandidateLimit);
            command.CommandTimeout = 5;
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) lexical.Add(reader.GetInt32(0));
        }
        if (options.Value.TrigramEnabled)
        {
            await using var command = new NpgsqlCommand("""
                SELECT d."ProductId" FROM ai_shopping."SearchDocuments" d
                JOIN public."Products" p ON p."Id" = d."ProductId"
                WHERE p."IsActive" AND NOT p."IsDeleted" AND p."IsInStock" AND p."TotalInStock" > 0
                    AND word_similarity(@query, translate(d."NormalizedText", @alphabet, @ascii)) >= @similarity
                ORDER BY word_similarity(@query, translate(d."NormalizedText", @alphabet, @ascii)) DESC, d."ProductId" LIMIT @limit
                """, connection);
            command.Parameters.AddWithValue("query", ArabicSearch.TrigramKey(query));
            command.Parameters.AddWithValue("alphabet", ArabicSearch.TrigramAlphabet);
            command.Parameters.AddWithValue("ascii", ArabicSearch.TrigramAscii);
            command.Parameters.AddWithValue("similarity", options.Value.MinimumTrigramSimilarity);
            command.Parameters.AddWithValue("limit", options.Value.CandidateLimit);
            command.CommandTimeout = 5;
            var typoCandidates = new List<int>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) typoCandidates.Add(reader.GetInt32(0));
            lexical = ArabicSearch.Fuse(lexical, typoCandidates, options.Value.CandidateLimit).ToList();
        }
        var semantic = new List<int>();
        if (options.Value.SemanticEnabled && !_semanticUnavailable)
        {
            try
            {
                using var semanticTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                semanticTimeout.CancelAfter(TimeSpan.FromSeconds(5));
                var vector = await embeddings.EmbedAsync(ArabicSearch.Normalize(query), semanticTimeout.Token);
                var vectorLiteral = VectorLiteral(vector);
                await using var command = new NpgsqlCommand("""
                    SELECT d."ProductId" FROM ai_shopping."SearchDocuments" d
                    JOIN public."Products" p ON p."Id" = d."ProductId"
                    WHERE d."Embedding" IS NOT NULL AND d."EmbeddingModel" = @model
                        AND p."IsActive" AND NOT p."IsDeleted" AND p."IsInStock" AND p."TotalInStock" > 0
                        AND d."Embedding" <=> CAST(@vector AS vector) < @distance
                    ORDER BY d."Embedding" <=> CAST(@vector AS vector), d."ProductId" LIMIT @limit
                    """, connection);
                command.Parameters.AddWithValue("vector", vectorLiteral);
                command.Parameters.AddWithValue("model", options.Value.EmbeddingModel);
                command.Parameters.AddWithValue("limit", options.Value.CandidateLimit);
                command.Parameters.AddWithValue("distance", options.Value.MaximumCosineDistance);
                command.CommandTimeout = 5;
                await using var reader = await command.ExecuteReaderAsync(semanticTimeout.Token);
                while (await reader.ReadAsync(semanticTimeout.Token)) semantic.Add(reader.GetInt32(0));
            }
            catch (Exception ex) when (!ct.IsCancellationRequested && ex is not OutOfMemoryException)
            { _semanticUnavailable = true; logger.LogWarning("AI semantic retrieval unavailable; lexical fallback used for this request. Code=SEMANTIC_UNAVAILABLE"); }
        }
        var ids = ArabicSearch.Fuse(lexical, semantic, options.Value.CandidateLimit);
        var products = await catalog.GetAsync(ids, ct);
        return ids.Select(id => products.FirstOrDefault(p => p.ProductId == id))
            .OfType<CatalogProduct>().Where(p => !p.RequiresVariant &&
                (int.TryParse(query, out var exactId) && exactId == p.ProductId || ArabicSearch.Suitable(query, p.Name, p.NameEn, p.Category)) &&
                (string.IsNullOrWhiteSpace(material) ||
                 (p.Material != null && ArabicSearch.Material(p.Material) == ArabicSearch.Material(material))))
            .Take(limit).ToArray();
    }

    public static string VectorLiteral(float[] vector)
    {
        if (vector.Length != 1536 || vector.Any(x => !float.IsFinite(x)) || !vector.Any(x => x != 0))
            throw new ShoppingException("INVALID_EMBEDDING", 502);
        return "[" + string.Join(',', vector.Select(x => x.ToString("R", CultureInfo.InvariantCulture))) + "]";
    }
}
