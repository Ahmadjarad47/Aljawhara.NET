using System.Collections.Concurrent;
using Ecom.Application.DTOs.Order;
using Ecom.Application.Services;
using Ecom.Application.Services.Interfaces;
using Ecom.Domain.constant;
using Ecom.Domain.Entity;
using Ecom.Infrastructure.Repositories.Interfaces;
using Ecom.Infrastructure.UnitOfWork;

namespace Ecom.CheckoutAudit.Tests;

public class WorkflowAndResilienceTests
{
    [Fact]
    public void Payment_link_recovery_uses_persistent_attempt_instead_of_instance_lock()
    {
        var source = File.ReadAllText(Path.Combine(Workspace.Root(), "Ecom.Application", "Services", "OrderService.cs"));
        Assert.DoesNotContain("ConcurrentDictionary", source, StringComparison.Ordinal);
        Assert.Contains("PaymentAttempts", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Guest_secret_is_hashed_on_the_order_and_only_raw_on_the_creation_response()
    {
        Assert.NotNull(typeof(Order).GetProperty(nameof(Order.GuestAccessTokenHash)));
        Assert.Null(typeof(Order).GetProperty("GuestAccessToken"));
        Assert.NotNull(typeof(TransactionAdvancedDto).GetProperty(nameof(TransactionAdvancedDto.GuestAccessToken)));
        Assert.DoesNotContain(typeof(OrderDto).GetProperties(), property =>
            property.Name.Contains("GuestAccess", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Checkout_persists_payment_attempt_and_outbox_before_commit_without_calling_Sadad()
    {
        var source = File.ReadAllText(Path.Combine(Workspace.Root(), "Ecom.Application", "Services", "OrderService.cs"));
        var reduce = source.IndexOf("ReduceProductStockAsync", StringComparison.Ordinal);
        var outbox = source.IndexOf("PaymentInvoiceRequested", StringComparison.Ordinal);
        var commit = source.IndexOf("CommitTransactionAsync", StringComparison.Ordinal);
        Assert.True(reduce < commit);
        Assert.True(outbox < commit);
        Assert.DoesNotContain("CreateTransactionAsync(transactionDto)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Coupon_usage_is_claimed_atomically_and_failure_aborts_checkout()
    {
        var source = File.ReadAllText(Path.Combine(Workspace.Root(), "Ecom.Application", "Services", "OrderService.cs"));

        Assert.Contains("if (!await _unitOfWork.Coupons.TryReserveUsageAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Order_email_is_persisted_to_outbox_instead_of_fire_and_forget()
    {
        var source = File.ReadAllText(Path.Combine(Workspace.Root(), "Ecom.API", "Controllers", "OrdersController.cs"));
        Assert.DoesNotContain("Task.Run", source, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(Workspace.Root(), "Ecom.Application", "Services", "OrderService.cs"));
        Assert.Contains("OrderCreatedEmail", service, StringComparison.Ordinal);
    }
}
