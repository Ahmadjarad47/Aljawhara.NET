using System.Security.Claims;
using Ecom.API.Controllers;
using Ecom.Application.DTOs.Order;
using Ecom.Application.Payments;
using Ecom.Application.Services.Interfaces;
using Ecom.Domain.constant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ecom.CheckoutAudit.Tests;

public class AuthorizationAndControllerTests
{
    public static TheoryData<string> GuestCompatibleActions => new()
    {
        nameof(OrdersController.GetOrder),
        nameof(OrdersController.GetOrderByNumber),
        nameof(OrdersController.GetOrderTransactions),
        nameof(OrdersController.GetInvoicePaymentData),
        nameof(OrdersController.GetOrderPaymentLink),
        nameof(OrdersController.CancelOrder)
    };

    [Theory]
    [MemberData(nameof(GuestCompatibleActions))]
    public void Guest_order_actions_remain_anonymous_at_transport_boundary(string actionName)
    {
        var method = typeof(OrdersController).GetMethod(actionName)!;

        Assert.NotEmpty(method.GetCustomAttributes(typeof(AllowAnonymousAttribute), true));
    }

    [Fact]
    public async Task Payment_status_checks_access_before_triggering_gateway_verification()
    {
        var orders = OrderServiceStub((method, _) => method.Name == "CanAccessOrderAsync"
            ? Task.FromResult(false) : throw Unexpected(method));
        var controller = Controller(orders);

        var response = await controller.GetPaymentStatus(91);

        Assert.IsType<NotFoundResult>(response.Result);
        Assert.Equal("no-store", controller.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task Authorized_payment_status_verifies_then_returns_the_updated_protected_status()
    {
        var verified = false;
        var orders = OrderServiceStub((method, _) => method.Name switch
        {
            "CanAccessOrderAsync" => Task.FromResult(true),
            "GetOrderPaymentStatusAsync" when verified => Task.FromResult(new PaymentStatusDto
            {
                OrderId = 91, InitializationStatus = PaymentInitializationStatus.Paid
            }),
            _ => throw Unexpected(method)
        });
        var verifier = InterfaceStub.Create<ISadadPaymentVerificationService>((method, args) =>
        {
            Assert.Equal("VerifyByOrderIdAsync", method.Name);
            Assert.Equal(91, args![0]);
            verified = true;
            return Task.FromResult(new PaymentVerificationResult(PaymentVerificationOutcome.Paid, "STRICT_GETBYID_VERIFIED"));
        });
        var controller = new OrdersController(orders, null!, verifier, NullLogger<OrdersController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var response = await controller.GetPaymentStatus(91);

        var body = Assert.IsType<PaymentStatusDto>(Assert.IsType<OkObjectResult>(response.Result).Value);
        Assert.Equal(PaymentInitializationStatus.Paid, body.InitializationStatus);
        Assert.Equal("no-store", controller.Response.Headers.CacheControl);
    }

    [Fact]
    public void Application_has_no_global_fallback_authorization_policy()
    {
        var source = File.ReadAllText(Path.Combine(Workspace.Root(), "Ecom.API", "Program.cs"));

        Assert.DoesNotContain("FallbackPolicy", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Anonymous_order_number_lookup_without_capability_returns_not_found()
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
        var orderService = OrderServiceStub((method, _) => method.Name switch
        {
            "GetOrderByNumberAsync" => Task.FromResult<OrderDto?>(exposed),
            "CanAccessOrderAsync" => Task.FromResult(false),
            _ => throw Unexpected(method)
        });
        var controller = Controller(orderService);

        var response = await controller.GetOrderByNumber(exposed.OrderNumber);

        Assert.IsType<NotFoundResult>(response.Result);
    }

    [Fact]
    public async Task Authenticated_user_cannot_cancel_an_unowned_guest_order()
    {
        var cancelCalled = false;
        var orderService = OrderServiceStub((method, _) => method.Name switch
        {
            "GetOrderByIdAsync" => Task.FromResult<OrderDto?>(new OrderDto { Id = 77, AppUserId = null }),
            "CanAccessOrderAsync" => Task.FromResult(false),
            "CancelOrderAsync" => Cancel(),
            _ => throw Unexpected(method)
        });
        Task<bool> Cancel() { cancelCalled = true; return Task.FromResult(true); }
        var controller = Controller(orderService, "attacker-user");

        var response = await controller.CancelOrder(77);

        Assert.IsType<NotFoundResult>(response);
        Assert.False(cancelCalled);
    }

    [Fact]
    public async Task Authenticated_user_cannot_retrieve_an_unowned_guest_payment_link()
    {
        var orderService = OrderServiceStub((method, _) => method.Name switch
        {
            "GetOrderByIdAsync" => Task.FromResult<OrderDto?>(new OrderDto { Id = 78, AppUserId = null }),
            "CanAccessOrderAsync" => Task.FromResult(false),
            "GetOrderPaymentLinkAsync" => Task.FromResult("https://sadad.test/pay/private"),
            _ => throw Unexpected(method)
        });
        var controller = Controller(orderService, "attacker-user");

        var response = await controller.GetOrderPaymentLink(78);

        Assert.IsType<NotFoundResult>(response);
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
    public async Task Controller_returns_generic_problem_without_internal_exception_text()
    {
        const string internalDetail = "Sadad invoice creation failed (500): upstream-internal-detail";
        var orderService = OrderServiceStub((method, _) => method.Name switch
        {
            "CreateOrderAsync" => Task.FromException<TransactionAdvancedDto>(new InvalidOperationException(internalDetail)),
            _ => throw Unexpected(method)
        });
        var controller = Controller(orderService);

        var response = await controller.CreateOrder(new OrderCreateDto());

        var problem = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status502BadGateway, problem.StatusCode);
        Assert.DoesNotContain(internalDetail, problem.Value!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Legacy_payment_endpoint_is_hard_disabled()
    {
        var controller = Controller(OrderServiceStub((method, _) => throw Unexpected(method)));

        var response = controller.ProcessPayment(1, new TransactionCreateDto
        {
            OrderId = 1,
            Amount = 1,
            Status = TransactionStatus.Paid
        });

        var disabled = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status410Gone, disabled.StatusCode);
    }

    [Fact]
    public async Task Guest_capability_allows_only_the_scoped_order()
    {
        var order = new OrderDto { Id = 91, OrderNumber = "ORD-91" };
        var orderService = OrderServiceStub((method, args) => method.Name switch
        {
            "GetOrderByIdAsync" => Task.FromResult<OrderDto?>(order),
            "CanAccessOrderAsync" => Task.FromResult((int)args![0]! == 91 && (string?)args[2] == "valid-order-secret"),
            _ => throw Unexpected(method)
        });
        var controller = Controller(orderService, guestToken: "valid-order-secret");

        var response = await controller.GetOrder(91);

        Assert.IsType<OkObjectResult>(response.Result);
    }

    private static OrdersController Controller(IOrderService orderService, string? userId = null, string? guestToken = null)
    {
        var email = InterfaceStub.Create<IEmailService>((method, _) => method.Name switch
        {
            "SendOrderNotificationToAdminAsync" => Task.CompletedTask,
            _ => throw Unexpected(method)
        });
        var verification = InterfaceStub.Create<ISadadPaymentVerificationService>((method, _) => throw Unexpected(method));
        var controller = new OrdersController(orderService, email, verification, NullLogger<OrdersController>.Instance);
        var claims = userId == null ? [] : new[] { new Claim(ClaimTypes.NameIdentifier, userId) };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, userId == null ? null : "test")) }
        };
        if (guestToken != null)
            controller.Request.Headers["X-Guest-Order-Token"] = guestToken;
        return controller;
    }

    private static IOrderService OrderServiceStub(Func<System.Reflection.MethodInfo, object?[]?, object?> handler) =>
        InterfaceStub.Create<IOrderService>(handler);

    private static Exception Unexpected(System.Reflection.MethodInfo method) =>
        new InvalidOperationException($"Unexpected call: {method.Name}");
}
