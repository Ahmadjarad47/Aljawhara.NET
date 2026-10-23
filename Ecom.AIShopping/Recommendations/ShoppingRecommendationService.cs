using Ecom.AIShopping.Abstractions;
using Ecom.AIShopping.Contracts;
using Ecom.AIShopping.Validation;

namespace Ecom.AIShopping.Recommendations;

public sealed class ShoppingRecommendationService(IProductRetrievalService retrieval, IProductCatalogReader catalog,
    ShoppingLimits limits)
{
    public async Task<(Recommendation[] Items, string[] Unavailable)> BuildAsync(ShoppingPlan plan,
        ShoppingConversation? previous, CancellationToken ct)
    {
        ShoppingValidation.Plan(plan, limits);
        var selected = new Dictionary<int, Recommendation>();
        var allocated = new Dictionary<int, (int Units, int Packages)>();
        var missing = new List<string>();
        decimal total = 0;
        foreach (var need in plan.Needs.Where(n => !plan.ExcludedNeeds.Contains(n.Key, StringComparer.Ordinal))
                     .OrderByDescending(n => n.Essential))
        {
            // A live search always rechecks the need/material. Never accept an arbitrary model-selected ID.
            var candidates = await retrieval.SearchAsync(need.Query, need.Material, 12, ct);
            var units = checked(need.FixedUnits + need.UnitsPerPerson * plan.People);
            int CombinedPackages(CatalogProduct p)
            {
                var prior = allocated.GetValueOrDefault(p.ProductId);
                var physical = prior.Units + (need.PackageBased ? 0 : units);
                return prior.Packages + (need.PackageBased ? units : 0) +
                    (physical > 0 ? ShoppingValidation.Packages(physical, p.UnitsPerPackage!.Value) : 0);
            }
            var usable = candidates.Where(p => !p.RequiresVariant && p.UnitPrice > 0 && p.AvailablePackages > 0 &&
                                               (need.PackageBased || p.UnitsPerPackage > 0))
                .Select(p => (Product: p, Quantity: CombinedPackages(p)))
                .Where(x => x.Quantity <= limits.MaxPackages && x.Quantity <= x.Product.AvailablePackages)
                .ToList();
            var priorNeed = previous?.Plan.Needs.FirstOrDefault(n => n.Key == need.Key && n.Query == need.Query && n.Material == need.Material);
            var priorId = previous?.Response.Recommendations.FirstOrDefault(r => r.NeedKeys.Contains(need.Key))?.ProductId;
            var preferredId = need.SelectedProductId ?? (priorNeed != null ? priorId : null);
            var ranked = usable.OrderByDescending(x => !plan.PreferCheaper && x.Product.ProductId == preferredId)
                .ThenBy(x => plan.PreferCheaper || plan.Budget.HasValue ? x.Product.UnitPrice * x.Quantity : 0).ToList();
            var match = ranked.FirstOrDefault(x =>
            {
                var existing = selected.GetValueOrDefault(x.Product.ProductId);
                return !plan.Budget.HasValue || total - (existing?.LineTotal ?? 0) +
                    ShoppingValidation.Money(x.Product.UnitPrice * x.Quantity) <= plan.Budget.Value;
            });
            if (match.Product is null)
            {
                missing.Add($"{need.Label}: لا يوجد خيار متاح بعبوة موثقة ضمن القيود والميزانية.");
                continue;
            }
            var product = match.Product;
            var old = selected.GetValueOrDefault(product.ProductId);
            var packages = match.Quantity;
            var allocation = allocated.GetValueOrDefault(product.ProductId);
            allocated[product.ProductId] = (allocation.Units + (need.PackageBased ? 0 : units), allocation.Packages + (need.PackageBased ? units : 0));
            var lineTotal = ShoppingValidation.Money(product.UnitPrice * packages);
            total += lineTotal - (old?.LineTotal ?? 0);
            selected[product.ProductId] = new(product.ProductId, null, product.Name, product.ImageUrl,
                ShoppingValidation.Money(product.UnitPrice), "KWD", product.UnitsPerPackage, packages, lineTotal,
                $"{need.Label} — {packages} عبوة", [.. (old?.NeedKeys ?? []), need.Key]);
        }
        return ([.. selected.Values], [.. missing]);
    }

    public async Task<Recommendation[]> RepriceAsync(IReadOnlyList<Recommendation> recommendations,
        IReadOnlyList<QuantityEdit> edits, decimal? budget, CancellationToken ct)
    {
        if (edits is null || edits.Count > limits.MaxNeeds || edits.Select(x => x.ProductId).Distinct().Count() != edits.Count ||
            edits.Any(x => x.PackageQuantity < 1 || x.PackageQuantity > limits.MaxPackages ||
                           !recommendations.Any(r => r.ProductId == x.ProductId)))
            throw new ShoppingException("INVALID_QUANTITY_EDIT");
        var products = await catalog.GetAsync(edits.Select(e => e.ProductId).ToArray(), ct);
        var result = new List<Recommendation>();
        foreach (var edit in edits)
        {
            var p = products.SingleOrDefault(p => p.ProductId == edit.ProductId);
            if (p is null || p.RequiresVariant || p.UnitPrice <= 0 || p.AvailablePackages < edit.PackageQuantity)
                throw new ShoppingException("PRODUCT_UNAVAILABLE", 409);
            var original = recommendations.Single(r => r.ProductId == edit.ProductId);
            result.Add(original with { ProductName = p.Name, ImageUrl = p.ImageUrl, UnitPrice = p.UnitPrice,
                UnitsPerPackage = p.UnitsPerPackage, RecommendedPackageQuantity = edit.PackageQuantity,
                LineTotal = ShoppingValidation.Money(p.UnitPrice * edit.PackageQuantity) });
        }
        if (budget.HasValue && result.Sum(r => r.LineTotal) > budget.Value)
            throw new ShoppingException("BUDGET_EXCEEDED", 409);
        return [.. result];
    }
}
