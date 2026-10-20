using System.Net;
using System.Reflection;
using System.Text.Json;
using Ecom.Application.Payments;
using Ecom.Application.Services.Interfaces;
using Ecom.Domain.Entity;
using Ecom.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Ecom.CheckoutAudit.Tests;

public sealed class SadadInvoiceDiagnosticsTests
{
    [Fact]
    public void Diagnostics_are_disabled_by_default()
    {
        using var db = Context();
        var logger = new RecordingLogger();
        var gateway = Gateway(db, new ConfigurationBuilder().Build(), logger);

        Invoke(gateway, "LogCreateResponse", 17, HttpStatusCode.OK,
            "{\"isValid\":true,\"response\":{\"invoiceId\":\"9001\"}}");
        Invoke(gateway, "LogGetByIdAmounts", 17, HttpStatusCode.OK,
            "{\"isValid\":true,\"response\":{\"amount\":203.689,\"total\":203.689,\"subtotal\":195.541}}");

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void Enabled_diagnostics_preserve_decimal_json_and_exclude_sensitive_fields()
    {
        using var db = Context();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Sadad:Diagnostics:EnableInvoiceLogging"] = "true" }).Build();
        var logger = new RecordingLogger();
        var gateway = Gateway(db, configuration, logger);
        var attempt = new PaymentAttempt { Id = 17, MerchantReference = "REF-17", Amount = 203.689m, Currency = "KWD" };
        var order = new Order { Subtotal = 195.541m, Shipping = 8.148m, Tax = 0m, Total = 203.689m };
        const string payload = "{\"Invoices\":[{\"ref_Number\":\"REF-17\",\"amount\":203.689,\"customer_Name\":\"Private Customer\",\"customer_Email\":\"private@example.test\",\"currency_Code\":\"KWD\",\"items\":[{\"name\":\"Private product\",\"quantity\":1,\"amount\":195.541},{\"name\":\"Shipping\",\"quantity\":1,\"amount\":8.148}]}]}";

        Invoke(gateway, "LogCreateRequest", attempt, order, payload);
        Invoke(gateway, "LogCreateResponse", 17, HttpStatusCode.OK,
            "{\"isValid\":true,\"errorKey\":null,\"response\":{\"invoiceId\":\"9001\",\"customer_Email\":\"private@example.test\"}}");
        Invoke(gateway, "LogGetByIdAmounts", 17, HttpStatusCode.OK,
            "{\"isValid\":true,\"response\":{\"url\":\"https://sadad.test/pay/private\",\"amount\":203.689,\"total\":203.689,\"subtotal\":195.541,\"discount_Amount\":0.000,\"service_Fee\":8.148,\"customer_Email\":\"private@example.test\",\"authorization\":\"Bearer secret-token\"}}");

        Assert.Contains(logger.Entries, entry => entry.Message.StartsWith("[Sadad CREATE REQUEST]", StringComparison.Ordinal));
        Assert.Contains(logger.Entries, entry => entry.Message.StartsWith("[Sadad CREATE RESPONSE]", StringComparison.Ordinal));
        Assert.Contains(logger.Entries, entry => entry.Message.StartsWith("[Sadad GETBYID AMOUNTS]", StringComparison.Ordinal));
        var create = logger.Entries.Single(entry => entry.Message.StartsWith("[Sadad CREATE REQUEST]", StringComparison.Ordinal));
        Assert.Equal(203.689m, create.Properties["InvoiceAmount"]);
        Assert.Equal(203.689m, create.Properties["InvoiceItemsTotal"]);
        using (var financial = JsonDocument.Parse(Assert.IsType<string>(create.Properties["SerializedFinancialValues"])))
        {
            Assert.Equal(203.689m, financial.RootElement.GetProperty("amount").GetDecimal());
            Assert.Equal(195.541m, financial.RootElement.GetProperty("items")[0].GetProperty("amount").GetDecimal());
            Assert.Equal(8.148m, financial.RootElement.GetProperty("items")[1].GetProperty("amount").GetDecimal());
        }
        var get = logger.Entries.Single(entry => entry.Message.StartsWith("[Sadad GETBYID AMOUNTS]", StringComparison.Ordinal));
        Assert.Equal(203.689m, get.Properties["GatewayAmount"]);
        Assert.Equal(203.689m, get.Properties["GatewayTotal"]);
        Assert.Equal(195.541m, get.Properties["GatewaySubtotal"]);
        Assert.Contains("\"discount_Amount\":0.000", Assert.IsType<string>(get.Properties["GatewayDiscountFields"]));
        Assert.Contains("\"service_Fee\":8.148", Assert.IsType<string>(get.Properties["GatewayFeeFields"]));

        var logged = string.Join('\n', logger.Entries.SelectMany(entry =>
            entry.Properties.Values.Select(value => value?.ToString() ?? "").Prepend(entry.Message)));
        foreach (var secret in new[] { "Private Customer", "Private product", "private@example.test", "https://sadad.test/pay/private", "secret-token", "Bearer" })
            Assert.DoesNotContain(secret, logged, StringComparison.OrdinalIgnoreCase);
    }

    private static EcomDbContext Context() => new(new DbContextOptionsBuilder<EcomDbContext>()
        .UseNpgsql("Host=127.0.0.1;Database=unused;Username=unused;Password=unused").Options);

    private static SadadInvoiceGateway Gateway(EcomDbContext db, IConfiguration configuration, ILogger<SadadInvoiceGateway> logger)
    {
        var transactions = InterfaceStub.Create<ITransactionService>((method, _) => throw new InvalidOperationException(method.Name));
        var client = new HttpClient { BaseAddress = new Uri("https://sadad.test/") };
        return new SadadInvoiceGateway(db, transactions, new StubHttpClientFactory(client), configuration, logger);
    }

    private static void Invoke(SadadInvoiceGateway gateway, string methodName, params object?[] arguments) =>
        typeof(SadadInvoiceGateway).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(gateway, arguments);

    private sealed class RecordingLogger : ILogger<SadadInvoiceGateway>
    {
        public List<(string Message, Dictionary<string, object?> Properties)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = state is IEnumerable<KeyValuePair<string, object?>> fields
                ? fields.Where(field => field.Key != "{OriginalFormat}").ToDictionary(field => field.Key, field => field.Value)
                : [];
            Entries.Add((formatter(state, exception), properties));
        }
    }
}
