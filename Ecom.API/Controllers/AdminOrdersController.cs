using Ecom.Application.DTOs.Order;
using Ecom.Application.DTOs.Common;
using Ecom.Application.Services.Interfaces;
using Ecom.Domain.constant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecom.API.Controllers
{
    [ApiController]
    [Route("api/admin/[controller]")]
    //[Authorize(Roles = "Admin")]
    public class AdminOrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;

        public AdminOrdersController(IOrderService orderService)
        {
            _orderService = orderService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResult<OrderSummaryDto>>> GetOrders(
            [FromQuery] OrderStatus? status = null,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            IEnumerable<OrderSummaryDto>? allOrders = status.HasValue
                ? await _orderService.GetOrdersByStatusAsync(status.Value)
                : await _orderService.GetRecentOrdersAsync(1000); // Get more orders for pagination

            var ordersList = allOrders.ToList();
            var totalCount = ordersList.Count;

            List<OrderSummaryDto>? pagedOrders = ordersList
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var result = new PagedResult<OrderSummaryDto>(pagedOrders, totalCount, pageNumber, pageSize);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<OrderDto>> GetOrder(int id)
        {
            var order = await _orderService.GetOrderByIdAsync(id);
            if (order == null)
            {
                return NotFound($"Order with ID {id} not found.");
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

        [HttpGet("user/{userId}")]
        public async Task<ActionResult<PagedResult<OrderSummaryDto>>> GetUserOrders(
            string userId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var allOrders = await _orderService.GetOrdersByUserAsync(userId);
            var ordersList = allOrders.ToList();
            var totalCount = ordersList.Count;

            var pagedOrders = ordersList
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var result = new PagedResult<OrderSummaryDto>(pagedOrders, totalCount, pageNumber, pageSize);
            return Ok(result);
        }

        [HttpPut("{id}/status")]
        public async Task<ActionResult<OrderDto>> UpdateOrderStatus(int id, [FromBody] OrderUpdateStatusDto orderUpdateDto)
        {
            if (id != orderUpdateDto.Id)
            {
                return BadRequest("ID mismatch.");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var order = await _orderService.UpdateOrderStatusAsync(orderUpdateDto);
                return Ok(order);
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

        [HttpPut("{id}/cancel")]
        public async Task<ActionResult> CancelOrder(int id)
        {
            try
            {
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

        [HttpGet("statistics")]
        public async Task<ActionResult> GetOrderStatistics()
        {
            Dictionary<OrderStatus, int>? statistics = await _orderService.GetOrderStatisticsAsync();
            return Ok(statistics);
        }

        [HttpGet("sales")]
        public async Task<ActionResult> GetTotalSales([FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
        {
            var totalSales = await _orderService.GetTotalSalesAsync(startDate, endDate);
            return Ok(new { TotalSales = totalSales, StartDate = startDate, EndDate = endDate });
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
        public async Task<ActionResult<PagedResult<TransactionDto>>> GetOrderTransactions(
            int id,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var allTransactions = await _orderService.GetOrderTransactionsAsync(id);
            var transactionsList = allTransactions.ToList();
            var totalCount = transactionsList.Count;

            var pagedTransactions = transactionsList
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var result = new PagedResult<TransactionDto>(pagedTransactions, totalCount, pageNumber, pageSize);
            return Ok(result);
        }
    }
}
