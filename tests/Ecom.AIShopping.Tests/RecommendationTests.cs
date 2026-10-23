using Ecom.AIShopping.Application;
using Ecom.AIShopping.Contracts;
using Ecom.AIShopping.Infrastructure.BackgroundJobs;
using Ecom.AIShopping.Infrastructure.ProductCatalog;
using Ecom.AIShopping.Infrastructure.Retrieval;
using Ecom.AIShopping.Recommendations;
using Ecom.AIShopping.Validation;
using Ecom.Domain.Entity;
using Xunit;

namespace Ecom.AIShopping.Tests;

public sealed class RecommendationTests
{
    private static readonly ShoppingLimits Limits = new();

    [Theory]
    [InlineData(5, 10, 1)] [InlineData(10, 10, 1)] [InlineData(11, 10, 2)] [InlineData(200, 12, 17)]
    public void Physical_units_round_up_to_packages(int units, int pack, int expected) =>
        Assert.Equal(expected, ShoppingValidation.Packages(units, pack));

    [Theory]
    [InlineData(0, 10)] [InlineData(10, 0)] [InlineData(-1, 1)]
    public void Invalid_quantities_fail(int units, int pack) => Assert.Throws<ShoppingException>(() => ShoppingValidation.Packages(units, pack));

    [Fact]
    public async Task Picnic_for_five_uses_one_real_package_and_current_KWD_price()
    {
        var catalog = new FixtureCatalog(FixtureCatalog.Cup());
        var result = await new ShoppingRecommendationService(catalog, catalog, Limits).BuildAsync(FixtureCatalog.Plan(), null, TestContext.Current.CancellationToken);
        var item = Assert.Single(result.Items);
        Assert.Equal(1, item.ProductId); Assert.Equal(1, item.RecommendedPackageQuantity); Assert.Equal(0.500m, item.LineTotal);
    }

    [Fact]
    public async Task Cheapest_package_count_total_wins_under_budget()
    {
        var catalog = new FixtureCatalog(FixtureCatalog.Cup(1, 0.4m, 2), FixtureCatalog.Cup(2, 0.6m, 10));
        var result = await new ShoppingRecommendationService(catalog, catalog, Limits).BuildAsync(FixtureCatalog.Plan(5, 0.7m), null, TestContext.Current.CancellationToken);
        Assert.Equal(2, Assert.Single(result.Items).ProductId);
    }

    [Fact]
    public async Task Impossible_budget_reports_missing_without_fabrication()
    {
        var catalog = new FixtureCatalog(FixtureCatalog.Cup());
        var result = await new ShoppingRecommendationService(catalog, catalog, Limits).BuildAsync(FixtureCatalog.Plan(5, 0.1m), null, TestContext.Current.CancellationToken);
        Assert.Empty(result.Items); Assert.Single(result.Unavailable);
    }

    [Theory]
    [InlineData(null, 50, false)] [InlineData(10, 0, false)] [InlineData(10, 50, true)]
    public async Task Unknown_pack_stock_or_unsupported_variant_is_excluded(int? pack, int stock, bool variant)
    {
        var catalog = new FixtureCatalog(FixtureCatalog.Cup(pack: pack, stock: stock) with { RequiresVariant = variant });
        var result = await new ShoppingRecommendationService(catalog, catalog, Limits).BuildAsync(FixtureCatalog.Plan(), null, TestContext.Current.CancellationToken);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Unknown_pack_allowed_only_for_explicit_package_need()
    {
        var catalog = new FixtureCatalog(FixtureCatalog.Cup(pack: null));
        var plan = FixtureCatalog.Plan(); plan = plan with { Needs = [plan.Needs[0] with { PackageBased = true, UnitsPerPerson = 0, FixedUnits = 1 }] };
        var result = await new ShoppingRecommendationService(catalog, catalog, Limits).BuildAsync(plan, null, TestContext.Current.CancellationToken);
        Assert.Equal(1, Assert.Single(result.Items).RecommendedPackageQuantity); Assert.Null(result.Items[0].UnitsPerPackage);
    }

    [Fact]
    public async Task Excluded_needs_stay_removed()
    {
        var catalog = new FixtureCatalog(FixtureCatalog.Cup());
        var result = await new ShoppingRecommendationService(catalog, catalog, Limits).BuildAsync(FixtureCatalog.Plan() with { ExcludedNeeds = ["cups"] }, null, TestContext.Current.CancellationToken);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Changing_people_recalculates_packages_and_preserves_context()
    {
        var catalog = new FixtureCatalog(FixtureCatalog.Cup()); var store = new FixtureStore();
        var planner = new FixturePlanner(FixtureCatalog.Plan());
        var agent = new AIShoppingAgent(planner, store, new(catalog, catalog, Limits), Limits, TimeProvider.System);
        var first = await agent.ChatAsync("owner", new(null, null, "بدي تجهيزات نزهة لـ 5 أشخاص"), TestContext.Current.CancellationToken);
        planner.Plan = FixtureCatalog.Plan(12);
        var second = await agent.ChatAsync("owner", new(first.ConversationId, first.Version, "صرنا 12"), TestContext.Current.CancellationToken);
        Assert.Equal(2, Assert.Single(second.Recommendations).RecommendedPackageQuantity);
        Assert.Equal("نزهة", planner.Previous!.Plan.Occasion); Assert.Equal(2, second.Version);
    }

    [Fact]
    public async Task Remove_edits_persist_excluded_need_and_new_preview_id()
    {
        var catalog = new FixtureCatalog(FixtureCatalog.Cup()); var store = new FixtureStore();
        var agent = new AIShoppingAgent(new FixturePlanner(FixtureCatalog.Plan()), store, new(catalog, catalog, Limits), Limits, TimeProvider.System);
        var first = await agent.ChatAsync("owner", new(null, null, "نزهة"), TestContext.Current.CancellationToken);
        var updated = await agent.RecalculateAsync(first.ConversationId, "owner", new(1, []), TestContext.Current.CancellationToken);
        Assert.Contains("cups", store.State!.Plan.ExcludedNeeds); Assert.Empty(updated.Recommendations);
        Assert.NotEqual(first.RecommendationId, updated.RecommendationId);
    }

    [Theory]
    [InlineData("إبْرِيق ـ أَكواب", "ابريق  اكواب")] [InlineData("عبوة ١٠", "عبوة 10")]
    public void Arabic_normalization(string value, string expected) => Assert.Equal(expected, ArabicSearch.Normalize(value));

    [Fact]
    public void RRF_rewards_overlap_and_deduplicates() => Assert.Equal(new[] { 2, 1, 3 }, ArabicSearch.Fuse([1, 2], [2, 3], 3));

    [Theory]
    [InlineData("١٠", 10)] [InlineData("12", 12)] [InlineData("ten", null)] [InlineData("10 pieces", null)] [InlineData("0", null)]
    public void Packaging_only_uses_explicit_verified_integer_details(string value, int? expected) => Assert.Equal(expected,
        CommerceCatalogReader.PackageSize([new ProductDetails { Label = "package quantity", Value = value }]));

    [Fact]
    public void Projection_hash_source_excludes_price_and_stock()
    {
        var p = new Product { Id = 1, TitleAr = "كاسات", newPrice = 10, TotalInStock = 20,
            subCategory = new() { Name = "tableware", Category = new() { Name = "home" } } };
        var before = ShoppingSearchIndexService.BuildDocument(p);
        p.newPrice = 100; p.TotalInStock = 1;
        Assert.Equal(before, ShoppingSearchIndexService.BuildDocument(p));
        p.productDetails.Add(new() { Label = "material", Value = "paper" });
        Assert.NotEqual(before, ShoppingSearchIndexService.BuildDocument(p));
    }

    [Fact]
    public async Task Distinct_needs_for_same_SKU_combine_physical_units_before_rounding()
    {
        var catalog = new FixtureCatalog(FixtureCatalog.Cup()); var plan = FixtureCatalog.Plan(6);
        plan = plan with { Needs = [plan.Needs[0], plan.Needs[0] with { Key = "extra-cups" }] };
        var result = await new ShoppingRecommendationService(catalog, catalog, Limits).BuildAsync(plan, null, TestContext.Current.CancellationToken);
        Assert.Equal(2, Assert.Single(result.Items).RecommendedPackageQuantity);
        Assert.Equal(2, result.Items[0].NeedKeys.Length);
    }

    [Fact]
    public async Task Manual_quantities_survive_unchanged_needs_but_reset_when_people_change()
    {
        var catalog = new FixtureCatalog(FixtureCatalog.Cup()); var store = new FixtureStore();
        var planner = new FixturePlanner(FixtureCatalog.Plan());
        var agent = new AIShoppingAgent(planner, store, new(catalog, catalog, Limits), Limits, TimeProvider.System);
        var first = await agent.ChatAsync("owner", new(null, null, "نزهة"), TestContext.Current.CancellationToken);
        var edited = await agent.RecalculateAsync(first.ConversationId, "owner", new(1, [new(1, 3)]), TestContext.Current.CancellationToken);
        var unchanged = await agent.ChatAsync("owner", new(first.ConversationId, edited.Version, "نفس التجهيزات"), TestContext.Current.CancellationToken);
        Assert.Equal(3, Assert.Single(unchanged.Recommendations).RecommendedPackageQuantity);
        planner.Plan = FixtureCatalog.Plan(8);
        var resized = await agent.ChatAsync("owner", new(first.ConversationId, unchanged.Version, "صرنا ثمانية"), TestContext.Current.CancellationToken);
        Assert.Equal(1, Assert.Single(resized.Recommendations).RecommendedPackageQuantity);
    }

    [Fact]
    public void Invalid_embedding_dimensions_nonfinite_or_zero_vectors_are_rejected()
    {
        Assert.Throws<ShoppingException>(() => PostgresProductRetrieval.VectorLiteral([1]));
        Assert.Throws<ShoppingException>(() => PostgresProductRetrieval.VectorLiteral(new float[1536]));
        var vector = new float[1536]; vector[0] = float.NaN;
        Assert.Throws<ShoppingException>(() => PostgresProductRetrieval.VectorLiteral(vector));
    }
}
