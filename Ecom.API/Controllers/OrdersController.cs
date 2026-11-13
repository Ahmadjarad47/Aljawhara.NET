using Ecom.API.Controllers.Extensions;
using Ecom.Application.DTOs.Order;
using Ecom.Application.Services.Interfaces;
using Ecom.Domain.constant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Ecom.Application.Payments;

namespace Ecom.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private const string GuestAccessHeader = "X-Guest-Order-Token";
        private readonly IOrderService _orderService;
        private readonly IEmailService _emailService;
        private readonly ISadadPaymentVerificationService _paymentVerification;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(IOrderService orderService, IEmailService emailService, ISadadPaymentVerificationService paymentVerification, ILogger<OrdersController> logger)
        {
            _orderService = orderService;
            _emailService = emailService;
            _paymentVerification = paymentVerification;
            _logger = logger;
        }


        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<OrderDto>> GetOrder(int id)
        {
            var order = await _orderService.GetOrderByIdAsync(id);
            if (order == null)
            {
                return NotFound();
            }

            if (!await HasOrderAccessAsync(id))
                return NotFound();
            
            return Ok(order);
        }

        [HttpGet("number/{orderNumber}")]
        [AllowAnonymous]
        public async Task<ActionResult<OrderDto>> GetOrderByNumber(string orderNumber)
        {
            var order = await _orderService.GetOrderByNumberAsync(orderNumber);
            if (order == null)
            {
                return NotFound();
            }
            if (!await HasOrderAccessAsync(order.Id))
                return NotFound();
            return Ok(order);
        }


        [HttpGet("my-orders")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<OrderSummaryDto>>> GetMyOrders()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var orders = await _orderService.GetOrdersByUserAsync(userId);
            return Ok(orders);
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<ActionResult<TransactionAdvancedDto>> CreateOrder([FromBody] OrderCreateDto orderDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault();
                var transaction = await _orderService.CreateOrderAsync(orderDto, userId, idempotencyKey);

                return CreatedAtAction(nameof(GetOrder), new { id = transaction.OrderId }, transaction);
            }
            catch (IdempotencyConflictException ex)
            {
                return Conflict(new ProblemDetails { Status = StatusCodes.Status409Conflict, Title = "Idempotency conflict", Detail = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Checkout failed. TraceId={TraceId}", HttpContext.TraceIdentifier);
                return Problem(
                    statusCode: StatusCodes.Status502BadGateway,
                    title: "Checkout could not be completed.",
                    detail: "The payment service is temporarily unavailable.");
            }
        }

        [HttpGet("{id}/payment-status")]
        [AllowAnonymous]
        public async Task<ActionResult<PaymentStatusDto>> GetPaymentStatus(int id, [FromQuery] bool verify = false)
        {
            Response.Headers.CacheControl = "no-store";
            if (!await HasOrderAccessAsync(id))
                return NotFound();
            // Preparing the link only reads persisted state. Explicit verification is
            // requested after the browser returns from Sadad, never before redirecting.
            if (verify)
            {
                try
                {
                    await _paymentVerification.VerifyByOrderIdAsync(id, HttpContext.RequestAborted);
                }
                catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Payment status verification unavailable for OrderId={OrderId}. ReasonCode={ReasonCode}",
                        id, "VERIFICATION_UNAVAILABLE");
                }
            }
            return Ok(await _orderService.GetOrderPaymentStatusAsync(id));
        }

        [HttpPost("{id}/payment-attempts")]
        [AllowAnonymous]
        public async Task<ActionResult<PaymentStatusDto>> RetryPayment(int id)
        {
            if (!await HasOrderAccessAsync(id))
                return NotFound();
            try
            {
                var result = await _orderService.RetryPaymentAsync(id, Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty);
                return AcceptedAtAction(nameof(GetPaymentStatus), new { id }, result);
            }
            catch (IdempotencyConflictException ex)
            {
                return Conflict(new ProblemDetails { Status = StatusCodes.Status409Conflict, Title = "Idempotency conflict", Detail = ex.Message });
            }
            catch (ArgumentException ex) { return BadRequest(ex.Message); }
            catch (InvalidOperationException ex) { return Conflict(ex.Message); }
        }

        [HttpPut("{id}/cancel")]
        [AllowAnonymous]
        public async Task<ActionResult> CancelOrder(int id)
        {
            try
            {
                var order = await _orderService.GetOrderByIdAsync(id);
                if (order == null)
                {
                    return NotFound();
                }
                if (!await HasOrderAccessAsync(id))
                    return NotFound();
                var result = await _orderService.CancelOrderAsync(id);
                if (!result)
                {
                    return BadRequest($"Unable to cancel order with ID {id}.");
                }
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Order cancellation failed for OrderId={OrderId}. TraceId={TraceId}", id, HttpContext.TraceIdentifier);
                return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Order cancellation failed.");
            }
        }


        [HttpPost("{id}/payment")]
        [AllowAnonymous]
        public ActionResult<TransactionDto> ProcessPayment(int id, [FromBody] TransactionCreateDto transactionDto)
        {
            return StatusCode(StatusCodes.Status410Gone,
                new ProblemDetails { Status = StatusCodes.Status410Gone, Title = "Legacy payment endpoint is disabled." });
        }

        [HttpGet("{id}/transactions")]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<TransactionDto>>> GetOrderTransactions(int id)
        {
            if (!await HasOrderAccessAsync(id))
                return NotFound();
            var transactions = await _orderService.GetOrderTransactionsAsync(id);
            return Ok(transactions);
        }

        [HttpGet("{id}/invoice-payment")]
        [AllowAnonymous]
        public async Task<ActionResult<InvoicePaymentDto>> GetInvoicePaymentData(int id)
        {
            if (!await HasOrderAccessAsync(id))
                return NotFound();
            var invoicePaymentData = await _orderService.GetInvoicePaymentDataAsync(id);
            if (invoicePaymentData == null)
            {
                return NotFound(new InvoicePaymentDto 
                { 
                    Success = false, 
                    Message = "الطلب غير موجود."
                });
            }
            
            return Ok(invoicePaymentData);
        }

        [HttpPost("{id}/payment-link")]
        [AllowAnonymous]
        public async Task<ActionResult> GetOrderPaymentLink(int id)
        {
            try
            {
                var order = await _orderService.GetOrderByIdAsync(id);
                if (order == null)
                {
                    return NotFound();
                }
                if (!await HasOrderAccessAsync(id))
                    return NotFound();

                var paymentLink = await _orderService.GetOrderPaymentLinkAsync(id);
                var rotatedGuestToken = string.IsNullOrWhiteSpace(order.AppUserId)
                    ? await _orderService.RotateGuestAccessTokenAsync(id)
                    : null;
                return Ok(new
                {
                    OrderId = id,
                    PaymentLink = paymentLink,
                    GuestAccessToken = rotatedGuestToken
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Payment-link recovery failed for OrderId={OrderId}. TraceId={TraceId}", id, HttpContext.TraceIdentifier);
                return Problem(statusCode: StatusCodes.Status502BadGateway, title: "Payment link could not be generated.");
            }
        }

        [HttpPost("webhook/sadad-paid")]
        [AllowAnonymous]
        [RequestSizeLimit(16 * 1024)]
        public async Task<IActionResult> SadadPaidWebhook()
        {
            // The reviewed Sadad contract does not specify a webhook signature. The payload is a hint only.
            SadadWebhookDto? payload;
            try
            {
                payload = await System.Text.Json.JsonSerializer.DeserializeAsync<SadadWebhookDto>(Request.Body,
                    WebhookJsonOptions, HttpContext.RequestAborted);
            }
            catch (System.Text.Json.JsonException)
            {
                return BadRequest();
            }
            if (payload == null || payload.InvoiceId <= 0)
                return BadRequest();

            return await AcknowledgePaymentTriggerAsync(payload.InvoiceId);
        }

        private static readonly System.Text.Json.JsonSerializerOptions WebhookJsonOptions = new(System.Text.Json.JsonSerializerDefaults.Web)
        {
            AllowTrailingCommas = true
        };

        [HttpGet("payment-callback")]
        [AllowAnonymous]
        public Task<IActionResult> PaymentCallback([FromQuery(Name = "invoice_id")] long invoiceId) =>
            invoiceId <= 0 ? Task.FromResult<IActionResult>(BadRequest()) : AcknowledgePaymentTriggerAsync(invoiceId);

        private async Task<IActionResult> AcknowledgePaymentTriggerAsync(long invoiceId)
        {
            Response.Headers.CacheControl = "no-store";
            try
            {
                await _paymentVerification.RequestPublicVerificationAsync(
                    invoiceId.ToString(System.Globalization.CultureInfo.InvariantCulture), HttpContext.RequestAborted);
                // Sadad requires HTTP 200 for delivery acknowledgment. The storefront must
                // query the existing owner/guest-authorized order status for actual results.
                return Ok();
            }
            catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not queue a Sadad verification trigger.");
                return StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
        }

        private Task<bool> HasOrderAccessAsync(int orderId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var guestToken = Request.Headers[GuestAccessHeader].FirstOrDefault();
            return _orderService.CanAccessOrderAsync(orderId, userId, guestToken, User.IsInRole("Admin"));
        }

    }
}


