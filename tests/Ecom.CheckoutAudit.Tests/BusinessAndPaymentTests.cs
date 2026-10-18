using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net;
using System.Text.Json;
using Ecom.Application.DTOs.Order;
using Ecom.Application.Services;
using Ecom.Application.Services.Interfaces;
using Ecom.Domain.constant;
using Ecom.Domain.Entity;
using Ecom.Infrastructure.Repositories.Interfaces;
using Ecom.Infrastructure.UnitOfWork;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ecom.CheckoutAudit.Tests;

public class BusinessAndPaymentTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void Order_item_rejects_non_positive_quantity(int quantity)
    {
        var dto = new OrderItemCreateDto { ProductId = 1, Quantity = quantity };

        Assert.False(IsValid(dto));
    }

    [Fact]
    public void Empty_order_is_model_invalid()
    {
        var dto = new OrderCreateDto
        {
            ShippingAddress = ValidAddress(),
            Items = []
        };

        Assert.False(IsValid(dto));
    }

    [Theory]
    [InlineData(-10)]
    [InlineData(0)]
    public void Legacy_transaction_rejects_non_positive_amount(double amount)
    {
        var dto = new TransactionCreateDto { OrderId = 1, Amount = (decimal)amount };

        Assert.False(IsValid(dto));
    }

    [Fact]
    public async Task Legacy_payment_service_is_disabled_without_persisting_a_transaction()
    {
        Transaction? stored = null;
        var transactions = InterfaceStub.Create<ITransactionRepository>((method, args) => method.Name switch
        {
            "AddAsync" => Add((Transaction)args![0]!),
            "GetTransactionWithDetailsAsync" => (object)Task.FromResult<Transaction?>(stored),
            _ => throw new InvalidOperationException($"Unexpected transaction call: {method.Name}")
        });
        Task<Transaction> Add(Transaction transaction)
        {
            stored = transaction;
            transaction.Id = 10;
            return Task.FromResult(transaction);
        }
        var uow = InterfaceStub.Create<IUnitOfWork>((method, _) => method.Name switch
        {
            "get_Transactions" => transactions,
            "SaveChangesAsync" => Task.FromResult(1),
            "Dispose" => null,
            _ => throw new InvalidOperationException($"Unexpected UoW call: {method.Name}")
        });
        var service = new OrderService(uow, TestMapper.Create(), null!, null!, null!);

        await Assert.ThrowsAsync<NotSupportedException>(() => service.ProcessPaymentAsync(new TransactionCreateDto
        {
            OrderId = 999999,
            Amount = 250m,
            PaymentMethod = PaymentMethod.Bank,
            Status = TransactionStatus.Paid
        }));

        Assert.Null(stored);
    }

    [Fact]
    public async Task Admin_completion_cannot_mark_electronic_transaction_paid_without_gateway_verification()
    {
        var order = Order(25m, 25m);
        var transaction = new Transaction
        {
            Id = 88,
            OrderId = order.Id,
            Order = order,
            Amount = order.Total,
            GatewayInvoiceId = "9001",
            Status = TransactionStatus.Pending
        };
        var handler = new SadadHandler((_, _) => throw new InvalidOperationException("Gateway must not be called"));
        var harness = new TransactionHarness(order, handler, transaction);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Service.MarkTransactionAsCompletedAsync(transaction.Id, "manual-reference"));

        Assert.Equal(TransactionStatus.Pending, transaction.Status);
        Assert.Equal(0, handler.Calls);
        Assert.Equal(0, harness.Saves);
    }

    [Fact]
    public async Task Cash_on_delivery_completion_uses_collected_not_paid()
    {
        var order = Order(25m, 25m);
        var transaction = new Transaction
        {
            Id = 89,
            OrderId = order.Id,
            Order = order,
            Amount = order.Total,
            PaymentMethod = PaymentMethod.Cod,
            Status = TransactionStatus.Pending
        };
        var harness = new TransactionHarness(order, new SadadHandler((_, _) => throw new InvalidOperationException()), transaction);

        var updated = await harness.Service.MarkTransactionAsCompletedAsync(transaction.Id, "cod-receipt");

        Assert.True(updated);
        Assert.Equal(TransactionStatus.Collected, transaction.Status);
        Assert.NotEqual(TransactionStatus.Paid, transaction.Status);
        Assert.Equal("cod-receipt", transaction.TransactionReference);
        Assert.Equal(1, harness.Saves);
    }

    [Fact]
    public async Task Negative_stock_reduction_is_rejected_without_changing_inventory()
    {
        var product = new Product { Id = 4, TotalInStock = 5, IsInStock = true };
        var products = InterfaceStub.Create<IProductRepository>((method, _) => method.Name switch
        {
            "FirstOrDefaultAsync" => Task.FromResult<Product?>(product),
            "Update" => null,
            _ => throw new InvalidOperationException($"Unexpected product call: {method.Name}")
        });
        var uow = InterfaceStub.Create<IUnitOfWork>((method, _) => method.Name switch
        {
            "get_Products" => products,
            "SaveChangesAsync" => Task.FromResult(1),
            "Dispose" => null,
            _ => throw new InvalidOperationException($"Unexpected UoW call: {method.Name}")
        });
        var service = new ProductService(uow, null!, null!, null!);

        var accepted = await service.ReduceProductStockAsync(4, -3);

        Assert.False(accepted);
        Assert.Equal(5, product.TotalInStock);
    }

    [Fact]
    public async Task Transaction_amount_is_always_derived_from_the_server_order_total()
    {
        using var environment = new EnvironmentVariableScope();
        var order = Order(total: 10m, itemPrice: 10m);
        var handler = new SadadHandler((request, _) => SadadResponses.HappyPath(request));
        var harness = new TransactionHarness(order, handler);

        await harness.Service.CreateTransactionAsync(new TransactionCreateAdvancedDto
        {
            OrderId = order.Id,
            Amount = 0.01m,
            PaymentMethod = PaymentMethod.Bank
        });

        Assert.Equal(10m, harness.StoredTransaction!.Amount);
        using var payload = InsertPayload(handler);
        Assert.Equal(10m, payload.RootElement.GetProperty("Invoices")[0].GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task Invoice_success_without_invoice_id_is_rejected_without_persistence()
    {
        using var environment = new EnvironmentVariableScope();
        var order = Order(10m, 10m);
        var handler = new SadadHandler((request, _) => SadadResponses.HappyPath(
            request,
            insert: "{\"isValid\":true,\"response\":{}}"));
        var harness = new TransactionHarness(order, handler);

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Service.CreateTransactionAsync(new TransactionCreateAdvancedDto
        {
            OrderId = order.Id,
            Amount = order.Total,
            PaymentMethod = PaymentMethod.Bank
        }));

        Assert.Null(harness.StoredTransaction);
        Assert.Equal(0, harness.Saves);
    }

    [Fact]
    public async Task Missing_loaded_coupon_fails_closed_before_invoice_creation()
    {
        using var environment = new EnvironmentVariableScope();
        var order = Order(8m, 10m);
        order.Subtotal = 10m;
        order.CouponId = 12;
        order.CouponDiscountAmount = 2m;
        order.Coupon = null;
        var handler = new SadadHandler((request, _) => SadadResponses.HappyPath(request));
        var harness = new TransactionHarness(order, handler);

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Service.CreateTransactionAsync(new TransactionCreateAdvancedDto
        {
            OrderId = order.Id,
            Amount = order.Total,
            PaymentMethod = PaymentMethod.Bank
        }));

        Assert.DoesNotContain(handler.Requests, request => request.Path == "/api/Invoice/insert");
        Assert.Null(harness.StoredTransaction);
    }

    [Fact]
    public async Task Invoice_amount_is_a_culture_independent_json_number()
    {
        using var environment = new EnvironmentVariableScope();
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
            var order = Order(12.345m, 12.345m);
            var handler = new SadadHandler((request, _) => SadadResponses.HappyPath(request));
            var harness = new TransactionHarness(order, handler);

            await harness.Service.CreateTransactionAsync(new TransactionCreateAdvancedDto
            {
                OrderId = order.Id,
                Amount = order.Total,
                PaymentMethod = PaymentMethod.Bank
            });

            using var payload = InsertPayload(handler);
            Assert.Equal(12.345m, payload.RootElement.GetProperty("Invoices")[0].GetProperty("amount").GetDecimal());
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Sadad_invoice_http_errors_do_not_persist_a_transaction(HttpStatusCode status)
    {
        using var environment = new EnvironmentVariableScope();
        var order = Order(10m, 10m);
        var handler = new SadadHandler((request, _) => request.RequestUri!.AbsolutePath switch
        {
            "/api/User/GenerateRefreshToken" => SadadHandler.Json(SadadResponses.Refresh),
            "/api/User/GenerateAccessToken" => SadadHandler.Json(SadadResponses.Access),
            "/api/Invoice/insert" => SadadHandler.Json("{\"error\":\"synthetic\"}", status),
            _ => throw new InvalidOperationException()
        });
        var harness = new TransactionHarness(order, handler);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Service.CreateTransactionAsync(new TransactionCreateAdvancedDto
            {
                OrderId = order.Id,
                Amount = order.Total,
                PaymentMethod = PaymentMethod.Bank
            }));

        Assert.Equal("Payment gateway invoice creation failed.", exception.Message);
        Assert.DoesNotContain("synthetic", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(harness.StoredTransaction);
        Assert.Equal(0, harness.Saves);
    }

    [Fact]
    public async Task Undocumented_or_mismatched_gateway_response_cannot_mark_transaction_paid()
    {
        using var environment = new EnvironmentVariableScope();
        var order = Order(25m, 25m);
        var transaction = new Transaction
        {
            Id = 7,
            OrderId = order.Id,
            Order = order,
            Amount = 25m,
            GatewayInvoiceId = "9001",
            Status = TransactionStatus.Pending
        };
        var mismatched = "{\"isValid\":true,\"response\":{\"status\":\"Paid\",\"amount\":0.001,\"currency_Code\":\"USD\",\"ref_Number\":\"different-order\"}}";
        var handler = new SadadHandler((request, _) => SadadResponses.HappyPath(request, get: mismatched));
        var harness = new TransactionHarness(order, handler, transaction);

        var updated = await harness.Service.HandleSadadPaidWebhookAsync(new SadadWebhookDto
        {
            InvoiceId = 9001,
            Status = "Paid",
            Amount = 0.001m
        });

        Assert.False(updated);
        Assert.Equal(TransactionStatus.Pending, transaction.Status);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(0, harness.Saves);
    }

    [Fact]
    public async Task Duplicate_verified_paid_webhook_is_idempotent_without_an_additional_gateway_call()
    {
        using var environment = new EnvironmentVariableScope();
        var order = Order(25m, 25m);
        var transaction = new Transaction
        {
            Id = 7,
            OrderId = order.Id,
            Order = order,
            Amount = 25m,
            GatewayInvoiceId = "9001",
            Status = TransactionStatus.Paid,
            GatewayVerifiedAtUtc = DateTime.UtcNow
        };
        var paid = "{\"isValid\":true,\"response\":{\"status\":\"Paid\"}}";
        var handler = new SadadHandler((request, _) => SadadResponses.HappyPath(request, get: paid));
        var harness = new TransactionHarness(order, handler, transaction);

        var result = await harness.Service.HandleSadadPaidWebhookAsync(new SadadWebhookDto { InvoiceId = 9001, Status = "Paid" });

        Assert.True(result);
        Assert.Equal(0, handler.Calls);
        Assert.Equal(0, harness.Saves);
        Assert.Equal(0, harness.TransactionUpdates);
    }

    [Fact]
    public async Task Network_interruption_during_invoice_creation_propagates_with_no_local_transaction()
    {
        using var environment = new EnvironmentVariableScope();
        var order = Order(10m, 10m);
        var handler = new SadadHandler((request, _) => request.RequestUri!.AbsolutePath switch
        {
            "/api/User/GenerateRefreshToken" => SadadHandler.Json(SadadResponses.Refresh),
            "/api/User/GenerateAccessToken" => SadadHandler.Json(SadadResponses.Access),
            "/api/Invoice/insert" => throw new HttpRequestException("synthetic connection interruption"),
            _ => throw new InvalidOperationException()
        });
        var harness = new TransactionHarness(order, handler);

        await Assert.ThrowsAsync<HttpRequestException>(() => harness.Service.CreateTransactionAsync(new TransactionCreateAdvancedDto
        {
            OrderId = order.Id,
            Amount = order.Total,
            PaymentMethod = PaymentMethod.Bank
        }));
        Assert.Null(harness.StoredTransaction);
        Assert.Equal(0, harness.Saves);
    }

    [Fact]
    public void Production_transaction_model_has_no_idempotency_or_concurrency_key()
    {
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<Ecom.Infrastructure.Data.EcomDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=metadata-only;Trusted_Connection=True")
            .Options;
        using var context = new Ecom.Infrastructure.Data.EcomDbContext(options);
        var entity = context.Model.FindEntityType(typeof(Transaction))!;

        Assert.False(entity.FindProperty(nameof(Transaction.GatewayInvoiceId))!.IsConcurrencyToken);
        Assert.DoesNotContain(entity.GetIndexes(), index => index.IsUnique &&
            index.Properties.Any(property => property.Name is nameof(Transaction.GatewayInvoiceId) or nameof(Transaction.TransactionReference)));
    }

    private static bool IsValid(object value) =>
        Validator.TryValidateObject(value, new ValidationContext(value), [], validateAllProperties: true);

    private static ShippingAddressCreateDto ValidAddress() => new()
    {
        FullName = "Synthetic Customer",
        Phone = "00000000",
        Street = "Synthetic Street"
    };

    private static Order Order(decimal total, decimal itemPrice) => new()
    {
        Id = 41,
        OrderNumber = "ORD-AUDIT-41",
        Subtotal = itemPrice,
        Total = total,
        ShippingAddress = new ShippingAddress
        {
            FullName = "Synthetic Guest",
            Phone = "00000000",
            Street = "Synthetic Street"
        },
        Items =
        [
            new OrderItem { Id = 1, ProductId = 1, Name = "Synthetic Item", Price = itemPrice, Quantity = 1 }
        ]
    };

    private static JsonDocument InsertPayload(SadadHandler handler)
    {
        var request = Assert.Single(handler.Requests, item => item.Path == "/api/Invoice/insert");
        return JsonDocument.Parse(request.Body!);
    }
}
