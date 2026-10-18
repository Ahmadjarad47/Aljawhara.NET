using System.Reflection;
using System.Text;
using AutoMapper;
using Ecom.Application.Mappings;
using Ecom.Application.Services;
using Ecom.Domain.Entity;
using Ecom.Infrastructure.Repositories.Interfaces;
using Ecom.Infrastructure.UnitOfWork;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ecom.CheckoutAudit.Tests;

internal class InterfaceStub : DispatchProxy
{
    public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = null!;

    public static T Create<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class
    {
        var instance = DispatchProxy.Create<T, InterfaceStub>();
        ((InterfaceStub)(object)instance).Handler = handler;
        return instance;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        Handler(targetMethod!, args);
}

internal sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => client;
}

internal sealed class SadadHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, int, HttpResponseMessage> _response;

    public SadadHandler(Func<HttpRequestMessage, int, HttpResponseMessage> response) => _response = response;

    public int Calls { get; private set; }
    public List<(string Path, string? Body)> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        var body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request.RequestUri!.AbsolutePath, body));
        return _response(request, Calls);
    }

    public static HttpResponseMessage Json(string json, System.Net.HttpStatusCode status = System.Net.HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}

internal sealed class TransactionHarness
{
    private readonly Order _order;
    private readonly Transaction? _gatewayTransaction;

    public TransactionHarness(Order order, SadadHandler handler, Transaction? gatewayTransaction = null)
    {
        _order = order;
        _gatewayTransaction = gatewayTransaction;
        Handler = handler;

        Orders = InterfaceStub.Create<IOrderRepository>((method, args) => method.Name switch
        {
            "GetOrderWithItemsAsync" or "GetByIdAsync" => Task.FromResult<Order?>(_order),
            "Update" => RecordOrderUpdate((Order)args![0]!),
            _ => throw new InvalidOperationException($"Unexpected order repository call: {method.Name}")
        });

        Transactions = InterfaceStub.Create<ITransactionRepository>((method, args) => method.Name switch
        {
            "AddAsync" => AddTransaction((Transaction)args![0]!),
            "GetTransactionWithDetailsAsync" => Task.FromResult(StoredTransaction),
            "GetByIdAsync" => Task.FromResult(StoredTransaction ?? _gatewayTransaction),
            "GetTransactionByGatewayInvoiceIdAsync" => Task.FromResult(_gatewayTransaction),
            "Update" => RecordTransactionUpdate((Transaction)args![0]!),
            "HasSuccessfulTransactionAsync" => Task.FromResult(false),
            _ => throw new InvalidOperationException($"Unexpected transaction repository call: {method.Name}")
        });

        UnitOfWork = InterfaceStub.Create<IUnitOfWork>((method, _) => method.Name switch
        {
            "get_Orders" => Orders,
            "get_Transactions" => Transactions,
            "SaveChangesAsync" => Save(),
            "Dispose" => null,
            _ => throw new InvalidOperationException($"Unexpected unit of work call: {method.Name}")
        });

        var client = new HttpClient(handler) { BaseAddress = new Uri("https://sadad.test/") };
        Service = new TransactionService(
            UnitOfWork,
            TestMapper.Create(),
            new StubHttpClientFactory(client),
            NullLogger<TransactionService>.Instance);
    }

    public SadadHandler Handler { get; }
    public IOrderRepository Orders { get; }
    public ITransactionRepository Transactions { get; }
    public IUnitOfWork UnitOfWork { get; }
    public TransactionService Service { get; }
    public Transaction? StoredTransaction { get; private set; }
    public int Saves { get; private set; }
    public int TransactionUpdates { get; private set; }
    public int OrderUpdates { get; private set; }

    private Task<Transaction> AddTransaction(Transaction transaction)
    {
        StoredTransaction = transaction;
        transaction.Id = 501;
        transaction.Order = _order;
        return Task.FromResult(transaction);
    }

    private object? RecordTransactionUpdate(Transaction transaction)
    {
        TransactionUpdates++;
        return null;
    }

    private object? RecordOrderUpdate(Order order)
    {
        OrderUpdates++;
        return null;
    }

    private Task<int> Save()
    {
        Saves++;
        return Task.FromResult(1);
    }
}

internal static class TestMapper
{
    public static IMapper Create() =>
        new MapperConfiguration(config => config.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
}

internal static class SadadResponses
{
    public const string Refresh = "{\"isValid\":true,\"response\":{\"refreshToken\":\"refresh-test\"}}";
    public const string Access = "{\"isValid\":true,\"response\":{\"accessToken\":\"access-test\",\"expiredAfterSeconds\":3600}}";
    public const string Insert = "{\"isValid\":true,\"response\":{\"invoiceId\":\"9001\"}}";
    public const string GetPending = "{\"isValid\":true,\"response\":{\"url\":\"https://sadad.test/pay/9001\",\"status\":\"Pending\"}}";

    public static HttpResponseMessage HappyPath(HttpRequestMessage request, string? insert = null, string? get = null) =>
        SadadHandler.Json(request.RequestUri!.AbsolutePath switch
        {
            "/api/User/GenerateRefreshToken" => Refresh,
            "/api/User/GenerateAccessToken" => Access,
            "/api/Invoice/insert" => insert ?? Insert,
            "/api/Invoice/getbyid" => get ?? GetPending,
            _ => throw new InvalidOperationException($"Unexpected Sadad request: {request.RequestUri}")
        });
}

internal sealed class EnvironmentVariableScope : IDisposable
{
    private readonly string? _oldKey = Environment.GetEnvironmentVariable("ClientKey");
    private readonly string? _oldSecret = Environment.GetEnvironmentVariable("ClientSecret");

    public EnvironmentVariableScope()
    {
        Environment.SetEnvironmentVariable("ClientKey", "audit-test-key");
        Environment.SetEnvironmentVariable("ClientSecret", "audit-test-secret");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("ClientKey", _oldKey);
        Environment.SetEnvironmentVariable("ClientSecret", _oldSecret);
    }
}

internal static class Workspace
{
    public static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Ecom.taani.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Workspace root was not found.");
    }
}
