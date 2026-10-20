using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ecom.API.Monitoring;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication;

namespace Ecom.CheckoutAudit.Tests;

public sealed class MonitoringTests
{
    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("ordinary-user", HttpStatusCode.Forbidden)]
    [InlineData("shipping-admin", HttpStatusCode.OK)]
    public async Task Monitoring_data_requires_an_admin_for_every_endpoint(string? user, HttpStatusCode expected)
    {
        await using var factory = new MonitoringApiFactory();
        using var client = factory.CreateMonitoringClient(user);
        foreach (var path in new[] { "summary", "logs" })
            Assert.Equal(expected, (await client.GetAsync("/api/admin/monitoring/" + path, TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public void Payment_amount_diagnostics_allow_decimals_and_reject_arbitrary_strings()
    {
        foreach (var field in new[]
        {
            "ExpectedAmount", "ExpectedGrossAmount", "ExpectedDiscountAmount", "GatewayAmount", "GatewayTotal", "GatewaySubtotal",
            "OrderSubtotal", "Shipping", "Tax", "Discount", "OrderTotal", "PaymentAttemptAmount", "InvoiceAmount", "InvoiceItemsTotal"
        })
        {
            Assert.Equal("12.345", LogPrivacy.Value(field, 12.345m));
            Assert.Equal("[redacted]", LogPrivacy.Value(field, "customer@example.test"));
            Assert.Equal("[redacted]", LogPrivacy.Value(field, "Bearer sensitive-token"));
        }
    }

    [Fact]
    public async Task Dashboard_shell_and_assets_are_available_without_exposing_data()
    {
        await using var factory = new MonitoringApiFactory();
        using var client = factory.CreateMonitoringClient();
        var response = await client.GetAsync("/admin/monitoring", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Sign in to monitoring", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/monitoring/app.js", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/monitoring/style.css", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Requests_errors_and_database_health_appear_without_a_database_dependency()
    {
        await using var factory = new MonitoringApiFactory();
        using var client = factory.CreateMonitoringClient("shipping-admin");
        var success = await client.GetAsync("/monitoring-probe/ok?token=query-secret&email=customer@example.test", TestContext.Current.CancellationToken);
        Assert.True(success.Headers.Contains("X-Trace-Id"));
        var failure = await client.GetAsync("/monitoring-probe/fail", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.InternalServerError, failure.StatusCode);
        var errorBody = await failure.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("exception-secret", errorBody);

        var summary = await client.GetFromJsonAsync<JsonElement>("/api/admin/monitoring/summary", TestContext.Current.CancellationToken);
        Assert.Equal(2, summary.GetProperty("requests").GetInt32());
        Assert.Equal(1, summary.GetProperty("serverErrors").GetInt32());
        Assert.Equal(60, summary.GetProperty("series").GetArrayLength());
        var again = await client.GetFromJsonAsync<JsonElement>("/api/admin/monitoring/summary", TestContext.Current.CancellationToken);
        Assert.Equal(2, again.GetProperty("requests").GetInt32());

        var trace = success.Headers.GetValues("X-Trace-Id").Single();
        var logs = await client.GetFromJsonAsync<JsonElement>("/api/admin/monitoring/logs?traceId=" + Uri.EscapeDataString(trace), TestContext.Current.CancellationToken);
        Assert.True(logs.GetProperty("items").GetArrayLength() > 0);
        var raw = logs.GetRawText();
        Assert.DoesNotContain("query-secret", raw);
        Assert.DoesNotContain("customer@example.test", raw);
        Assert.Contains("monitoring-probe/ok", raw);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=201")]
    [InlineData("level=invalid")]
    [InlineData("from=2026-10-20T00:00:00Z&to=2026-10-19T00:00:00Z")]
    public async Task Log_query_rejects_invalid_or_unbounded_input(string query)
    {
        await using var factory = new MonitoringApiFactory();
        using var client = factory.CreateMonitoringClient("shipping-admin");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.GetAsync("/api/admin/monitoring/logs?" + query, TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Structured_logs_are_redacted_filterable_paginated_and_not_cached()
    {
        await using var factory = new MonitoringApiFactory();
        using var client = factory.CreateMonitoringClient("shipping-admin");
        var logger = factory.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Ecom.Tests");
        logger.LogWarning(new InvalidOperationException("exception-secret"),
            "Payment {OrderId} failed: {Email} {Password} {Token} {Payload}",
            42, "customer@example.test", "password-secret", "token-secret", "private-customer-data");
        logger.LogWarning("Payment {OrderId} completed", 43);
        var response = await client.GetAsync("/api/admin/monitoring/logs?category=Ecom.Tests&level=Warning&pageSize=1", TestContext.Current.CancellationToken);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var data = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(2, data.GetProperty("totalCount").GetInt32());
        Assert.Single(data.GetProperty("items").EnumerateArray());
        var raw = await client.GetStringAsync("/api/admin/monitoring/logs?category=Ecom.Tests&page=2&pageSize=1", TestContext.Current.CancellationToken);
        Assert.Contains("InvalidOperationException", raw);
        foreach (var secret in new[] { "customer@example.test", "password-secret", "token-secret", "private-customer-data", "exception-secret" })
            Assert.DoesNotContain(secret, raw);
        Assert.Contains("42", raw);
    }

    [Fact]
    public async Task Logs_rotate_restore_and_prune_and_memory_stays_bounded()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ecom-monitoring-tests-" + Guid.NewGuid().ToString("N"));
        var options = new MonitoringOptions { Directory = directory, MaxFileBytes = 600, MaxFiles = 2,
            RecentEventLimit = 3, QueueCapacity = 100 };
        var store = new MonitoringStore(Options.Create(options), new MonitoringTestEnvironment());
        using var worker = new MonitoringPersistenceWorker(store);
        await worker.StartAsync(TestContext.Current.CancellationToken);
        for (var i = 0; i < 10; i++) store.Add(Event(i));
        store.Complete();
        await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await worker.StopAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, store.Snapshot().Length);
        Assert.InRange(Directory.GetFiles(directory).Length, 1, 2);
        Assert.Null(store.PersistenceError);
        var restored = new MonitoringStore(Options.Create(options), new MonitoringTestEnvironment());
        restored.RestoreRecent();
        Assert.Equal(store.Snapshot().Select(e => e.Id), restored.Snapshot().Select(e => e.Id));
        var expired = Path.Combine(directory, "events-20000101-000000.jsonl");
        await File.WriteAllTextAsync(expired, "old", TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(expired, DateTime.UtcNow.AddDays(-20));
        store.Prune();
        Assert.False(File.Exists(expired));
        foreach (var file in Directory.GetFiles(directory)) File.Delete(file);
        Directory.Delete(directory);
    }

    [Fact]
    public async Task Disk_failure_and_queue_overflow_are_visible_without_breaking_requests()
    {
        var invalidDirectory = Path.GetTempFileName();
        var store = new MonitoringStore(Options.Create(new MonitoringOptions { Directory = invalidDirectory, QueueCapacity = 1 }), new MonitoringTestEnvironment());
        store.Add(Event(1));
        store.Add(Event(2));
        Assert.Equal(1, store.DroppedEvents);
        using var worker = new MonitoringPersistenceWorker(store);
        await worker.StartAsync(TestContext.Current.CancellationToken);
        store.Complete();
        await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await worker.StopAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(store.PersistenceError);
        Assert.Equal(2, store.DroppedEvents);
        File.Delete(invalidDirectory);
    }

    private static MonitoringEvent Event(int id) => new(id.ToString(), DateTimeOffset.UtcNow, "Information", "Ecom.Tests",
        "Test log " + new string('x', 100), null, null, null, new Dictionary<string, string?>());
}

internal sealed class MonitoringApiFactory : WebApplicationFactory<Program>
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ecom-monitoring-api-" + Guid.NewGuid().ToString("N"));
    public HttpClient CreateMonitoringClient(string? user = null)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        if (user != null) client.DefaultRequestHeaders.Add("X-Test-User", user);
        return client;
    }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JwtSettings:SecretKey"] = "monitoring-test-signing-key-at-least-thirty-two-bytes",
            ["Monitoring:Directory"] = _directory
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<HealthCheckService>();
            services.AddSingleton<HealthCheckService, MonitoringTestHealthChecks>();
            services.AddControllers().AddApplicationPart(typeof(MonitoringProbeController).Assembly);
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.SchemeName, _ => { });
        });
    }
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (Directory.Exists(_directory))
        {
            foreach (var file in Directory.GetFiles(_directory)) File.Delete(file);
            Directory.Delete(_directory);
        }
    }
}

internal sealed class MonitoringTestEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Testing";
    public string ApplicationName { get; set; } = "MonitoringTests";
    public string ContentRootPath { get; set; } = Path.GetTempPath();
    public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
}

internal sealed class MonitoringTestHealthChecks : HealthCheckService
{
    public override Task<HealthReport> CheckHealthAsync(Func<HealthCheckRegistration, bool>? predicate,
        CancellationToken cancellationToken = default) => Task.FromResult(new HealthReport(new Dictionary<string, HealthReportEntry>
        {
            ["database"] = new(HealthStatus.Healthy, null, TimeSpan.FromMilliseconds(1), null, null)
        }, TimeSpan.FromMilliseconds(1)));
}

[ApiController, AllowAnonymous, Route("monitoring-probe")]
public sealed class MonitoringProbeController : ControllerBase
{
    [HttpGet("ok")] public IActionResult Success() => Ok();
    [HttpGet("fail")] public IActionResult Fail() => throw new InvalidOperationException("exception-secret");
}
