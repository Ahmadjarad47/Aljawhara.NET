using System.ClientModel;
using Ecom.AIShopping.Abstractions;
using Ecom.AIShopping.Application;
using Ecom.AIShopping.Infrastructure.DependencyInjection;
using Ecom.AIShopping.Validation;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using OpenAI.Embeddings;

namespace Ecom.AIShopping.Infrastructure.Embeddings;

public sealed class DisabledEmbeddingService : IEmbeddingService
{
    public Task<float[]> EmbedAsync(string text, CancellationToken ct) => throw new ShoppingException("SEMANTIC_DISABLED", 503);
}

public sealed class OpenAIEmbeddingService(EmbeddingClient client, IMemoryCache cache, IOptions<AIShoppingOptions> options,
    ShoppingUsageLedger usage, ILogger<OpenAIEmbeddingService> logger) : IEmbeddingService
{
    public async Task<float[]> EmbedAsync(string text, CancellationToken ct)
    {
        if (!options.Value.SemanticEnabled) throw new ShoppingException("SEMANTIC_DISABLED", 503);
        if (text.Length < 1 || System.Text.Encoding.UTF8.GetByteCount(text) > 8000) throw new ShoppingException("EMBEDDING_INPUT_LIMIT");
        var key = "ai-embedding:" + options.Value.EmbeddingModel + ":" +
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));
        if (cache.TryGetValue<float[]>(key, out var existing) && existing != null) return existing;
        try
        {
            if (usage.TotalEstimatedUsd + System.Text.Encoding.UTF8.GetByteCount(text) * options.Value.EmbeddingUsdPerMillion / 1_000_000m >
                options.Value.MaxTurnEstimatedUsd) throw new ShoppingException("AI_BUDGET_EXCEEDED", 429);
            var response = await client.GenerateEmbeddingsAsync([text],
                new EmbeddingGenerationOptions { Dimensions = options.Value.EmbeddingDimensions }, ct);
            usage.RecordEmbedding(response.Value.Usage.InputTokenCount, options.Value.EmbeddingUsdPerMillion);
            logger.LogInformation("AI embedding completed. InputTokens={InputTokens}, EstimatedUsd={EstimatedUsd}",
                response.Value.Usage.InputTokenCount, response.Value.Usage.InputTokenCount * options.Value.EmbeddingUsdPerMillion / 1_000_000m);
            var vector = response.Value[0].ToFloats().ToArray();
            Retrieval.PostgresProductRetrieval.VectorLiteral(vector);
            cache.Set(key, vector, new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) });
            return vector;
        }
        catch (ClientResultException) { throw new ShoppingException("EMBEDDING_PROVIDER_UNAVAILABLE", 503); }
    }
}
