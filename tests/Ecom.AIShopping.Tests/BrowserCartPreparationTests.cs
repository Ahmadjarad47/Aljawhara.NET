using Ecom.AIShopping.Application;
using Ecom.AIShopping.Contracts;
using Ecom.AIShopping.Validation;
using Xunit;

namespace Ecom.AIShopping.Tests;

public sealed class BrowserCartPreparationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static async Task<(BrowserCartPreparationService Service, FixtureCatalog Catalog, ShoppingResponse Preview)> Setup()
    {
        var catalog = new FixtureCatalog(FixtureCatalog.Cup()); var store = new FixtureStore(); var limits = new ShoppingLimits();
        var agent = new AIShoppingAgent(new FixturePlanner(FixtureCatalog.Plan()), store, new(catalog, catalog, limits), limits, TimeProvider.System);
        var preview = await agent.ChatAsync("owner", new(null, null, "نزهة لخمسة"), Ct);
        return (new(agent, catalog, TimeProvider.System), catalog, preview);
    }

    [Fact]
    public async Task Preparation_returns_current_verified_lines_and_never_claims_a_cart_mutation()
    {
        var (service, _, preview) = await Setup();
        var result = await service.PrepareAsync(preview.ConversationId, preview.RecommendationId, "owner", new(1, true, []), Ct);
        Assert.Equal("prepared", result.Status); Assert.Equal(preview.RecommendationId, result.OperationId);
        Assert.Equal(.500m, Assert.Single(result.Items).Price); Assert.Equal(1, result.Items[0].Quantity);
        Assert.True(result.ExpiresAtUtc <= DateTime.UtcNow.AddSeconds(30)); Assert.Equal(preview.ExpiresAtUtc, result.ReplayUntilUtc);
    }

    [Fact]
    public async Task Approval_owner_and_version_are_checked_before_returning_cart_data()
    {
        var (service, _, preview) = await Setup();
        Assert.Equal("EXPLICIT_APPROVAL_REQUIRED", (await Assert.ThrowsAsync<ShoppingException>(() => service.PrepareAsync(preview.ConversationId, preview.RecommendationId, "owner", new(1, false, []), Ct))).Code);
        Assert.Equal(404, (await Assert.ThrowsAsync<ShoppingException>(() => service.PrepareAsync(preview.ConversationId, preview.RecommendationId, "other", new(1, true, []), Ct))).Status);
        Assert.Equal(409, (await Assert.ThrowsAsync<ShoppingException>(() => service.PrepareAsync(preview.ConversationId, preview.RecommendationId, "owner", new(2, true, []), Ct))).Status);
    }

    [Fact]
    public async Task Existing_cart_quantity_cannot_exceed_observed_stock()
    {
        var (service, _, preview) = await Setup();
        var result = await service.PrepareAsync(preview.ConversationId, preview.RecommendationId, "owner", new(1, true, [new(1, 50)]), Ct);
        Assert.Empty(result.Items); Assert.Equal(0, Assert.Single(result.UnavailableItems).AddedQuantity);
    }

    [Fact]
    public async Task Price_change_requires_a_new_preview_and_approval()
    {
        var (service, catalog, preview) = await Setup(); catalog.Products = [FixtureCatalog.Cup(price: 9)];
        Assert.Equal("PREVIEW_CHANGED", (await Assert.ThrowsAsync<ShoppingException>(() => service.PrepareAsync(preview.ConversationId, preview.RecommendationId, "owner", new(1, true, []), Ct))).Code);
    }

    [Fact]
    public async Task Duplicate_negative_and_oversized_cart_quantities_are_rejected()
    {
        var (service, _, preview) = await Setup();
        foreach (var quantities in new[] { new[] { new BrowserCartQuantity(1, -1) }, new[] { new BrowserCartQuantity(1, 10001) }, new[] { new BrowserCartQuantity(1, 1), new BrowserCartQuantity(1, 1) } })
            Assert.Equal("INVALID_CART_QUANTITIES", (await Assert.ThrowsAsync<ShoppingException>(() => service.PrepareAsync(preview.ConversationId, preview.RecommendationId, "owner", new(1, true, quantities), Ct))).Code);
    }
}
