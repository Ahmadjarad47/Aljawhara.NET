#pragma warning disable OPENAI001
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Diagnostics;
using System.Text.Json;
using Ecom.AIShopping.Contracts;
using Ecom.AIShopping.Infrastructure.DependencyInjection;
using Ecom.AIShopping.Infrastructure.OpenAI;
using Ecom.AIShopping.Infrastructure.Retrieval;
using Ecom.AIShopping.Recommendations;
using Ecom.AIShopping.Validation;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenAI.Responses;
using Xunit;

namespace Ecom.AIShopping.Tests;

public sealed class ArabicEvaluationTests
{
    public static bool PaidLiveEnabled => Environment.GetEnvironmentVariable("ECOM_ALLOW_PAID_AI_TESTS") == "1" &&
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AIShopping__ApiKey"));
    private sealed record Scenario(string Id, string[] Messages, int People, decimal? Budget, string? Material, string ExpectedNeed, string[] Checks);
    private static Scenario[] Scenarios() => JsonSerializer.Deserialize<Scenario[]>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "evaluation-scenarios.json")), ShoppingJson.Options)!;

    [Fact]
    public async Task Deterministic_Arabic_scenario_constraints_are_reproducible()
    {
        // Expected structured plans are fixtures. This tests downstream grounding/math, NOT model language understanding.
        var latencies = new List<double>();
        foreach (var scenario in Scenarios())
        {
            var started = Stopwatch.GetTimestamp();
            var products = scenario.ExpectedNeed is "unavailable" or "privacy" ? Array.Empty<CatalogProduct>() :
                new[] { FixtureCatalog.Cup(1, .500m, 10), FixtureCatalog.Cup(2, .350m, 3) };
            var catalog = new FixtureCatalog(products);
            var plan = FixtureCatalog.Plan(scenario.People, scenario.Budget);
            if (scenario.Material != null) plan = plan with { Needs = [plan.Needs[0] with { Material = scenario.Material }] };
            var result = await new ShoppingRecommendationService(catalog, catalog, new()).BuildAsync(plan, null, TestContext.Current.CancellationToken);
            Assert.All(result.Items, item =>
            {
                var real = products.Single(p => p.ProductId == item.ProductId);
                Assert.Equal(real.UnitPrice, item.UnitPrice);
                Assert.Equal(ShoppingValidation.Packages(scenario.People, real.UnitsPerPackage!.Value), item.RecommendedPackageQuantity);
                Assert.Equal(ShoppingValidation.Money(item.UnitPrice * item.RecommendedPackageQuantity), item.LineTotal);
            });
            if (scenario.Budget.HasValue) Assert.True(result.Items.Sum(i => i.LineTotal) <= scenario.Budget);
            if (products.Length == 0) { Assert.Empty(result.Items); Assert.NotEmpty(result.Unavailable); }
            latencies.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
        Assert.Equal(10, Scenarios().Length);
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "ai-shopping-evaluation-results.json"), JsonSerializer.Serialize(new
        {
            mode = "deterministic downstream fixtures; no live language interpretation", scenarios = Scenarios().Length,
            passed = Scenarios().Length, liveModelCalls = 0, estimatedPaidCostUsd = 0,
            meanMilliseconds = latencies.Average(), maximumMilliseconds = latencies.Max(),
            evidence = "Assertions verify fixture product grounding, physical package quantities, budget bounds, and unavailable handling; HTTP and provider boundary tests cover authorization/tool injection separately."
        }, new JsonSerializerOptions { WriteIndented = true }));
        TestContext.Current.TestOutputHelper!.WriteLine($"10 fixture scenarios; no paid calls. Mean downstream latency={latencies.Average():F2}ms; max={latencies.Max():F2}ms. Grounding/quantity/budget assertions passed. This is not a live model accuracy benchmark.");
    }

    [Fact(Skip = "Paid provider evaluation requires explicit ECOM_ALLOW_PAID_AI_TESTS=1 and an API key", SkipUnless = nameof(PaidLiveEnabled))]
    public async Task Optional_paid_model_Arabic_planning_evaluation()
    {
        var options = new AIShoppingOptions { Model = Environment.GetEnvironmentVariable("AIShopping__Model") ?? "gpt-6-luna" };
        var client = new ResponsesClient(new ApiKeyCredential(Environment.GetEnvironmentVariable("AIShopping__ApiKey")!),
            new() { RetryPolicy = new ClientRetryPolicy(0), NetworkTimeout = TimeSpan.FromSeconds(45), EnableDistributedTracing = false });
        var catalog = new FixtureCatalog(FixtureCatalog.Cup());
        var planner = new OpenAIShoppingPlanner(client, new(catalog, catalog), Options.Create(options), NullLogger<OpenAIShoppingPlanner>.Instance);
        foreach (var scenario in Scenarios().Where(s => s.ExpectedNeed is not ("privacy" or "unavailable")))
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            var result = await planner.PlanAsync(null, string.Join('\n', scenario.Messages), timeout.Token);
            Assert.Equal(scenario.People, result.Plan.People);
            Assert.Equal(scenario.Budget, result.Plan.Budget);
            Assert.All(result.Plan.Needs, n => Assert.DoesNotContain("price", n.Key, StringComparison.OrdinalIgnoreCase));
            TestContext.Current.TestOutputHelper!.WriteLine($"{scenario.Id}: {result.Usage.DurationMs}ms; input={result.Usage.InputTokens}; output={result.Usage.OutputTokens}; estimated model USD={result.Usage.EstimatedUsd}");
        }
    }

    [Theory]
    [InlineData("كاسات ورقية", "أكواب", "cups", true)]
    [InlineData("أكواب", "صحون ورقية", "paper plates", false)]
    [InlineData("اكياس نفايات", "اكياس هدايا", "gift bags", false)]
    [InlineData("محارم", "مناديل", "napkins", true)]
    public void Query_suitability_does_not_equate_any_shared_word_with_purchase_fit(string query, string name, string english, bool expected) =>
        Assert.Equal(expected, ArabicSearch.Suitable(query, name, english, ""));
}
