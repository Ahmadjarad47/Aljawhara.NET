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
    public async Task Concurrent_payment_link_requests_can_create_duplicate_external_invoices()
    {
        var order = new Order { Id = 55, OrderNumber = "AUDIT-55", Total = 10m };
        var orders = InterfaceStub.Create<IOrderRepository>((method, _) => method.Name switch
        {
            "GetByIdAsync" => Task.FromResult<Order?>(order),
            _ => throw new InvalidOperationException($"Unexpected order call: {method.Name}")
        });
        var uow = InterfaceStub.Create<IUnitOfWork>((method, _) => method.Name switch
        {
            "get_Orders" => orders,
            "Dispose" => null,
            _ => throw new InvalidOperationException($"Unexpected UoW call: {method.Name}")
        });
        var created = new ConcurrentBag<int>();
        var nextSequence = 0;
        var barrier = new Barrier(2);
        var transactionService = InterfaceStub.Create<ITransactionService>((method, args) => method.Name switch
        {
            "HasSuccessfulTransactionAsync" => Task.FromResult(false),
            "GetLatestTransactionByOrderAsync" => Task.FromResult<TransactionAdvancedDto?>(null),
            "CreateTransactionAsync" => Create((TransactionCreateAdvancedDto)args![0]!),
            _ => throw new InvalidOperationException($"Unexpected transaction service call: {method.Name}")
        });
        async Task<TransactionAdvancedDto> Create(TransactionCreateAdvancedDto dto)
        {
            await Task.Yield();
            Assert.True(barrier.SignalAndWait(TimeSpan.FromSeconds(5)));
            var sequence = Interlocked.Increment(ref nextSequence);
            created.Add(sequence);
            return new TransactionAdvancedDto
            {
                Id = sequence,
                OrderId = dto.OrderId,
                PaymentUrl = $"https://sadad.test/pay/{sequence}"
            };
        }
        var service = new OrderService(uow, TestMapper.Create(), null!, null!, transactionService);

        var results = await Task.WhenAll(
            service.GetOrderPaymentLinkAsync(order.Id),
            service.GetOrderPaymentLinkAsync(order.Id));

        Assert.Equal(2, created.Count);
        Assert.Equal(2, results.Distinct().Count());
    }

    [Fact]
    public void Guest_records_have_no_access_token_or_ownership_secret()
    {
        var guestFacingTypes = new[]
        {
            typeof(Order), typeof(Transaction), typeof(OrderDto), typeof(TransactionAdvancedDto), typeof(OrderCreateDto)
        };

        Assert.All(guestFacingTypes, type => Assert.DoesNotContain(type.GetProperties(), property =>
            property.Name.Contains("GuestToken", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("AccessToken", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("OwnershipToken", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void Order_flow_commits_stock_and_coupon_before_calling_Sadad()
    {
        var source = File.ReadAllText(Path.Combine(Workspace.Root(), "Ecom.Application", "Services", "OrderService.cs"));
        var reduce = source.IndexOf("ReduceProductStockAsync", StringComparison.Ordinal);
        var coupon = source.IndexOf("IncrementCouponUsageAsync", StringComparison.Ordinal);
        var commit = source.IndexOf("CommitTransactionAsync", StringComparison.Ordinal);
        var gateway = source.IndexOf("CreateTransactionAsync(transactionDto)", StringComparison.Ordinal);

        Assert.True(reduce < commit);
        Assert.True(coupon < commit);
        Assert.True(commit < gateway);
    }

    [Fact]
    public void Order_flow_ignores_coupon_usage_failure_result()
    {
        var source = File.ReadAllText(Path.Combine(Workspace.Root(), "Ecom.Application", "Services", "OrderService.cs"));

        Assert.Contains("await _couponService.IncrementCouponUsageAsync(order.CouponId.Value);", source, StringComparison.Ordinal);
        Assert.DoesNotContain("if (!await _couponService.IncrementCouponUsageAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Order_email_is_fire_and_forget_and_swallowing_exceptions()
    {
        var source = File.ReadAllText(Path.Combine(Workspace.Root(), "Ecom.API", "Controllers", "OrdersController.cs"));
        var taskRun = source.IndexOf("_ = Task.Run", StringComparison.Ordinal);
        var emptyCatch = source.IndexOf("catch", taskRun, StringComparison.Ordinal);

        Assert.True(taskRun >= 0);
        Assert.True(emptyCatch > taskRun);
    }
}
