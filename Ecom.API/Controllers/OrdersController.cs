using Ecom.API.Controllers.Extensions;
using Ecom.Application.DTOs.Order;
using Ecom.Application.Services.Interfaces;
using Ecom.Domain.constant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Ecom.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly IEmailService _emailService;
        private readonly ITransactionService _transactionService;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(IOrderService orderService, IEmailService emailService, ITransactionService transactionService, ILogger<OrdersController> logger)
        {
            _orderService = orderService;
            _emailService = emailService;
            _transactionService = transactionService;
            _logger = logger;
        }


        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<OrderDto>> GetOrder(int id)
        {
            var order = await _orderService.GetOrderByIdAsync(id);
            if (order == null)
            {
                return NotFound($"Order with ID {id} not found.");
            }
            
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId) && userId != order.AppUserId)
            {
                return Unauthorized();
            }
            
            return Ok(order);
        }

        [HttpGet("number/{orderNumber}")]
        public async Task<ActionResult<OrderDto>> GetOrderByNumber(string orderNumber)
        {
            var order = await _orderService.GetOrderByNumberAsync(orderNumber);
            if (order == null)
            {
                return NotFound($"Order with number {orderNumber} not found.");
            }
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
        public async Task<ActionResult<TransactionAdvancedDto>> CreateOrder([FromBody] OrderCreateDto orderDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var transaction = await _orderService.CreateOrderAsync(orderDto, userId);

                // Send email notification to admin
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var order = await _orderService.GetOrderByIdAsync(transaction.OrderId);
                        if (order != null)
                            await _emailService.SendOrderNotificationToAdminAsync(order, transaction.CustomerName ?? order.CustomerName, "Created");
                    }
                    catch
                    {
                        // Log error but don't fail the order creation
                        // Email sending failures shouldn't prevent order creation
                    }
                });

                return CreatedAtAction(nameof(GetOrder), new { id = transaction.OrderId }, transaction);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }


        [HttpPut("{id}/cancel")]
        [Authorize]
        public async Task<ActionResult> CancelOrder(int id)
        {
            try
            {
                var order = await _orderService.GetOrderByIdAsync(id);
                if (order == null)
                {
                    return NotFound($"Order with ID {id} not found.");
                }

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrWhiteSpace(order.AppUserId) && !string.Equals(order.AppUserId, userId, StringComparison.Ordinal))
                {
                    return Unauthorized();
                }
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
                return BadRequest(ex.Message);
            }
        }


        [HttpPost("{id}/payment")]
        public async Task<ActionResult<TransactionDto>> ProcessPayment(int id, [FromBody] TransactionCreateDto transactionDto)
        {
            if (id != transactionDto.OrderId)
            {
                return BadRequest("Order ID mismatch.");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var transaction = await _orderService.ProcessPaymentAsync(transactionDto);
                return Ok(transaction);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("{id}/transactions")]
        public async Task<ActionResult<IEnumerable<TransactionDto>>> GetOrderTransactions(int id)
        {
            var transactions = await _orderService.GetOrderTransactionsAsync(id);
            return Ok(transactions);
        }

        [HttpGet("{id}/invoice-payment")]
        public async Task<ActionResult<InvoicePaymentDto>> GetInvoicePaymentData(int id)
        {
            var invoicePaymentData = await _orderService.GetInvoicePaymentDataAsync(id);
            if (invoicePaymentData == null)
            {
                return NotFound(new InvoicePaymentDto 
                { 
                    Success = false, 
                    Message = $"الطلب بالرقم {id} غير موجود."
                });
            }
            
            return Ok(invoicePaymentData);
        }

        [HttpPost("{id}/payment-link")]
        [Authorize]
        public async Task<ActionResult> GetOrderPaymentLink(int id)
        {
            try
            {
                var order = await _orderService.GetOrderByIdAsync(id);
                if (order == null)
                {
                    return NotFound($"Order with ID {id} not found.");
                }

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrWhiteSpace(order.AppUserId) && !string.Equals(order.AppUserId, userId, StringComparison.Ordinal))
                {
                    return Unauthorized();
                }

                var paymentLink = await _orderService.GetOrderPaymentLinkAsync(id);
                return Ok(new
                {
                    OrderId = id,
                    PaymentLink = paymentLink
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
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("webhook/sadad-paid")]
        [AllowAnonymous]
        public async Task<IActionResult> SadadPaidWebhook()
        {
            _logger.LogInformation("[Sadad Webhook] Step 1: Received webhook request.");

            string rawJson;
            using (var reader = new StreamReader(Request.Body))
            {
                rawJson = await reader.ReadToEndAsync();
            }

            if (string.IsNullOrWhiteSpace(rawJson))
            {
                _logger.LogWarning("[Sadad Webhook] Step 1 failed: Empty body.");
                return BadRequest("Invalid webhook: empty body.");
            }
            _logger.LogDebug("[Sadad Webhook] Step 1 OK: Raw body length {Length} chars.", rawJson.Length);

            SadadWebhookDto? payload;
            try
            {
                payload = System.Text.Json.JsonSerializer.Deserialize<SadadWebhookDto>(rawJson, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
                });
            }
            catch (System.Text.Json.JsonException ex)
            {
                _logger.LogWarning(ex, "[Sadad Webhook] Step 2 failed: Malformed JSON.");
                return BadRequest("Invalid webhook: malformed JSON.");
            }

            if (payload == null || payload.InvoiceId <= 0
)
            {
                _logger.LogWarning("[Sadad Webhook] Step 2 failed: Missing invoiceId. Payload: {Payload}", payload == null ? "null" : "empty invoiceId");
                return BadRequest("Invalid webhook payload: missing invoiceId.");
            }
            _logger.LogInformation("[Sadad Webhook] Step 2 OK: Parsed payload. InvoiceId={InvoiceId}, Status={Status}", payload.InvoiceId, payload.Status);

            _logger.LogInformation("[Sadad Webhook] Step 3: Calling HandleSadadPaidWebhookAsync for InvoiceId={InvoiceId}.", payload.InvoiceId);
            var success = await _transactionService.HandleSadadPaidWebhookAsync(payload);

            if (success)
            {
                _logger.LogInformation("[Sadad Webhook] Step 3 OK: Webhook processed successfully for InvoiceId={InvoiceId}.", payload.InvoiceId);
                return Ok();
            }

            _logger.LogWarning("[Sadad Webhook] Step 3 failed: HandleSadadPaidWebhookAsync returned false for InvoiceId={InvoiceId}.", payload.InvoiceId);
            return BadRequest("Webhook processing failed.");
        }

        [HttpGet("payment-callback")]
        [Authorize]
        public async Task<IActionResult> PaymentCallback(
            [FromQuery(Name = "invoice_id")] long invoiceId,
            [FromQuery(Name = "payment")] string? payment)
        {
            if (invoiceId <= 0)
            {
                return BadRequest(new
                {
                    InvoiceId = invoiceId,
                    Payment = payment,
                    Updated = false,
                    Message = "Invalid invoice_id."
                });
            }

            if (!string.Equals(payment, "success", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    InvoiceId = invoiceId,
                    Payment = payment,
                    Updated = false,
                    Message = "Payment is not marked as success."
                });
            }

            var payload = new SadadWebhookDto
            {
                InvoiceId = invoiceId,
                Status = "Paid"
            };

            var updated = await _transactionService.HandleSadadPaidWebhookAsync(payload);
            if (!updated)
            {
                return BadRequest(new
                {
                    InvoiceId = invoiceId,
                    Payment = payment,
                    Updated = false,
                    Message = "Payment verification or status update failed."
                });
            }

            return Ok(new
            {
                InvoiceId = invoiceId,
                Payment = payment,
                Updated = true,
                Message = "Payment verified and order status updated."
            });
        }

    }
}


