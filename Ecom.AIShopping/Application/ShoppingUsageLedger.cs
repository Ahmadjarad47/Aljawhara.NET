namespace Ecom.AIShopping.Application;

/// <summary>Request-scoped usage only; no customer content is retained.</summary>
public sealed class ShoppingUsageLedger
{
    public int EmbeddingInputTokens { get; private set; }
    public decimal EmbeddingEstimatedUsd { get; private set; }
    public decimal ModelEstimatedUsd { get; private set; }
    public decimal TotalEstimatedUsd => ModelEstimatedUsd + EmbeddingEstimatedUsd;
    public void SetModelEstimate(decimal value) => ModelEstimatedUsd = value;
    public void RecordEmbedding(int tokens, decimal usdPerMillion)
    {
        EmbeddingInputTokens = checked(EmbeddingInputTokens + tokens);
        EmbeddingEstimatedUsd += tokens * usdPerMillion / 1_000_000m;
    }
}
