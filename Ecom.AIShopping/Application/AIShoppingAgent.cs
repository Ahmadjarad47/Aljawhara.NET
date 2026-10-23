using System.Diagnostics;
using Ecom.AIShopping.Abstractions;
using Ecom.AIShopping.Contracts;
using Ecom.AIShopping.Recommendations;
using Ecom.AIShopping.Validation;

namespace Ecom.AIShopping.Application;

public sealed class AIShoppingAgent(IShoppingPlanner planner, IShoppingConversationStore store,
    ShoppingRecommendationService recommendations, ShoppingLimits limits, TimeProvider clock, ShoppingUsageLedger? usage = null)
{
    public async Task<ShoppingResponse> ChatAsync(string ownerHash, ChatRequest request, CancellationToken ct)
    {
        if (!ShoppingValidation.Text(request.Message, limits.MaxMessageLength)) throw new ShoppingException("INVALID_MESSAGE");
        var started = Stopwatch.GetTimestamp();
        ShoppingConversation? old = null;
        if (request.ConversationId.HasValue)
        {
            old = await GetStateAsync(request.ConversationId.Value, ownerHash, ct);
            if (request.ExpectedVersion != old.Version) throw new ShoppingException("STALE_RECOMMENDATION", 409);
            if (old.TurnCount >= limits.MaxTurns) throw new ShoppingException("CONVERSATION_LIMIT", 429);
        }
        else if (request.ExpectedVersion is not null) throw new ShoppingException("INVALID_VERSION");
        var planning = await planner.PlanAsync(old, request.Message, ct);
        ShoppingValidation.Plan(planning.Plan, limits);
        var (items, missing) = string.IsNullOrWhiteSpace(planning.Plan.Clarification)
            ? await recommendations.BuildAsync(planning.Plan, old, ct)
            : (Array.Empty<Recommendation>(), Array.Empty<string>());
        var overrides = old?.QuantityOverrides.Where(edit => planning.Plan.People == old.Plan.People &&
            items.Any(i => i.ProductId == edit.ProductId && i.NeedKeys.All(key =>
                old.Plan.Needs.Any(n => n.Key == key && planning.Plan.Needs.Any(next => next == n))))).ToArray() ?? [];
        if (overrides.Length > 0)
            items = await recommendations.RepriceAsync(items, items.Select(i => overrides.FirstOrDefault(e => e.ProductId == i.ProductId) ??
                new QuantityEdit(i.ProductId, i.RecommendedPackageQuantity)).ToArray(), planning.Plan.Budget, ct);
        var id = old?.Id ?? Guid.NewGuid();
        var expiry = old?.ExpiresAtUtc ?? clock.GetUtcNow().UtcDateTime.AddMinutes(limits.ConversationMinutes);
        var response = new ShoppingResponse(id, Guid.NewGuid(), (old?.Version ?? 0) + 1,
            planning.Plan.Clarification ?? (items.Length > 0 ? "جهزتلك قائمة من المنتجات المتاحة. راجع الكميات قبل الإضافة للسلة." : "لم أجد منتجات مناسبة متاحة ضمن القيود المطلوبة."),
            planning.Plan.Clarification != null ? "clarification" : items.Length > 0 ? "ready" : "empty",
            items, "KWD", ShoppingValidation.Money(items.Sum(i => i.LineTotal)), missing, items.Length > 0,
            expiry, planning.Usage with { DurationMs = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                EmbeddingInputTokens = usage?.EmbeddingInputTokens ?? 0,
                EstimatedUsd = planning.Usage.EstimatedUsd + (usage?.EmbeddingEstimatedUsd ?? 0) });
        var history = (old?.History ?? []).Append(new("user", request.Message)).TakeLast(limits.HistoryTurns).ToArray();
        var conversation = new ShoppingConversation(id, ownerHash, response.Version, expiry, planning.Plan,
            history, response, (old?.TurnCount ?? 0) + 1) { QuantityOverrides = overrides };
        if (!await store.SaveAsync(conversation, old?.Version ?? 0, ct)) throw new ShoppingException("STALE_RECOMMENDATION", 409);
        return response;
    }

    public async Task<ShoppingConversation> GetStateAsync(Guid id, string ownerHash, CancellationToken ct)
    {
        var value = await store.FindAsync(id, ownerHash, ct);
        if (value is null || value.ExpiresAtUtc <= clock.GetUtcNow().UtcDateTime) throw new ShoppingException("CONVERSATION_NOT_FOUND", 404);
        return value;
    }

    public async Task<ShoppingResponse> RecalculateAsync(Guid id, string ownerHash, RecalculateRequest request, CancellationToken ct)
    {
        var old = await GetStateAsync(id, ownerHash, ct);
        if (old.Version != request.ExpectedVersion) throw new ShoppingException("STALE_RECOMMENDATION", 409);
        var items = await recommendations.RepriceAsync(old.Response.Recommendations, request.Items, old.Plan.Budget, ct);
        var removedKeys = old.Response.Recommendations.Where(r => items.All(i => i.ProductId != r.ProductId)).SelectMany(r => r.NeedKeys);
        var excluded = old.Plan.ExcludedNeeds.Concat(removedKeys).Distinct().ToArray();
        var response = old.Response with { Version = old.Version + 1, RecommendationId = Guid.NewGuid(),
            Recommendations = items, EstimatedTotal = ShoppingValidation.Money(items.Sum(i => i.LineTotal)),
            RequiresConfirmation = items.Length > 0, Status = items.Length > 0 ? "ready" : "empty", Usage = new(0, 0, 0, 0) };
        if (!await store.SaveAsync(old with { Version = response.Version, Response = response,
                Plan = old.Plan with { ExcludedNeeds = excluded }, QuantityOverrides = request.Items.ToArray() }, old.Version, ct))
            throw new ShoppingException("STALE_RECOMMENDATION", 409);
        return response;
    }
}
