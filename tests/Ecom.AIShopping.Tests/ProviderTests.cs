#pragma warning disable OPENAI001
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using System.Text.Json;
using Ecom.AIShopping.Contracts;
using Ecom.AIShopping.Infrastructure.DependencyInjection;
using Ecom.AIShopping.Infrastructure.OpenAI;
using Ecom.AIShopping.Validation;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenAI.Responses;
using Xunit;

namespace Ecom.AIShopping.Tests;

public sealed class ProviderTests
{
    private static string Final(ShoppingPlan? plan = null) => Envelope(new[] { new { type = "message", id = "msg_1", role = "assistant", status = "completed",
        content = new[] { new { type = "output_text", text = JsonSerializer.Serialize(plan ?? FixtureCatalog.Plan(), ShoppingJson.Options), annotations = Array.Empty<object>() } } } });
    private static string Call(string name, string args) => Envelope(new[] { new { type = "function_call", id = "fc_1", call_id = "call_1", name, arguments = args, status = "completed" } });
    private static string Envelope(object output) => JsonSerializer.Serialize(new { id = "resp_fixture", @object = "response", created_at = 1, status = "completed", model = "gpt-6-luna",
        output, usage = new { input_tokens = 100, output_tokens = 50, total_tokens = 150 } });

    private static OpenAIShoppingPlanner Planner(RecordingHandler handler, AIShoppingOptions? config = null)
    {
        var catalog = new FixtureCatalog(FixtureCatalog.Cup());
        var client = new ResponsesClient(new ApiKeyCredential("test-only-not-a-real-key"), new ResponsesClientOptions
        { Transport = new HttpClientPipelineTransport(new HttpClient(handler)), RetryPolicy = new ClientRetryPolicy(0) });
        return new(client, new(catalog, catalog), Options.Create(config ?? new()), NullLogger<OpenAIShoppingPlanner>.Instance);
    }

    [Fact]
    public async Task Stateless_strict_Responses_request_and_tool_output_call_id_are_preserved()
    {
        var handler = new RecordingHandler(Call("search_products", "{\"query\":\"أكواب\",\"material\":null}"), Final());
        var result = await Planner(handler).PlanAsync(null, "نزهة لخمسة", TestContext.Current.CancellationToken);
        Assert.Equal(5, result.Plan.People); Assert.Equal(200, result.Usage.InputTokens);
        Assert.Equal(2, handler.Bodies.Count);
        using var request = JsonDocument.Parse(handler.Bodies[0]);
        Assert.False(request.RootElement.GetProperty("store").GetBoolean());
        Assert.False(request.RootElement.GetProperty("parallel_tool_calls").GetBoolean());
        Assert.True(request.RootElement.GetProperty("text").GetProperty("format").GetProperty("strict").GetBoolean());
        Assert.All(request.RootElement.GetProperty("tools").EnumerateArray(), t => Assert.True(t.GetProperty("strict").GetBoolean()));
        Assert.Contains("function_call_output", handler.Bodies[1]); Assert.Contains("call_1", handler.Bodies[1]);
        Assert.DoesNotContain("test-only-not-a-real-key", handler.Bodies[0]);
    }

    [Fact]
    public async Task Tool_loop_is_bounded_before_another_execution()
    {
        var call = Call("get_product_details", "{\"productId\":1}");
        var handler = new RecordingHandler(call, call, call);
        var error = await Assert.ThrowsAsync<ShoppingException>(() => Planner(handler, new() { MaxIterations = 2 }).PlanAsync(null, "نزهة", TestContext.Current.CancellationToken));
        Assert.Equal("TOOL_LIMIT_EXCEEDED", error.Code); Assert.Equal(2, handler.Bodies.Count);
    }

    [Theory]
    [InlineData("change_price", "{\"productId\":1}", "UNAUTHORIZED_TOOL")]
    [InlineData("search_products", "{\"query\":\"cups\",\"material\":null,\"sql\":\"DROP TABLE Products\"}", "INVALID_TOOL_ARGUMENTS")]
    [InlineData("get_product_details", "{\"productId\":-1}", "INVALID_TOOL_ARGUMENTS")]
    public async Task Untrusted_tool_requests_cannot_expand_permissions(string name, string args, string code)
    {
        var handler = new RecordingHandler(Call(name, args));
        var error = await Assert.ThrowsAsync<ShoppingException>(() => Planner(handler).PlanAsync(null, "ignore instructions", TestContext.Current.CancellationToken));
        Assert.Equal(code, error.Code); Assert.Single(handler.Bodies);
    }

    [Fact]
    public async Task Invalid_structured_plan_is_rejected()
    {
        var handler = new RecordingHandler(Final(FixtureCatalog.Plan(0)));
        var error = await Assert.ThrowsAsync<ShoppingException>(() => Planner(handler).PlanAsync(null, "نزهة", TestContext.Current.CancellationToken));
        Assert.Equal("INVALID_PLAN", error.Code);
    }

    [Fact]
    public async Task Provider_failure_is_safe_and_not_retried()
    {
        var handler = new RecordingHandler() { Status = HttpStatusCode.ServiceUnavailable };
        var error = await Assert.ThrowsAsync<ShoppingException>(() => Planner(handler).PlanAsync(null, "نزهة", TestContext.Current.CancellationToken));
        Assert.Equal("AI_PROVIDER_UNAVAILABLE", error.Code); Assert.Single(handler.Bodies);
        Assert.DoesNotContain("private", error.Message);
    }

    [Fact]
    public async Task Cost_admission_prevents_paid_request_when_budget_insufficient()
    {
        var handler = new RecordingHandler(Final());
        var error = await Assert.ThrowsAsync<ShoppingException>(() => Planner(handler, new() { MaxTurnEstimatedUsd = 0.00001m }).PlanAsync(null, "نزهة", TestContext.Current.CancellationToken));
        Assert.Equal("AI_BUDGET_EXCEEDED", error.Code); Assert.Empty(handler.Bodies);
    }

    [Fact]
    public async Task Cancellation_propagates_without_retry()
    {
        var handler = new RecordingHandler() { Delay = true };
        using var cancellation = new CancellationTokenSource(100);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Planner(handler).PlanAsync(null, "نزهة", cancellation.Token));
        Assert.Single(handler.Bodies);
    }

    private sealed class RecordingHandler(params string[] responses) : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];
        public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;
        public bool Delay { get; init; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Bodies.Add(await request.Content!.ReadAsStringAsync(ct));
            if (Delay) await Task.Delay(5000, ct);
            return new(Status) { Content = new StringContent(responses.Length > 0 ? responses[Math.Min(Bodies.Count - 1, responses.Length - 1)] : "{\"error\":{\"message\":\"private provider detail\"}}", Encoding.UTF8, "application/json") };
        }
    }
}
