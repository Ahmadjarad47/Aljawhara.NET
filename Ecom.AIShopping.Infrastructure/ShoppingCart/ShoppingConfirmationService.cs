using System.Text.Json;
using Ecom.AIShopping.Abstractions;
using Ecom.AIShopping.Application;
using Ecom.AIShopping.Contracts;
using Ecom.AIShopping.Infrastructure.Persistence;
using Ecom.AIShopping.Validation;
using Microsoft.EntityFrameworkCore;

namespace Ecom.AIShopping.Infrastructure.ShoppingCart;

public sealed class UnconfiguredShoppingCartGateway : IShoppingCartGateway
{
    public Task<CartConfirmation> AddApprovedAsync(string ownerHash, Guid operationId, IReadOnlyList<CartLine> lines, CancellationToken ct)
        => throw new ShoppingException("CART_INTEGRATION_REQUIRED", 503);
}

public sealed class ShoppingConfirmationService(AIShoppingDbContext db, AIShoppingAgent agent,
    IProductCatalogReader catalog, IShoppingCartGateway cart) : IShoppingConfirmationService
{
    public async Task<CartConfirmation> ConfirmAsync(Guid conversationId, Guid recommendationId, string ownerHash,
        ConfirmRequest request, CancellationToken ct)
    {
        if (!request.Approved) throw new ShoppingException("EXPLICIT_APPROVAL_REQUIRED");
        // Serialize confirmation with conversation writes. Database lock is brief: no model calls in this path.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var rows = await db.Conversations.FromSqlInterpolated($"SELECT * FROM ai_shopping.\"Conversations\" WHERE \"Id\" = {conversationId} AND \"OwnerHash\" = {ownerHash} FOR UPDATE")
            .AsNoTracking().ToListAsync(ct);
        if (rows.Count != 1 || rows[0].ExpiresAtUtc <= DateTime.UtcNow) throw new ShoppingException("CONVERSATION_NOT_FOUND", 404);
        var receipt = await db.ConfirmationReceipts.AsNoTracking().SingleOrDefaultAsync(r =>
            r.RecommendationId == recommendationId && r.ConversationId == conversationId && r.OwnerHash == ownerHash, ct);
        if (receipt != null)
        {
            var replay = JsonSerializer.Deserialize<CartConfirmation>(receipt.Payload, ShoppingJson.Options)!;
            await transaction.CommitAsync(ct);
            return replay;
        }
        var state = await agent.GetStateAsync(conversationId, ownerHash, ct);
        if (state.Version != request.ExpectedVersion || state.Response.RecommendationId != recommendationId)
            throw new ShoppingException("STALE_RECOMMENDATION", 409);
        if (state.Response.Recommendations.Length == 0) throw new ShoppingException("EMPTY_RECOMMENDATION");
        var live = await catalog.GetAsync(state.Response.Recommendations.Select(r => r.ProductId).ToArray(), ct);
        var accepted = new List<CartLine>();
        var rejected = new List<CartLineResult>();
        foreach (var item in state.Response.Recommendations)
        {
            var product = live.SingleOrDefault(p => p.ProductId == item.ProductId);
            if (product is null || product.RequiresVariant || product.AvailablePackages < item.RecommendedPackageQuantity)
            { rejected.Add(new(item.ProductId, item.RecommendedPackageQuantity, 0, "PRODUCT_UNAVAILABLE")); continue; }
            // A price/package change needs a fresh preview and a new explicit approval.
            if (product.UnitPrice != item.UnitPrice || product.UnitsPerPackage != item.UnitsPerPackage)
                throw new ShoppingException("PREVIEW_CHANGED", 409);
            accepted.Add(new(item.ProductId, item.RecommendedPackageQuantity, product.UnitPrice));
        }
        var result = accepted.Count == 0 ? new CartConfirmation(recommendationId, "unavailable", []) :
            await cart.AddApprovedAsync(ownerHash, recommendationId, accepted, ct);
        if (result.OperationId != recommendationId || result.Items.Length != accepted.Count ||
            result.Items.Select(x => x.ProductId).Distinct().Count() != accepted.Count ||
            result.Items.Any(r => !accepted.Any(a => a.ProductId == r.ProductId && a.PackageQuantity == r.RequestedQuantity) ||
                r.AddedQuantity < 0 || r.AddedQuantity > r.RequestedQuantity))
            throw new ShoppingException("INVALID_CART_RESULT", 502);
        var all = result.Items.Concat(rejected).ToArray();
        result = result with { Items = all, Status = all.All(x => x.AddedQuantity == x.RequestedQuantity) ? "added" :
            all.Any(x => x.AddedQuantity > 0) ? "partial" : "unavailable" };
        db.ConfirmationReceipts.Add(new() { RecommendationId = recommendationId, ConversationId = conversationId,
            OwnerHash = ownerHash, ExpiresAtUtc = state.ExpiresAtUtc, Payload = JsonSerializer.Serialize(result, ShoppingJson.Options) });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return result;
    }
}
