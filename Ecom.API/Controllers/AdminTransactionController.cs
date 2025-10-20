using Ecom.Application.DTOs.Common;
using Ecom.Application.DTOs.Order;
using Ecom.Application.Services.Interfaces;
using Ecom.Domain.constant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Ecom.API.Controllers
{
    [ApiController]
    [Route("api/admin/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AdminTransactionController : ControllerBase
    {
        private readonly ITransactionService _transactionService;

        public AdminTransactionController(ITransactionService transactionService)
        {
            _transactionService = transactionService;
        }

        /// <summary>
        /// Get all transactions with advanced filtering and pagination
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<PagedResult<TransactionAdvancedDto>>> GetTransactions([FromQuery] TransactionFilterDto filter)
        {
            try
            {
                var result = await _transactionService.GetTransactionsAsync(filter);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error retrieving transactions: {ex.Message}");
            }
        }

        /// <summary>
        /// Get transaction by ID with full details
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<TransactionAdvancedDto>> GetTransaction(int id)
        {
            try
            {
                var transaction = await _transactionService.GetTransactionByIdAsync(id);
                if (transaction == null)
                {
                    return NotFound($"Transaction with ID {id} not found.");
                }
                return Ok(transaction);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error retrieving transaction: {ex.Message}");
            }
        }

        /// <summary>
        /// Get transaction by reference number
        /// </summary>
        [HttpGet("reference/{reference}")]
        public async Task<ActionResult<TransactionAdvancedDto>> GetTransactionByReference(string reference)
        {
            try
            {
                var transaction = await _transactionService.GetTransactionByReferenceAsync(reference);
                if (transaction == null)
                {
                    return NotFound($"Transaction with reference {reference} not found.");
                }
                return Ok(transaction);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error retrieving transaction: {ex.Message}");
            }
        }

        /// <summary>
        /// Get transactions for a specific order
        /// </summary>
        [HttpGet("order/{orderId}")]
        public async Task<ActionResult<PagedResult<TransactionAdvancedDto>>> GetTransactionsByOrder(
            int orderId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var result = await _transactionService.GetTransactionsByOrderAsync(orderId, pageNumber, pageSize);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error retrieving order transactions: {ex.Message}");
            }
        }

        /// <summary>
        /// Get latest transaction for an order
        /// </summary>
        [HttpGet("order/{orderId}/latest")]
        public async Task<ActionResult<TransactionAdvancedDto>> GetLatestTransactionByOrder(int orderId)
        {
            try
            {
                var transaction = await _transactionService.GetLatestTransactionByOrderAsync(orderId);
                if (transaction == null)
                {
                    return NotFound($"No transactions found for order {orderId}.");
                }
                return Ok(transaction);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error retrieving latest transaction: {ex.Message}");
            }
        }

        /// <summary>
        /// Create a new transaction
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<TransactionAdvancedDto>> CreateTransaction([FromBody] TransactionCreateAdvancedDto transactionDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var transaction = await _transactionService.CreateTransactionAsync(transactionDto);
                return CreatedAtAction(nameof(GetTransaction), new { id = transaction.Id }, transaction);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error creating transaction: {ex.Message}");
            }
        }

        /// <summary>
        /// Update an existing transaction
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<TransactionAdvancedDto>> UpdateTransaction(int id, [FromBody] TransactionUpdateDto transactionDto)
        {
            try
            {
                if (id != transactionDto.Id)
                {
                    return BadRequest("ID mismatch.");
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var transaction = await _transactionService.UpdateTransactionAsync(transactionDto);
                return Ok(transaction);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error updating transaction: {ex.Message}");
            }
        }

        /// <summary>
        /// Delete a transaction
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteTransaction(int id)
        {
            try
            {
                var result = await _transactionService.DeleteTransactionAsync(id);
                if (!result)
                {
                    return NotFound($"Transaction with ID {id} not found.");
                }
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest($"Error deleting transaction: {ex.Message}");
            }
        }

        /// <summary>
        /// Process payment for an order
        /// </summary>
        [HttpPost("process-payment")]
        public async Task<ActionResult<TransactionAdvancedDto>> ProcessPayment([FromBody] PaymentProcessingDto paymentDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var transaction = await _transactionService.ProcessPaymentAsync(paymentDto);
                return Ok(transaction);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error processing payment: {ex.Message}");
            }
        }

        /// <summary>
        /// Process refund for a transaction
        /// </summary>
        [HttpPost("refund")]
        public async Task<ActionResult<TransactionAdvancedDto>> ProcessRefund([FromBody] TransactionRefundDto refundDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var transaction = await _transactionService.ProcessRefundAsync(refundDto);
                return Ok(transaction);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error processing refund: {ex.Message}");
            }
        }

        /// <summary>
        /// Update transaction status
        /// </summary>
        [HttpPut("{id}/status")]
        public async Task<ActionResult> UpdateTransactionStatus(
            int id,
            [FromBody] UpdateTransactionStatusDto statusDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _transactionService.UpdateTransactionStatusAsync(id, statusDto.Status, statusDto.GatewayResponse);
                if (!result)
                {
                    return NotFound($"Transaction with ID {id} not found.");
                }

                return Ok(new { Message = "Transaction status updated successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest($"Error updating transaction status: {ex.Message}");
            }
        }

        /// <summary>
        /// Mark transaction as completed
        /// </summary>
        [HttpPut("{id}/complete")]
        public async Task<ActionResult> MarkTransactionAsCompleted(int id, [FromBody] CompleteTransactionDto completeDto)
        {
            try
            {
                var result = await _transactionService.MarkTransactionAsCompletedAsync(id, completeDto.Reference);
                if (!result)
                {
                    return NotFound($"Transaction with ID {id} not found.");
                }

                return Ok(new { Message = "Transaction marked as completed." });
            }
            catch (Exception ex)
            {
                return BadRequest($"Error completing transaction: {ex.Message}");
            }
        }

        /// <summary>
        /// Mark transaction as failed
        /// </summary>
        [HttpPut("{id}/fail")]
        public async Task<ActionResult> MarkTransactionAsFailed(int id, [FromBody] FailTransactionDto failDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _transactionService.MarkTransactionAsFailedAsync(id, failDto.Reason);
                if (!result)
                {
                    return NotFound($"Transaction with ID {id} not found.");
                }

                return Ok(new { Message = "Transaction marked as failed." });
            }
            catch (Exception ex)
            {
                return BadRequest($"Error failing transaction: {ex.Message}");
            }
        }

        /// <summary>
        /// Mark transaction as pending
        /// </summary>
        [HttpPut("{id}/pending")]
        public async Task<ActionResult> MarkTransactionAsPending(int id)
        {
            try
            {
                var result = await _transactionService.MarkTransactionAsPendingAsync(id);
                if (!result)
                {
                    return NotFound($"Transaction with ID {id} not found.");
                }

                return Ok(new { Message = "Transaction marked as pending." });
            }
            catch (Exception ex)
            {
                return BadRequest($"Error updating transaction: {ex.Message}");
            }
        }

        /// <summary>
        /// Get transaction analytics
        /// </summary>
        [HttpGet("analytics")]
        public async Task<ActionResult<TransactionAnalyticsDto>> GetTransactionAnalytics(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var analytics = await _transactionService.GetTransactionAnalyticsAsync(startDate, endDate);
                return Ok(analytics);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error retrieving analytics: {ex.Message}");
            }
        }

        /// <summary>
        /// Get transaction analytics for a specific order
        /// </summary>
        [HttpGet("analytics/order/{orderId}")]
        public async Task<ActionResult<TransactionAnalyticsDto>> GetOrderTransactionAnalytics(int orderId)
        {
            try
            {
                var analytics = await _transactionService.GetTransactionAnalyticsByOrderAsync(orderId);
                return Ok(analytics);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error retrieving order analytics: {ex.Message}");
            }
        }

        /// <summary>
        /// Get revenue by payment method
        /// </summary>
        [HttpGet("revenue/payment-methods")]
        public async Task<ActionResult<Dictionary<string, decimal>>> GetRevenueByPaymentMethod(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var revenue = await _transactionService.GetRevenueByPaymentMethodAsync(startDate, endDate);
                var result = revenue.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error retrieving revenue data: {ex.Message}");
            }
        }

        /// <summary>
        /// Get transaction trends
        /// </summary>
        [HttpGet("trends")]
        public async Task<ActionResult<List<TransactionTrendDto>>> GetTransactionTrends(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate,
            [FromQuery] string period = "daily")
        {
            try
            {
                var trends = await _transactionService.GetTransactionTrendsAsync(startDate, endDate, period);
                return Ok(trends);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error retrieving trends: {ex.Message}");
            }
        }

        /// <summary>
        /// Get refunded transactions
        /// </summary>
        [HttpGet("refunded")]
        public async Task<ActionResult<List<TransactionAdvancedDto>>> GetRefundedTransactions(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var transactions = await _transactionService.GetRefundedTransactionsAsync(startDate, endDate);
                return Ok(transactions);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error retrieving refunded transactions: {ex.Message}");
            }
        }

        /// <summary>
        /// Get total refunded amount
        /// </summary>
        [HttpGet("refunded/total")]
        public async Task<ActionResult<decimal>> GetTotalRefundedAmount(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var total = await _transactionService.GetTotalRefundedAmountAsync(startDate, endDate);
                return Ok(new { TotalRefundedAmount = total });
            }
            catch (Exception ex)
            {
                return BadRequest($"Error retrieving refunded amount: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if transaction can be refunded
        /// </summary>
        [HttpGet("{id}/can-refund")]
        public async Task<ActionResult<bool>> CanRefundTransaction(int id)
        {
            try
            {
                var canRefund = await _transactionService.CanRefundTransactionAsync(id);
                return Ok(new { CanRefund = canRefund });
            }
            catch (Exception ex)
            {
                return BadRequest($"Error checking refund eligibility: {ex.Message}");
            }
        }

        /// <summary>
        /// Search transactions
        /// </summary>
        [HttpGet("search")]
        public async Task<ActionResult<List<TransactionSummaryDto>>> SearchTransactions(
            [FromQuery] string searchTerm,
            [FromQuery] int limit = 50)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchTerm))
                {
                    return BadRequest("Search term is required.");
                }

                var transactions = await _transactionService.SearchTransactionsAsync(searchTerm, limit);
                return Ok(transactions);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error searching transactions: {ex.Message}");
            }
        }

        /// <summary>
        /// Get transactions by status
        /// </summary>
        [HttpGet("status/{status}")]
        public async Task<ActionResult<List<TransactionAdvancedDto>>> GetTransactionsByStatus(
            string status,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var transactions = await _transactionService.GetTransactionsByStatusAsync(status, pageNumber, pageSize);
                return Ok(transactions);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error retrieving transactions by status: {ex.Message}");
            }
        }

        /// <summary>
        /// Get transactions by payment method
        /// </summary>
        [HttpGet("payment-method/{paymentMethod}")]
        public async Task<ActionResult<List<TransactionAdvancedDto>>> GetTransactionsByPaymentMethod(
            PaymentMethod paymentMethod,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var transactions = await _transactionService.GetTransactionsByPaymentMethodAsync(paymentMethod, pageNumber, pageSize);
                return Ok(transactions);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error retrieving transactions by payment method: {ex.Message}");
            }
        }

        /// <summary>
        /// Export transactions to CSV
        /// </summary>
        [HttpGet("export/csv")]
        public async Task<ActionResult> ExportTransactionsToCsv([FromQuery] TransactionFilterDto filter)
        {
            try
            {
                var csvData = await _transactionService.ExportTransactionsToCsvAsync(filter);
                var fileName = $"transactions_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
                
                return File(csvData, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error exporting transactions: {ex.Message}");
            }
        }

        /// <summary>
        /// Export transactions to Excel
        /// </summary>
        [HttpGet("export/excel")]
        public async Task<ActionResult> ExportTransactionsToExcel([FromQuery] TransactionFilterDto filter)
        {
            try
            {
                var excelData = await _transactionService.ExportTransactionsToExcelAsync(filter);
                var fileName = $"transactions_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
                
                return File(excelData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error exporting transactions: {ex.Message}");
            }
        }
    }

    // Additional DTOs for specific operations
    public class UpdateTransactionStatusDto
    {
        [Required]
        public string Status { get; set; } = string.Empty;
        public string? GatewayResponse { get; set; }
    }

    public class CompleteTransactionDto
    {
        public string? Reference { get; set; }
    }

    public class FailTransactionDto
    {
        [Required]
        public string Reason { get; set; } = string.Empty;
    }
}
