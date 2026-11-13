using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ecom.Application.DTOs.Order;
using Ecom.Application.Payments;
using Ecom.Application.Services;
using Ecom.Application.Services.Interfaces;
using Ecom.Domain.constant;
using Ecom.Domain.Entity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Ecom.CheckoutAudit.Tests;

public sealed class RefundIntegrityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Disabled_refunds_and_admin_status_updates_preserve_financial_records(bool hasHistoricalRefund)
    {
        var ct = TestContext.Current.CancellationToken;
        var order = new Order { Id = 17, Status = OrderStatus.Delivered, Total = 25m };
        var transaction = new Transaction
        {
            Id = 42,
            OrderId = order.Id,
            Status = TransactionStatus.Paid,
            Amount = order.Total,
            IsRefunded = hasHistoricalRefund,
            RefundAmount = hasHistoricalRefund ? 3m : null,
            RefundDate = hasHistoricalRefund ? new DateTime(2026, 10, 18, 0, 0, 0, DateTimeKind.Utc) : null,
            RefundReason = hasHistoricalRefund ? "Historical entry" : null,
            Notes = "Original transaction notes",
            PaymentGatewayResponse = "Original gateway evidence"
        };
        var original = (transaction.Status, transaction.IsRefunded, transaction.RefundAmount,
            transaction.RefundDate, transaction.RefundReason, transaction.Notes, transaction.PaymentGatewayResponse);
        var gateway = new SadadHandler((_, _) => throw new InvalidOperationException("Unexpected provider call."));
        var harness = new TransactionHarness(order, gateway, transaction);
        var refund = new TransactionRefundDto
        {
            TransactionId = transaction.Id,
            RefundAmount = 5m,
            RefundReason = "Synthetic request",
            Notes = "Do not persist"
        };

        await using var baseFactory = new MonitoringApiFactory();
        await using var factory = baseFactory.WithWebHostBuilder(host => host.ConfigureTestServices(services =>
        {
            services.AddSingleton<ITransactionService>(harness.Service);
            services.AddSingleton<IOrderService>(new OrderService(harness.UnitOfWork, TestMapper.Create(),
                null!, null!, harness.Service, null!));
            services.AddSingleton(InterfaceStub.Create<ISadadPaymentVerificationService>((_, _) =>
                throw new InvalidOperationException("Unexpected verification call.")));
        }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        const string refundRoute = "/api/admin/transaction/refund";
        using var anonymous = await client.PostAsJsonAsync(refundRoute, refund, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        client.DefaultRequestHeaders.Add("X-Test-User", "shipping-admin");
        using var response = await client.PostAsJsonAsync(refundRoute, refund, ct);
        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken: ct);
        Assert.Equal("Refund processing is disabled.", problem?.Title);
        var eligibility = await client.GetFromJsonAsync<JsonElement>(
            $"/api/admin/transaction/{transaction.Id}/can-refund", ct);
        Assert.False(eligibility.GetProperty("canRefund").GetBoolean());
        await Assert.ThrowsAsync<NotSupportedException>(() => harness.Service.ProcessRefundAsync(refund));

        using var editTransaction = await client.PutAsJsonAsync($"/api/admin/transaction/{transaction.Id}",
            new TransactionUpdateDto { Id = transaction.Id, Status = TransactionStatus.Refunded, Notes = "Do not persist" }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, editTransaction.StatusCode);
        Assert.Contains("Refunded status requires verified refund confirmation.", await editTransaction.Content.ReadAsStringAsync(ct));

        using var editTransactionStatus = await client.PutAsJsonAsync($"/api/admin/transaction/{transaction.Id}/status",
            new { status = TransactionStatus.Refunded, gatewayResponse = "Unverified claim" }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, editTransactionStatus.StatusCode);
        Assert.Contains("Refunded status requires verified refund confirmation.", await editTransactionStatus.Content.ReadAsStringAsync(ct));

        using var editOrderStatus = await client.PutAsJsonAsync($"/api/admin/order/{order.Id}/status",
            new OrderUpdateStatusDto { Id = order.Id, Status = OrderStatus.Refunded }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, editOrderStatus.StatusCode);
        Assert.Contains("Refunded status requires verified refund confirmation.", await editOrderStatus.Content.ReadAsStringAsync(ct));

        Assert.Equal(original, (transaction.Status, transaction.IsRefunded, transaction.RefundAmount,
            transaction.RefundDate, transaction.RefundReason, transaction.Notes, transaction.PaymentGatewayResponse));
        Assert.Equal(OrderStatus.Delivered, order.Status);
        Assert.Equal(0, harness.TransactionUpdates);
        Assert.Equal(0, harness.OrderUpdates);
        Assert.Equal(0, harness.Saves);
        Assert.Equal(0, gateway.Calls);
    }
}
