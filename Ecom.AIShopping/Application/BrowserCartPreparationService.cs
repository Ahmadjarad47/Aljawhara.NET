using Ecom.AIShopping.Abstractions;
using Ecom.AIShopping.Contracts;
using Ecom.AIShopping.Validation;

namespace Ecom.AIShopping.Application;

/// <summary>Validates an explicit approval for the existing browser cart. This service does not claim a cart mutation.</summary>
public sealed class BrowserCartPreparationService(AIShoppingAgent agent, IProductCatalogReader catalog, TimeProvider clock)
{
    public async Task<PreparedBrowserCart> PrepareAsync(Guid conversationId, Guid recommendationId, string owner,
        PrepareBrowserCartRequest request, CancellationToken ct)
    {
        if (!request.Approved) throw new ShoppingException("EXPLICIT_APPROVAL_REQUIRED");
        if (request.CurrentCart is null || request.CurrentCart.Length > 100 || request.CurrentCart.Any(x =>
                x is null || x.ProductId <= 0 || x.Quantity is < 0 or > 10000) ||
            request.CurrentCart.Select(x => x.ProductId).Distinct().Count() != request.CurrentCart.Length)
            throw new ShoppingException("INVALID_CART_QUANTITIES");
        var state = await agent.GetStateAsync(conversationId, owner, ct);
        if (state.Version != request.ExpectedVersion || state.Response.RecommendationId != recommendationId)
            throw new ShoppingException("STALE_RECOMMENDATION", 409);
        if (state.Response.Recommendations.Length == 0) throw new ShoppingException("EMPTY_RECOMMENDATION");
        var products = await catalog.GetAsync(state.Response.Recommendations.Select(x => x.ProductId).ToArray(), ct);
        var accepted = new List<PreparedCartLine>();
        var rejected = new List<CartLineResult>();
        foreach (var item in state.Response.Recommendations)
        {
            var product = products.SingleOrDefault(p => p.ProductId == item.ProductId);
            if (product is not null && (product.UnitPrice != item.UnitPrice || product.UnitsPerPackage != item.UnitsPerPackage))
                throw new ShoppingException("PREVIEW_CHANGED", 409);
            var existing = request.CurrentCart.FirstOrDefault(x => x.ProductId == item.ProductId)?.Quantity ?? 0;
            if (product is null || product.RequiresVariant || product.UnitPrice <= 0 ||
                (long)existing + item.RecommendedPackageQuantity > product.AvailablePackages)
            {
                rejected.Add(new(item.ProductId, item.RecommendedPackageQuantity, 0, "PRODUCT_UNAVAILABLE"));
                continue;
            }
            accepted.Add(new(product.ProductId, product.Name, product.ImageUrl, product.UnitPrice,
                item.RecommendedPackageQuantity, product.AvailablePackages));
        }
        // Browser quantities are untrusted availability hints, never an inventory reservation or checkout authority.
        var expires = clock.GetUtcNow().UtcDateTime.AddSeconds(30);
        return new(recommendationId, "prepared", expires < state.ExpiresAtUtc ? expires : state.ExpiresAtUtc,
            state.ExpiresAtUtc, accepted.ToArray(), rejected.ToArray());
    }
}
