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

        public OrdersController(IOrderService orderService, IEmailService emailService)
        {
            _orderService = orderService;
            _emailService = emailService;
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

        [Authorize]
        [HttpPost]
        public async Task<ActionResult<OrderDto>> CreateOrder([FromBody] OrderCreateDto orderDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token.");
                }

                var order = await _orderService.CreateOrderAsync(orderDto, userId);
                
                // Send email notification to admin
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _emailService.SendOrderNotificationToAdminAsync(order, "Created");
                    }
                    catch
                    {
                        // Log error but don't fail the order creation
                        // Email sending failures shouldn't prevent order creation
                    }
                });
                
                return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
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
                if (order!=null)
                {
                    if (!User.GetUserId().Equals(order.AppUserId))
                    {
                        return Unauthorized();
                    }
                }
                var result = await _orderService.CancelOrderAsync(id);
                if (!result)
                {
                    return NotFound($"Order with ID {id} not found.");
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
    }
}


