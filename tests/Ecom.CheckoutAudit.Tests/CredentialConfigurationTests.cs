using System.Net;
using System.Text.Json;
using Ecom.Application.Services;
using Ecom.Application.Telegram;
using Ecom.Domain.Entity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ecom.CheckoutAudit.Tests;

public sealed class CredentialConfigurationTests
{
    [Fact]
    public void Api_defaults_and_shared_launch_template_do_not_supply_credentials()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Ecom.taani.slnx")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        var apiPath = Path.Combine(directory.FullName, "Ecom.API");
        var settings = new ConfigurationBuilder().SetBasePath(apiPath)
            .AddJsonFile("appsettings.json").AddJsonFile("appsettings.Development.json").Build();

        foreach (var key in new[]
                 {
                     "SmtpSettings:Password", "SmtpSettings:Username", "Telegram:BotToken", "Telegram:ChatId",
                     "JwtSettings:SecretKey", "ConnectionStrings:DefaultConnection", "Sadad:ClientKey",
                     "Sadad:ClientSecret", "Gupshup:ApiKey", "Gupshup:WebhookSecret",
                     "Gupshup_sandbox:ApiKey", "Gupshup_sandbox:WebhookSecret"
                 })
            Assert.True(string.IsNullOrWhiteSpace(settings[key]), $"The tracked defaults contain a value for {key}.");

        Assert.False(settings.GetValue<bool>("Telegram:Enabled"));
        using var launch = JsonDocument.Parse(File.ReadAllText(Path.Combine(apiPath, "Properties", "launchSettings.example.json")));
        foreach (var profile in launch.RootElement.GetProperty("profiles").EnumerateObject())
        {
            if (!profile.Value.TryGetProperty("environmentVariables", out var variables)) continue;
            Assert.True(variables.EnumerateObject().All(setting => setting.NameEquals("ASPNETCORE_ENVIRONMENT")),
                "The shared launch template injects a service setting.");
        }
    }

    [Fact]
    public async Task Invalid_injected_smtp_configuration_fails_without_logging_the_setting()
    {
        var marker = Guid.NewGuid().ToString("N");
        var prefix = $"ECOM_P0_{marker}_";
        var portVariable = prefix + "SmtpSettings__Port";
        var passwordVariable = prefix + "SmtpSettings__Password";
        Environment.SetEnvironmentVariable(portVariable, marker);
        Environment.SetEnvironmentVariable(passwordVariable, marker);
        try
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SmtpSettings:Host"] = "mail.invalid",
                    ["SmtpSettings:Username"] = "synthetic-account",
                    ["SmtpSettings:FromEmail"] = "sender@example.test"
                })
                .AddEnvironmentVariables(prefix).Build();
            Assert.True(config["SmtpSettings:Port"] == marker && config["SmtpSettings:Password"] == marker,
                "The environment override was not applied.");
            var logger = new MailLogger();
            var service = new EmailService(config, logger);

            Assert.False(await service.SendOtpEmailAsync(new AppUsers { Email = "recipient@example.test" }, "123456"));
            Assert.True(logger.Entries.Count == 1 &&
                        logger.Entries[0].Message.StartsWith("SMTP delivery skipped", StringComparison.Ordinal),
                "SMTP did not report a configuration failure.");
            Assert.True(logger.Entries.All(entry => entry.Error == null &&
                        !entry.Message.Contains(marker, StringComparison.Ordinal)),
                "SMTP diagnostics exposed an injected setting.");
        }
        finally
        {
            Environment.SetEnvironmentVariable(portVariable, null);
            Environment.SetEnvironmentVariable(passwordVariable, null);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Telegram_delivery_errors_exclude_response_text_and_credential_urls(bool transportError)
    {
        var ct = TestContext.Current.CancellationToken;
        var token = $"123456:{Guid.NewGuid():N}";
        var settings = new TelegramOptions { Enabled = true, ChatId = "synthetic-chat", BotToken = token };
        using var client = new HttpClient(new TelegramFailureHandler(token, transportError))
        {
            BaseAddress = new Uri("https://telegram.invalid/")
        };
        var service = new TelegramOrderNotificationService(null!, null!, client, Options.Create(settings),
            NullLogger<TelegramOrderNotificationService>.Instance);

        var failure = await Record.ExceptionAsync(() =>
            service.SendDocumentAsync(settings, "SYNTHETIC-ORDER", "Invoice", [1, 2], ct));
        var error = failure as TelegramDeliveryException;
        Assert.NotNull(error);

        Assert.Equal(transportError ? 0 : 429, error.StatusCode);
        Assert.True(error.Retryable);
        Assert.Equal(transportError ? null : TimeSpan.FromSeconds(12), error.RetryAfter);
        Assert.True(error.InnerException == null && !error.Message.Contains(token, StringComparison.Ordinal),
            "Telegram diagnostics exposed credential material.");
    }

    [Fact]
    public async Task Disabled_telegram_processing_leaves_queued_notifications_alone()
    {
        var processor = new TelegramOrderNotificationProcessor(null!, null!, Options.Create(new TelegramOptions()),
            NullLogger<TelegramOrderNotificationProcessor>.Instance);
        Assert.Equal(0, await processor.ProcessBatchAsync("test-worker", 10, TestContext.Current.CancellationToken));
    }

    private sealed class TelegramFailureHandler(string token, bool transportError) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (transportError)
                throw new HttpRequestException($"Synthetic failure for {request.RequestUri}");

            var body = JsonSerializer.Serialize(new { ok = false, description = token, parameters = new { retry_after = 12 } });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent(body)
            });
        }
    }

    private sealed class MailLogger : ILogger<EmailService>
    {
        public List<(string Message, Exception? Error)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Entries.Add((formatter(state, exception), exception));
    }
}
