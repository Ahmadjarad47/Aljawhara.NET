using Ecom.AIShopping.Abstractions;
using Ecom.AIShopping.Contracts;

namespace Ecom.AIShopping.Tests;

public sealed class FixtureCatalog(params CatalogProduct[] products) : IProductCatalogReader, IProductRetrievalService
{
    public CatalogProduct[] Products { get; set; } = products;
    public Task<IReadOnlyList<CatalogProduct>> GetAsync(IReadOnlyCollection<int> ids, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<CatalogProduct>>(Products.Where(p => ids.Contains(p.ProductId)).ToArray());
    public Task<IReadOnlyList<CatalogProduct>> SearchAsync(string query, string? material, int limit, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<CatalogProduct>>(Products.Where(p => material == null || p.Material == material).Take(limit).ToArray());
    public static CatalogProduct Cup(int id = 1, decimal price = 0.500m, int? pack = 10, int stock = 50) =>
        new(id, "أكواب ورقية", "Paper cups", "catalog text", "أدوات مائدة", "/cups.png", price, stock, pack, "ورق", false);
    public static ShoppingPlan Plan(int people = 5, decimal? budget = null) => new("نزهة", people, budget, false, [],
        [new("cups", "أكواب", "أكواب", null, 1, 0, false, true, null)], null);
}

public sealed class FixtureStore : IShoppingConversationStore
{
    public ShoppingConversation? State { get; set; }
    public Task<ShoppingConversation?> FindAsync(Guid id, string ownerHash, CancellationToken ct) =>
        Task.FromResult(State?.Id == id && State.OwnerHash == ownerHash ? State : null);
    public Task<bool> SaveAsync(ShoppingConversation conversation, int expectedVersion, CancellationToken ct)
    {
        if (State != null && State.Version != expectedVersion) return Task.FromResult(false);
        State = conversation; return Task.FromResult(true);
    }
}

public sealed class FixturePlanner(ShoppingPlan plan) : IShoppingPlanner
{
    public ShoppingConversation? Previous { get; private set; }
    public ShoppingPlan Plan { get; set; } = plan;
    public Task<PlanningResult> PlanAsync(ShoppingConversation? previous, string message, CancellationToken ct)
    { Previous = previous; return Task.FromResult(new PlanningResult(Plan, new(1, 1, 0, 0))); }
}
