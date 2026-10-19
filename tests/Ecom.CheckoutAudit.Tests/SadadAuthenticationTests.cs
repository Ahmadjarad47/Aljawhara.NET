using System.Net;
using System.Text;
using Ecom.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Ecom.CheckoutAudit.Tests;

public sealed class SadadAuthenticationTests
{
    [Theory]
    [InlineData("Sadad:")]
    [InlineData("")]
    public async Task Authentication_accepts_configured_credentials_and_sends_the_expected_headers(string prefix)
    {
        var handler = new SadadHandler((request, call) =>
        {
            Assert.Equal(call == 1 ? "/api/User/GenerateRefreshToken" : "/api/User/GenerateAccessToken", request.RequestUri!.AbsolutePath);
            Assert.Equal(call == 1 ? "Basic" : "Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal(call == 1 ? Convert.ToBase64String(Encoding.ASCII.GetBytes("config-key:config-secret")) : "refresh-test",
                request.Headers.Authorization?.Parameter);
            return SadadHandler.Json(call == 1 ? SadadResponses.Refresh : SadadResponses.Access);
        });
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [prefix + "ClientKey"] = "config-key", [prefix + "ClientSecret"] = "config-secret"
        }).Build();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://sadad.test/") };
        var service = CreateService(client, config, new RecordingLogger());

        var tokens = await service.GenerateTokens(TestContext.Current.CancellationToken);

        Assert.Equal("access-test", tokens.AccessToken);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Caller_cancellation_is_not_reported_as_an_authentication_timeout()
    {
        var handler = new SadadHandler((_, _) => throw new InvalidOperationException("Must not send a cancelled request"));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://sadad.test/") };
        var logger = new RecordingLogger();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateService(client, Configuration(), logger).GenerateTokens(cancellation.Token));

        Assert.Empty(logger.Messages);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Legacy_environment_credentials_remain_supported()
    {
        using var environment = new EnvironmentVariableScope();
        var handler = new SadadHandler((_, call) => SadadHandler.Json(call == 1 ? SadadResponses.Refresh : SadadResponses.Access));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://sadad.test/") };

        var tokens = await CreateService(client, null, new RecordingLogger()).GenerateTokens(TestContext.Current.CancellationToken);

        Assert.Equal("access-test", tokens.AccessToken);
        Assert.Equal(2, handler.Calls);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("key-only", null)]
    [InlineData(null, "secret-only")]
    [InlineData(" ", "secret")]
    public async Task Missing_credentials_are_reported_without_sending_a_gateway_request(string? key, string? secret)
    {
        var config = Configuration(key ?? "", secret ?? "");
        var handler = new SadadHandler((_, _) => throw new InvalidOperationException("Must not call Sadad"));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://sadad.test/") };
        var logger = new RecordingLogger();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService(client, config, logger).GenerateTokens(TestContext.Current.CancellationToken));

        Assert.Contains("SADAD_CREDENTIALS_MISSING", error.Message);
        Assert.Contains(logger.Messages, m => m.Contains("SADAD_CREDENTIALS_MISSING", StringComparison.Ordinal));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Partial_section_does_not_mix_with_a_legacy_credential_pair()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Sadad:ClientKey"] = "new-key",
            ["ClientKey"] = "old-key", ["ClientSecret"] = "old-secret"
        }).Build();
        var handler = new SadadHandler((_, _) => throw new InvalidOperationException("Must not call Sadad"));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://sadad.test/") };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService(client, config, new RecordingLogger()).GenerateTokens(TestContext.Current.CancellationToken));

        Assert.Contains("SADAD_CREDENTIALS_MISSING", error.Message);
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData(1, 401, "{}", "SADAD_REFRESH_HTTP_401")]
    [InlineData(2, 403, "{}", "SADAD_ACCESS_HTTP_403")]
    [InlineData(1, 200, "{\"isValid\":false}", "SADAD_REFRESH_REJECTED")]
    [InlineData(2, 200, "{\"isValid\":false}", "SADAD_ACCESS_REJECTED")]
    [InlineData(1, 200, "{\"isValid\":true,\"response\":{\"refreshToken\":\" \"}}", "SADAD_REFRESH_TOKEN_EMPTY")]
    [InlineData(2, 200, "{\"isValid\":true,\"response\":{\"accessToken\":\"\"}}", "SADAD_ACCESS_TOKEN_EMPTY")]
    [InlineData(1, 200, "<html>unexpected response</html>", "SADAD_REFRESH_INVALID_RESPONSE")]
    [InlineData(2, 200, "<html>unexpected response</html>", "SADAD_ACCESS_INVALID_RESPONSE")]
    public async Task Authentication_reports_the_stage_and_reason_without_logging_secrets(int failedCall, int status, string body, string reason)
    {
        var handler = new SadadHandler((_, call) => call == failedCall
            ? SadadHandler.Json(body, (HttpStatusCode)status)
            : SadadHandler.Json(SadadResponses.Refresh));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://sadad.test/") };
        var logger = new RecordingLogger();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService(client, Configuration(), logger).GenerateTokens(TestContext.Current.CancellationToken));

        Assert.Contains(reason, error.Message);
        Assert.Contains(logger.Messages, m => m.Contains(reason, StringComparison.Ordinal));
        Assert.Equal(failedCall, handler.Calls);
        var logs = string.Join('\n', logger.Messages);
        foreach (var sensitive in new[] { "config-key", "config-secret", "refresh-test", "unexpected response" })
            Assert.DoesNotContain(sensitive, logs, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1, false, "SADAD_REFRESH_NETWORK_ERROR")]
    [InlineData(2, false, "SADAD_ACCESS_NETWORK_ERROR")]
    [InlineData(1, true, "SADAD_REFRESH_TIMEOUT")]
    [InlineData(2, true, "SADAD_ACCESS_TIMEOUT")]
    public async Task Transport_failure_reports_a_safe_reason(int failedCall, bool timeout, string reason)
    {
        var handler = new SadadHandler((_, call) => call != failedCall
            ? SadadHandler.Json(SadadResponses.Refresh)
            : throw (timeout ? new TaskCanceledException("sensitive-upstream-detail") : new HttpRequestException("sensitive-upstream-detail")));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://sadad.test/") };
        var logger = new RecordingLogger();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService(client, Configuration(), logger).GenerateTokens(TestContext.Current.CancellationToken));

        Assert.Contains(reason, error.Message);
        Assert.Contains(logger.Messages, m => m.Contains(reason, StringComparison.Ordinal));
        Assert.DoesNotContain("sensitive-upstream-detail", string.Join('\n', logger.Messages));
        Assert.Equal(failedCall, handler.Calls);
    }

    private static IConfiguration Configuration(string key = "config-key", string secret = "config-secret") =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Sadad:ClientKey"] = key, ["Sadad:ClientSecret"] = secret
        }).Build();

    private static TransactionService CreateService(HttpClient client, IConfiguration? config, RecordingLogger logger) =>
        new(null!, null!, new StubHttpClientFactory(client), logger, config);

    private sealed class RecordingLogger : ILogger<TransactionService>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
