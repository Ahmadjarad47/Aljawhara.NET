using System.Text.Json.Serialization;

namespace Ecom.AIShopping.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChatRequest(Guid? ConversationId, int? ExpectedVersion, string Message);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record QuantityEdit(int ProductId, int PackageQuantity);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RecalculateRequest(int ExpectedVersion, IReadOnlyList<QuantityEdit> Items);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ConfirmRequest(int ExpectedVersion, bool Approved);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BrowserCartQuantity(int ProductId, int Quantity);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PrepareBrowserCartRequest(int ExpectedVersion, bool Approved, BrowserCartQuantity[] CurrentCart);
public sealed record PreparedCartLine(int ProductId, string Name, string? Image, decimal Price, int Quantity, int AvailablePackages);
public sealed record PreparedBrowserCart(Guid OperationId, string Status, DateTime ExpiresAtUtc, DateTime ReplayUntilUtc, PreparedCartLine[] Items,
    CartLineResult[] UnavailableItems);
public sealed record ShoppingNeed(string Key, string Label, string Query, string? Material,
    int UnitsPerPerson, int FixedUnits, bool PackageBased, bool Essential, int? SelectedProductId);
public sealed record ShoppingPlan(string Occasion, int People, decimal? Budget, bool PreferCheaper,
    string[] ExcludedNeeds, ShoppingNeed[] Needs, string? Clarification);
public sealed record CatalogProduct(int ProductId, string Name, string NameEn, string Description,
    string Category, string? ImageUrl, decimal UnitPrice, int AvailablePackages, int? UnitsPerPackage,
    string? Material, bool RequiresVariant);
public sealed record Recommendation(int ProductId, int? VariantId, string ProductName, string? ImageUrl,
    decimal UnitPrice, string Currency, int? UnitsPerPackage, int RecommendedPackageQuantity,
    decimal LineTotal, string Reason, string[] NeedKeys);
public sealed record UsageSummary(int InputTokens, int OutputTokens, decimal EstimatedUsd, long DurationMs)
{
    public int EmbeddingInputTokens { get; init; }
}
public sealed record ShoppingResponse(Guid ConversationId, Guid RecommendationId, int Version,
    string Message, string Status, Recommendation[] Recommendations, string Currency,
    decimal EstimatedTotal, string[] UnavailableNeeds, bool RequiresConfirmation, DateTime ExpiresAtUtc,
    UsageSummary Usage);
public sealed record CartLine(int ProductId, int PackageQuantity, decimal UnitPrice);
public sealed record CartLineResult(int ProductId, int RequestedQuantity, int AddedQuantity, string? ErrorCode);
public sealed record CartConfirmation(Guid OperationId, string Status, CartLineResult[] Items);
public sealed record ConversationTurn(string Role, string Text);
public sealed record ShoppingConversation(Guid Id, string OwnerHash, int Version, DateTime ExpiresAtUtc,
    ShoppingPlan Plan, ConversationTurn[] History, ShoppingResponse Response, int TurnCount)
{
    public QuantityEdit[] QuantityOverrides { get; init; } = [];
}
public sealed record PlanningResult(ShoppingPlan Plan, UsageSummary Usage);

public static class ShoppingJson
{
    public static readonly System.Text.Json.JsonSerializerOptions Options = new(System.Text.Json.JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        MaxDepth = 24
    };
}
