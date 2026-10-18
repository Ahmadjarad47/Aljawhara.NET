using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using AutoMapper;
using Ecom.Application.DTOs.Order;
using Ecom.Application.Mappings;
using Ecom.Application.Services;
using Ecom.Application.Services.Interfaces;
using Ecom.Domain.constant;
using Ecom.Domain.Entity;
using Ecom.Infrastructure.Repositories.Interfaces;
using Ecom.Infrastructure.UnitOfWork;
using Microsoft.Extensions.Logging.Abstractions;

static void Check(bool value, string message) { if (!value) throw new Exception(message); }
static bool Valid(object value) => Validator.TryValidateObject(value, new ValidationContext(value), [], true);
static async Task Reject(Func<Task> action)
{
    try { await action(); }
    catch (ArgumentException) { return; }
    catch (InvalidOperationException) { return; }
    throw new Exception("Expected rejection");
}
var mapper = new MapperConfiguration(c => c.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
Check(new OrderCreateDto().PaymentMethod == PaymentMethod.Bank, "Backward compatibility failed");
foreach (var method in Enum.GetValues<PaymentMethod>())
    Check(Valid(new OrderCreateDto { PaymentMethod = method }), "Valid method rejected");
Check(!Valid(new OrderCreateDto { PaymentMethod = (PaymentMethod)99 }), "Invalid method accepted");
var order = new Order { Id = 10, OrderNumber = "CHECK-10", Total = 20, ShippingAddress = new ShippingAddress { FullName = "بدر", Phone = "213123213", Street = "الفروانية - عبدالله المبارك" } };
Transaction? stored = null;
var orders = Stub.Make<IOrderRepository>((name, a) => name switch {
    "GetOrderWithItemsAsync" or "GetByIdAsync" => Task.FromResult<Order?>(order),
    _ => throw new Exception("Unexpected order call: " + name)
});
var transactions = Stub.Make<ITransactionRepository>((name, a) => name switch {
    "AddAsync" => Add((Transaction)a![0]!),
    "GetTransactionWithDetailsAsync" or "GetByIdAsync" => Task.FromResult(stored),
    "HasSuccessfulTransactionAsync" => Task.FromResult(false),
    _ => throw new Exception("Unexpected transaction call: " + name)
});
Task<Transaction> Add(Transaction t) { stored = t; t.Id = 50; t.Order = order; return Task.FromResult(t); }
var uow = Stub.Make<IUnitOfWork>((name, a) => name switch {
    "get_Orders" => orders, "get_Transactions" => transactions,
    "SaveChangesAsync" => Task.FromResult(1),
    _ => throw new Exception("Unexpected unit of work call: " + name)
});
var handler = new Gateway();
var client = new HttpClient(handler) { BaseAddress = new Uri("https://test.invalid") };
var factory = Stub.Make<IHttpClientFactory>((name, a) => client);
var service = new TransactionService(uow, mapper, factory, NullLogger<TransactionService>.Instance);
Environment.SetEnvironmentVariable("ClientKey", null);
Environment.SetEnvironmentVariable("ClientSecret", null);
var cod = await service.CreateTransactionAsync(new TransactionCreateAdvancedDto { OrderId = 10, Amount = 20, PaymentMethod = PaymentMethod.Cod, Status = TransactionStatus.Paid, GatewayInvoiceId = "ignore" });
Check(cod.PaymentMethod == PaymentMethod.Cod && cod.Status == TransactionStatus.Pending && cod.PaymentUrl == null && cod.GatewayInvoiceId == null && handler.Calls == 0, "COD created online payment or marked paid");
Check(await service.GetPaymentUrlByTransactionIdAsync(50) == null && handler.Calls == 0, "COD payment URL lookup called gateway");
await service.ProcessPaymentAsync(new PaymentProcessingDto { OrderId = 10, PaymentMethod = PaymentMethod.Cod });
Check(stored!.Status == TransactionStatus.Pending && order.Status == OrderStatus.Pending && handler.Calls == 0, "COD payment processing marked paid or called gateway");
var transactionService = Stub.Make<ITransactionService>((name, a) => name switch {
    "HasSuccessfulTransactionAsync" => Task.FromResult(false),
    "GetLatestTransactionByOrderAsync" => Task.FromResult<TransactionAdvancedDto?>(cod),
    _ => throw new Exception("Unexpected service call: " + name)
});
var orderService = new OrderService(uow, mapper, null!, null!, transactionService);
await Reject(async () => await orderService.GetOrderPaymentLinkAsync(10));
await Reject(async () => await orderService.CreateOrderAsync(new OrderCreateDto { PaymentMethod = (PaymentMethod)99 }));
await Reject(async () => await service.CreateTransactionAsync(new TransactionCreateAdvancedDto { PaymentMethod = (PaymentMethod)99 }));
Console.WriteLine("PASS COD: persisted pending, zero gateway calls, null URL, payment-link rejected, invalid methods rejected");
Environment.SetEnvironmentVariable("ClientKey", "test");
Environment.SetEnvironmentVariable("ClientSecret", "test");
var online = await service.CreateTransactionAsync(new TransactionCreateAdvancedDto { OrderId = 10, Amount = 20, PaymentMethod = PaymentMethod.Bank });
Check(online.PaymentMethod == PaymentMethod.Bank && online.Status == TransactionStatus.Pending && online.GatewayInvoiceId == "123" && online.PaymentUrl == "https://test.invalid/pay/123" && handler.Calls == 4, "Electronic payment regression");
Console.WriteLine("PASS electronic payment: mocked Sadad tokens, invoice creation, returned payment URL");

public class Stub : DispatchProxy
{
    public Func<string, object?[]?, object?> Handler = null!;
    public static T Make<T>(Func<string, object?[]?, object?> handler) where T : class
    {
        var instance = Create<T, Stub>();
        ((Stub)(object)instance).Handler = handler;
        return instance;
    }
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!.Name, args);
}
public class Gateway : HttpMessageHandler
{
    public int Calls;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        Calls++;
        var payload = request.RequestUri!.AbsolutePath switch {
            "/api/User/GenerateRefreshToken" => "{\"isValid\":true,\"response\":{\"refreshToken\":\"test\"}}",
            "/api/User/GenerateAccessToken" => "{\"isValid\":true,\"response\":{\"accessToken\":\"test\",\"expiredAfterSeconds\":3600}}",
            "/api/Invoice/insert" => "{\"isValid\":true,\"response\":{\"invoiceId\":\"123\"}}",
            "/api/Invoice/getbyid" => "{\"isValid\":true,\"response\":{\"url\":\"https://test.invalid/pay/123\",\"status\":\"Pending\"}}",
            _ => throw new Exception("Unexpected gateway request")
        };
        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(payload) });
    }
}
