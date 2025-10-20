using AutoMapper;
using Ecom.Application.DTOs.Common;
using Ecom.Application.DTOs.Order;
using Ecom.Application.Services.Interfaces;
using Ecom.Domain.Entity;
using Ecom.Domain.constant;
using Ecom.Infrastructure.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace Ecom.Application.Services
{
    public class TransactionService : ITransactionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IOrderService _orderService;

        public TransactionService(IUnitOfWork unitOfWork, IMapper mapper, IOrderService orderService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _orderService = orderService;
        }

        public async Task<TransactionAdvancedDto?> GetTransactionByIdAsync(int id)
        {
            var transaction = await _unitOfWork.Transactions.GetTransactionWithDetailsAsync(id);
            return transaction != null ? _mapper.Map<TransactionAdvancedDto>(transaction) : null;
        }

        public async Task<TransactionAdvancedDto?> GetTransactionByReferenceAsync(string reference)
        {
            var transaction = await _unitOfWork.Transactions.GetTransactionByReferenceAsync(reference);
            return transaction != null ? _mapper.Map<TransactionAdvancedDto>(transaction) : null;
        }

        public async Task<PagedResult<TransactionAdvancedDto>> GetTransactionsAsync(TransactionFilterDto filter)
        {
            var query = _unitOfWork.Transactions.GetTransactionsQuery();

            // Apply filters
            if (filter.OrderId.HasValue)
                query = query.Where(t => t.OrderId == filter.OrderId.Value);

            if (!string.IsNullOrEmpty(filter.OrderNumber))
                query = query.Where(t => t.Order.OrderNumber.Contains(filter.OrderNumber));

            if (!string.IsNullOrEmpty(filter.AppUserId))
                query = query.Where(t => t.AppUserId == filter.AppUserId);

            if (filter.PaymentMethod.HasValue)
                query = query.Where(t => t.PaymentMethod == filter.PaymentMethod.Value);

            if (!string.IsNullOrEmpty(filter.Status))
                query = query.Where(t => t.Status == filter.Status);

            if (filter.StartDate.HasValue)
                query = query.Where(t => t.TransactionDate >= filter.StartDate.Value);

            if (filter.EndDate.HasValue)
                query = query.Where(t => t.TransactionDate <= filter.EndDate.Value);

            if (filter.MinAmount.HasValue)
                query = query.Where(t => t.Amount >= filter.MinAmount.Value);

            if (filter.MaxAmount.HasValue)
                query = query.Where(t => t.Amount <= filter.MaxAmount.Value);

            if (filter.IsRefunded.HasValue)
                query = query.Where(t => filter.IsRefunded.Value ? t.IsRefunded : !t.IsRefunded);

            // Apply sorting
            query = filter.SortDirection.ToLower() == "desc"
                ? query.OrderByDescending(GetSortExpression(filter.SortBy))
                : query.OrderBy(GetSortExpression(filter.SortBy));

            var totalCount = await query.CountAsync();

            var transactions = await query
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            var transactionDtos = _mapper.Map<List<TransactionAdvancedDto>>(transactions);

            return new PagedResult<TransactionAdvancedDto>(transactionDtos, totalCount, filter.PageNumber, filter.PageSize);
        }

        public async Task<TransactionAdvancedDto> CreateTransactionAsync(TransactionCreateAdvancedDto transactionDto)
        {
            var transaction = _mapper.Map<Transaction>(transactionDto);
            transaction.TransactionDate = DateTime.UtcNow;
            transaction.TransactionReference = transactionDto.TransactionReference ?? GenerateTransactionReference();

            await _unitOfWork.Transactions.AddAsync(transaction);
            await _unitOfWork.SaveChangesAsync();

            var createdTransaction = await _unitOfWork.Transactions.GetTransactionWithDetailsAsync(transaction.Id);
            return _mapper.Map<TransactionAdvancedDto>(createdTransaction);
        }

        public async Task<TransactionAdvancedDto> UpdateTransactionAsync(TransactionUpdateDto transactionDto)
        {
            var transaction = await _unitOfWork.Transactions.GetByIdAsync(transactionDto.Id);
            if (transaction == null)
                throw new ArgumentException($"Transaction with ID {transactionDto.Id} not found.");

            transaction.Status = transactionDto.Status;
            transaction.TransactionReference = transactionDto.TransactionReference ?? transaction.TransactionReference;
            transaction.PaymentGatewayResponse = transactionDto.PaymentGatewayResponse ?? transaction.PaymentGatewayResponse;
            transaction.Notes = transactionDto.Notes ?? transaction.Notes;

            if (transactionDto.Status == "Completed")
                transaction.ProcessedDate = DateTime.UtcNow;

            _unitOfWork.Transactions.Update(transaction);
            await _unitOfWork.SaveChangesAsync();

            var updatedTransaction = await _unitOfWork.Transactions.GetTransactionWithDetailsAsync(transaction.Id);
            return _mapper.Map<TransactionAdvancedDto>(updatedTransaction);
        }

        public async Task<bool> DeleteTransactionAsync(int id)
        {
            var transaction = await _unitOfWork.Transactions.GetByIdAsync(id);
            if (transaction == null)
                return false;

            _unitOfWork.Transactions.Remove(transaction);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<PagedResult<TransactionAdvancedDto>> GetTransactionsByOrderAsync(int orderId, int pageNumber = 1, int pageSize = 20)
        {
            var transactions = await _unitOfWork.Transactions.GetTransactionsByOrderAsync(orderId);
            var totalCount = transactions.Count();

            var pagedTransactions = transactions
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var transactionDtos = _mapper.Map<List<TransactionAdvancedDto>>(pagedTransactions);
            return new PagedResult<TransactionAdvancedDto>(transactionDtos, totalCount, pageNumber, pageSize);
        }

        public async Task<TransactionAdvancedDto?> GetLatestTransactionByOrderAsync(int orderId)
        {
            var transaction = await _unitOfWork.Transactions.GetLatestTransactionByOrderAsync(orderId);
            return transaction != null ? _mapper.Map<TransactionAdvancedDto>(transaction) : null;
        }

        public async Task<bool> HasSuccessfulTransactionAsync(int orderId)
        {
            return await _unitOfWork.Transactions.HasSuccessfulTransactionAsync(orderId);
        }

        public async Task<TransactionAdvancedDto> ProcessPaymentAsync(PaymentProcessingDto paymentDto)
        {
            // Validate order exists and get amount
            var order = await _orderService.GetOrderByIdAsync(paymentDto.OrderId);
            if (order == null)
                throw new ArgumentException($"Order with ID {paymentDto.OrderId} not found.");

            // Check if order already has successful transaction
            if (await HasSuccessfulTransactionAsync(paymentDto.OrderId))
                throw new InvalidOperationException("Order already has a successful transaction.");

            // Simulate payment processing (in real app, integrate with payment gateway)
            var isSuccessful = await ProcessPaymentWithGateway(paymentDto);

            var transactionDto = new TransactionCreateAdvancedDto
            {
                OrderId = paymentDto.OrderId,
                Amount = order.Total,
                PaymentMethod = paymentDto.PaymentMethod,
                Status = isSuccessful ? "Completed" : "Failed",
                TransactionReference = GenerateTransactionReference(),
                PaymentGatewayResponse = isSuccessful ? "Payment processed successfully" : "Payment failed",
                Notes = paymentDto.Notes
            };

            var transaction = await CreateTransactionAsync(transactionDto);

            // Update order status if payment successful
            if (isSuccessful)
            {
                await _orderService.UpdateOrderStatusAsync(new OrderUpdateStatusDto
                {
                    Id = paymentDto.OrderId,
                    Status = OrderStatus.Processing
                });
            }

            return transaction;
        }

        public async Task<TransactionAdvancedDto> ProcessRefundAsync(TransactionRefundDto refundDto)
        {
            var transaction = await _unitOfWork.Transactions.GetByIdAsync(refundDto.TransactionId);
            if (transaction == null)
                throw new ArgumentException($"Transaction with ID {refundDto.TransactionId} not found.");

            if (transaction.Status != "Completed")
                throw new InvalidOperationException("Only completed transactions can be refunded.");

            if (transaction.IsRefunded)
                throw new InvalidOperationException("Transaction has already been refunded.");

            if (refundDto.RefundAmount > transaction.Amount)
                throw new ArgumentException("Refund amount cannot exceed transaction amount.");

            // Simulate refund processing
            var isRefundSuccessful = await ProcessRefundWithGateway(transaction, refundDto.RefundAmount);

            if (isRefundSuccessful)
            {
                transaction.IsRefunded = true;
                transaction.RefundAmount = refundDto.RefundAmount;
                transaction.RefundDate = DateTime.UtcNow;
                transaction.RefundReason = refundDto.RefundReason;
                transaction.Notes = $"{transaction.Notes}\nRefund: {refundDto.Notes}".Trim();

                _unitOfWork.Transactions.Update(transaction);
                await _unitOfWork.SaveChangesAsync();
            }

            var updatedTransaction = await _unitOfWork.Transactions.GetTransactionWithDetailsAsync(transaction.Id);
            return _mapper.Map<TransactionAdvancedDto>(updatedTransaction);
        }

        public async Task<bool> UpdateTransactionStatusAsync(int transactionId, string status, string? gatewayResponse = null)
        {
            var transaction = await _unitOfWork.Transactions.GetByIdAsync(transactionId);
            if (transaction == null)
                return false;

            transaction.Status = status;
            if (!string.IsNullOrEmpty(gatewayResponse))
                transaction.PaymentGatewayResponse = gatewayResponse;

            if (status == "Completed")
                transaction.ProcessedDate = DateTime.UtcNow;

            _unitOfWork.Transactions.Update(transaction);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<TransactionAnalyticsDto> GetTransactionAnalyticsAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _unitOfWork.Transactions.GetTransactionsQuery();

            if (startDate.HasValue)
                query = query.Where(t => t.TransactionDate >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(t => t.TransactionDate <= endDate.Value);

            var transactions = await query.ToListAsync();

            var analytics = new TransactionAnalyticsDto
            {
                TotalTransactions = transactions.Count,
                TotalAmount = transactions.Sum(t => t.Amount),
                AverageTransactionAmount = transactions.Any() ? transactions.Average(t => t.Amount) : 0,
                SuccessfulTransactions = transactions.Count(t => t.Status == "Completed"),
                FailedTransactions = transactions.Count(t => t.Status == "Failed"),
                PendingTransactions = transactions.Count(t => t.Status == "Pending"),
                RefundedAmount = transactions.Where(t => t.IsRefunded).Sum(t => t.RefundAmount ?? 0),
                NetAmount = transactions.Sum(t => t.Amount) - transactions.Where(t => t.IsRefunded).Sum(t => t.RefundAmount ?? 0)
            };

            // Group by status
            analytics.TransactionsByStatus = transactions
                .GroupBy(t => t.Status)
                .ToDictionary(g => g.Key, g => g.Count());

            // Group by payment method
            analytics.AmountByPaymentMethod = transactions
                .GroupBy(t => t.PaymentMethod)
                .ToDictionary(g => g.Key.ToString(), g => g.Sum(t => t.Amount));

            analytics.CountByPaymentMethod = transactions
                .GroupBy(t => t.PaymentMethod)
                .ToDictionary(g => g.Key.ToString(), g => g.Count());

            // Calculate trends
            var start = startDate ?? DateTime.UtcNow.AddDays(-30);
            var end = endDate ?? DateTime.UtcNow;
            analytics.DailyTrends = await GetTransactionTrendsAsync(start, end, "daily");
            analytics.MonthlyTrends = await GetTransactionTrendsAsync(start, end, "monthly");

            return analytics;
        }

        public async Task<TransactionAnalyticsDto> GetTransactionAnalyticsByOrderAsync(int orderId)
        {
            var transactions = await _unitOfWork.Transactions.GetTransactionsByOrderAsync(orderId);
            var transactionList = transactions.ToList();

            var analytics = new TransactionAnalyticsDto
            {
                TotalTransactions = transactionList.Count,
                TotalAmount = transactionList.Sum(t => t.Amount),
                AverageTransactionAmount = transactionList.Any() ? transactionList.Average(t => t.Amount) : 0,
                SuccessfulTransactions = transactionList.Count(t => t.Status == "Completed"),
                FailedTransactions = transactionList.Count(t => t.Status == "Failed"),
                PendingTransactions = transactionList.Count(t => t.Status == "Pending"),
                RefundedAmount = transactionList.Where(t => t.IsRefunded).Sum(t => t.RefundAmount ?? 0),
                NetAmount = transactionList.Sum(t => t.Amount) - transactionList.Where(t => t.IsRefunded).Sum(t => t.RefundAmount ?? 0)
            };

            return analytics;
        }

        public async Task<Dictionary<PaymentMethod, decimal>> GetRevenueByPaymentMethodAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _unitOfWork.Transactions.GetTransactionsQuery()
                .Where(t => t.Status == "Completed");

            if (startDate.HasValue)
                query = query.Where(t => t.TransactionDate >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(t => t.TransactionDate <= endDate.Value);

            var transactions = await query.ToListAsync();

            return transactions
                .GroupBy(t => t.PaymentMethod)
                .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));
        }

        public async Task<List<TransactionTrendDto>> GetTransactionTrendsAsync(DateTime startDate, DateTime endDate, string period = "daily")
        {
            var query = _unitOfWork.Transactions.GetTransactionsQuery()
                .Where(t => t.TransactionDate >= startDate && t.TransactionDate <= endDate);

            var transactions = await query.ToListAsync();

            if (period.ToLower() == "daily")
            {
                return transactions
                    .GroupBy(t => t.TransactionDate.Date)
                    .Select(g => new TransactionTrendDto
                    {
                        Period = g.Key.ToString("yyyy-MM-dd"),
                        TransactionCount = g.Count(),
                        TotalAmount = g.Sum(t => t.Amount),
                        AverageAmount = g.Average(t => t.Amount)
                    })
                    .OrderBy(t => t.Period)
                    .ToList();
            }
            else
            {
                return transactions
                    .GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month })
                    .Select(g => new TransactionTrendDto
                    {
                        Period = $"{g.Key.Year}-{g.Key.Month:D2}",
                        TransactionCount = g.Count(),
                        TotalAmount = g.Sum(t => t.Amount),
                        AverageAmount = g.Average(t => t.Amount)
                    })
                    .OrderBy(t => t.Period)
                    .ToList();
            }
        }

        public async Task<bool> MarkTransactionAsCompletedAsync(int transactionId, string? reference = null)
        {
            return await UpdateTransactionStatusAsync(transactionId, "Completed", reference);
        }

        public async Task<bool> MarkTransactionAsFailedAsync(int transactionId, string reason)
        {
            return await UpdateTransactionStatusAsync(transactionId, "Failed", reason);
        }

        public async Task<bool> MarkTransactionAsPendingAsync(int transactionId)
        {
            return await UpdateTransactionStatusAsync(transactionId, "Pending");
        }

        public async Task<List<TransactionAdvancedDto>> GetRefundedTransactionsAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _unitOfWork.Transactions.GetTransactionsQuery()
                .Where(t => t.IsRefunded);

            if (startDate.HasValue)
                query = query.Where(t => t.RefundDate >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(t => t.RefundDate <= endDate.Value);

            var transactions = await query.ToListAsync();
            return _mapper.Map<List<TransactionAdvancedDto>>(transactions);
        }

        public async Task<decimal> GetTotalRefundedAmountAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _unitOfWork.Transactions.GetTransactionsQuery()
                .Where(t => t.IsRefunded);

            if (startDate.HasValue)
                query = query.Where(t => t.RefundDate >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(t => t.RefundDate <= endDate.Value);

            return await query.SumAsync(t => t.RefundAmount ?? 0);
        }

        public async Task<bool> CanRefundTransactionAsync(int transactionId)
        {
            var transaction = await _unitOfWork.Transactions.GetByIdAsync(transactionId);
            return transaction != null && 
                   transaction.Status == "Completed" && 
                   !transaction.IsRefunded;
        }

        public async Task<List<TransactionSummaryDto>> SearchTransactionsAsync(string searchTerm, int limit = 50)
        {
            var transactions = await _unitOfWork.Transactions.SearchTransactionsAsync(searchTerm, limit);
            return _mapper.Map<List<TransactionSummaryDto>>(transactions);
        }

        public async Task<List<TransactionAdvancedDto>> GetTransactionsByStatusAsync(string status, int pageNumber = 1, int pageSize = 20)
        {
            var transactions = await _unitOfWork.Transactions.GetTransactionsByStatusAsync(status, pageNumber, pageSize);
            return _mapper.Map<List<TransactionAdvancedDto>>(transactions);
        }

        public async Task<List<TransactionAdvancedDto>> GetTransactionsByPaymentMethodAsync(PaymentMethod paymentMethod, int pageNumber = 1, int pageSize = 20)
        {
            var transactions = await _unitOfWork.Transactions.GetTransactionsByPaymentMethodAsync(paymentMethod, pageNumber, pageSize);
            return _mapper.Map<List<TransactionAdvancedDto>>(transactions);
        }

        public async Task<byte[]> ExportTransactionsToCsvAsync(TransactionFilterDto filter)
        {
            var transactions = await GetTransactionsAsync(filter);
            var csv = new StringBuilder();
            
            // Add headers
            csv.AppendLine("ID,Order Number,Customer Name,Amount,Payment Method,Status,Transaction Date,Refunded");
            
            // Add data
            foreach (var transaction in transactions.Items)
            {
                csv.AppendLine($"{transaction.Id},{transaction.OrderNumber},{transaction.CustomerName},{transaction.Amount},{transaction.PaymentMethodName},{transaction.Status},{transaction.TransactionDate:yyyy-MM-dd HH:mm:ss},{transaction.IsRefunded}");
            }
            
            return Encoding.UTF8.GetBytes(csv.ToString());
        }

        public async Task<byte[]> ExportTransactionsToExcelAsync(TransactionFilterDto filter)
        {
            // This would require EPPlus or similar library
            // For now, return CSV as placeholder
            return await ExportTransactionsToCsvAsync(filter);
        }

        private static System.Linq.Expressions.Expression<Func<Transaction, object>> GetSortExpression(string sortBy)
        {
            return sortBy.ToLower() switch
            {
                "amount" => t => t.Amount,
                "status" => t => t.Status,
                "paymentmethod" => t => t.PaymentMethod,
                "orderid" => t => t.OrderId,
                "transactiondate" => t => t.TransactionDate,
                _ => t => t.TransactionDate
            };
        }

        private static string GenerateTransactionReference()
        {
            return $"TXN-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
        }

        private async Task<bool> ProcessPaymentWithGateway(PaymentProcessingDto paymentDto)
        {
            // Simulate payment gateway processing
            await Task.Delay(1000); // Simulate API call delay
            
            // In real implementation, integrate with actual payment gateway
            // For demo purposes, randomly succeed/fail based on amount
            return paymentDto.PaymentMethod != PaymentMethod.Card || new Random().Next(1, 10) > 2;
        }

        private async Task<bool> ProcessRefundWithGateway(Transaction transaction, decimal refundAmount)
        {
            // Simulate refund processing
            await Task.Delay(500); // Simulate API call delay
            
            // In real implementation, integrate with actual payment gateway
            return true; // For demo purposes, always succeed
        }
    }
}
