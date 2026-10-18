using System.Security.Claims;
using Ecom.API.Controllers;
using Ecom.Application.DTOs.Order;
using Ecom.Application.Services.Interfaces;
using Ecom.Domain.constant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ecom.CheckoutAudit.Tests;

public class AuthorizationAndControllerTests
{
    public static TheoryData<string> AnonymousSensitiveActions => new()
    {
        nameof(OrdersController.GetOrderByNumber),
        nameof(OrdersController.ProcessPayment),
        nameof(OrdersController.GetOrderTransactions),
        nameof(OrdersController.GetInvoicePaymentData),
        nameof(OrdersController.PaymentCallback)
    };

    [Theory]
    [MemberData(nameof(AnonymousSensitiveActions))]
    public void Sensitive_order_action_has_no_authorization_requirement(string actionName)
    {
        var method = typeof(OrdersController).GetMethod(actionName)!;

        Assert.Empty(method.GetCustomAttributes(typeof(AuthorizeAttribute), true));
        Assert.Empty(typeof(OrdersController).GetCustomAttributes(typeof(AuthorizeAttribute), true));
    }

    [Fact]
    public void Application_has_no_global_fallback_authorization_policy()
    {
        var source = File.ReadAllText(Path.Combine(Workspace.Root(), "Ecom.API", "Program.cs"));

        Assert.DoesNotContain("FallbackPolicy", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Anonymous_order_number_lookup_returns_guest_personal_data()
    {
        var exposed = new OrderDto
        {
            Id = 44,
            OrderNumber = "ORD-PUBLIC",
            AppUserId = "customer-b",
            ShippingAddress = new ShippingAddressDto
            {
                FullName = "Synthetic Customer",
                Phone = "00000000",
                Street = "Synthetic Street"
            }
        };
        var orderService = OrderServiceStub((method, _) => method.Name == "GetOrderByNumberAsync"
            ? Task.FromResult<OrderDto?>(exposed)
            : throw Unexpected(method));
        var controller = Controller(orderService);

        var response = await controller.GetOrderByNumber(exposed.OrderNumber);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var returned = Assert.IsType<OrderDto>(ok.Value);
        Assert.Equal("customer-b", returned.AppUserId);
        Assert.Equal("00000000", returned.ShippingAddress.Phone);
        Assert.Equal("Synthetic Street", returned.ShippingAddress.Street);
    }

    [Fact]
    public async Task Authenticated_user_can_cancel_an_unowned_guest_order()
    {
        var cancelCalled = false;
        var orderService = OrderServiceStub((method, _) => method.Name switch
        {
            "GetOrderByIdAsync" => Task.FromResult<OrderDto?>(new OrderDto { Id = 77, AppUserId = null }),
            "CancelOrderAsync" => Cancel(),
            _ => throw Unexpected(method)
        });
        Task<bool> Cancel() { cancelCalled = true; return Task.FromResult(true); }
        var controller = Controller(orderService, "attacker-user");

        var response = await controller.CancelOrder(77);

        Assert.IsType<NoContentResult>(response);
        Assert.True(cancelCalled);
    }

    [Fact]
    public async Task Authenticated_user_can_retrieve_an_unowned_guest_payment_link()
    {
        var orderService = OrderServiceStub((method, _) => method.Name switch
        {
            "GetOrderByIdAsync" => Task.FromResult<OrderDto?>(new OrderDto { Id = 78, AppUserId = null }),
            "GetOrderPaymentLinkAsync" => Task.FromResult("https://sadad.test/pay/private"),
            _ => throw Unexpected(method)
        });
        var controller = Controller(orderService, "attacker-user");

        var response = await controller.GetOrderPaymentLink(78);

        Assert.IsType<OkObjectResult>(response);
    }

    [Fact]
    public async Task Guest_checkout_passes_null_identity_to_order_service()
    {
        string? observedUserId = "not-called";
        var orderService = OrderServiceStub((method, args) => method.Name switch
        {
            "CreateOrderAsync" => Created(args),
            "GetOrderByIdAsync" => Task.FromResult<OrderDto?>(null),
            _ => throw Unexpected(method)
        });
        Task<TransactionAdvancedDto> Created(object?[]? args)
        {
            observedUserId = (string?)args![1];
            return Task.FromResult(new TransactionAdvancedDto { OrderId = 5 });
        }
        var controller = Controller(orderService);

        var response = await controller.CreateOrder(new OrderCreateDto());

        Assert.IsType<CreatedAtActionResult>(response.Result);
        Assert.Null(observedUserId);
    }

    [Fact]
    public async Task Controller_returns_internal_exception_text_to_anonymous_client()
    {
        const string internalDetail = "Sadad invoice creation failed (500): upstream-internal-detail";
        var orderService = OrderServiceStub((method, _) => method.Name switch
        {
            "CreateOrderAsync" => Task.FromException<TransactionAdvancedDto>(new InvalidOperationException(internalDetail)),
            _ => throw Unexpected(method)
        });
        var controller = Controller(orderService);

        var response = await controller.CreateOrder(new OrderCreateDto());

        var badRequest = Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Equal(internalDetail, badRequest.Value);
    }

    private static OrdersController Controller(IOrderService orderService, string? userId = null)
    {
        var email = InterfaceStub.Create<IEmailService>((method, _) => method.Name switch
        {
            "SendOrderNotificationToAdminAsync" => Task.CompletedTask,
            _ => throw Unexpected(method)
        });
        var transactions = InterfaceStub.Create<ITransactionService>((method, _) => throw Unexpected(method));
        var controller = new OrdersController(orderService, email, transactions, NullLogger<OrdersController>.Instance);
        var claims = userId == null ? [] : new[] { new Claim(ClaimTypes.NameIdentifier, userId) };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, userId == null ? null : "test")) }
        };
        return controller;
    }

    private static IOrderService OrderServiceStub(Func<System.Reflection.MethodInfo, object?[]?, object?> handler) =>
        InterfaceStub.Create<IOrderService>(handler);

    private static Exception Unexpected(System.Reflection.MethodInfo method) =>
        new InvalidOperationException($"Unexpected call: {method.Name}");
}
