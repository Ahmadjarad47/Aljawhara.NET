using Ecom.AIShopping.Validation;

namespace Ecom.AIShopping.Infrastructure.DependencyInjection;

public sealed class AIShoppingOptions
{
    public bool Enabled { get; set; }
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "gpt-6-luna";
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
    public bool SemanticEnabled { get; set; }
    public bool TrigramEnabled { get; set; }
    public double MinimumTrigramSimilarity { get; set; } = 0.35;
    public double MaximumCosineDistance { get; set; } = 0.65;
    public bool IndexingEnabled { get; set; }
    public int EmbeddingDimensions { get; set; } = 1536;
    public int MaxIterations { get; set; } = 4;
    public int MaxToolCalls { get; set; } = 8;
    public int MaxOutputTokens { get; set; } = 2500;
    public int MaxInputTokens { get; set; } = 32000;
    public int TimeoutSeconds { get; set; } = 45;
    public int MaxConcurrentRequests { get; set; } = 4;
    public int RequestsPerMinute { get; set; } = 8;
    public int IpRequestsPerMinute { get; set; } = 20;
    public int IndexBatchSize { get; set; } = 25;
    public int IndexIntervalSeconds { get; set; } = 60;
    public int CandidateLimit { get; set; } = 40;
    public decimal InputUsdPerMillion { get; set; } = 0.10m;
    public decimal OutputUsdPerMillion { get; set; } = 0.50m;
    public decimal EmbeddingUsdPerMillion { get; set; } = 0.02m;
    public decimal MaxTurnEstimatedUsd { get; set; } = 0.03m;
    public string[] ImageHosts { get; set; } = ["aljawharaplus.com"];
    public ShoppingLimits Limits { get; set; } = new();

    public bool IsValid() => !Enabled || (!string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(Model) && EmbeddingDimensions == 1536 &&
        MaxIterations is >= 1 and <= 6 && MaxToolCalls is >= 1 and <= 12 &&
        MaxOutputTokens is >= 500 and <= 5000 && MaxInputTokens is >= 2000 and <= 32000 &&
        TimeoutSeconds is >= 5 and <= 90 && MaxConcurrentRequests is >= 1 and <= 16 &&
        RequestsPerMinute is >= 1 and <= 60 && IpRequestsPerMinute is >= 1 and <= 120 &&
        IndexBatchSize is >= 1 and <= 100 && IndexIntervalSeconds is >= 10 and <= 3600 &&
        CandidateLimit is >= 12 and <= 100 && InputUsdPerMillion >= 0 && OutputUsdPerMillion >= 0 && EmbeddingUsdPerMillion >= 0 &&
        MinimumTrigramSimilarity is >= 0.2 and <= 0.8 && MaximumCosineDistance is > 0 and < 1 &&
        MaxTurnEstimatedUsd is > 0 and <= 1 && Limits.MaxNeeds is >= 1 and <= 20 &&
        Limits.MaxPackages is >= 1 and <= 100 && Limits.MaxMessageLength is >= 100 and <= 4000 &&
        Limits.HistoryTurns is >= 1 and <= 10 && Limits.MaxTurns is >= 1 and <= 50 &&
        Limits.ConversationMinutes is >= 5 and <= 1440);
}
