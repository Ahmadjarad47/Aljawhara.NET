using Ecom.Application.DTOs.Common;
using Ecom.Application.DTOs.Order;
using Ecom.Domain.constant;

namespace Ecom.Application.Services.Interfaces
{
    public interface ITransactionService
    {
        // Basic CRUD operations
        Task<TransactionAdvancedDto?> GetTransactionByIdAsync(int id);
        Task<TransactionAdvancedDto?> GetTransactionByReferenceAsync(string reference);
        Task<PagedResult<TransactionAdvancedDto>> GetTransactionsAsync(TransactionFilterDto filter);
        Task<TransactionAdvancedDto> CreateTransactionAsync(TransactionCreateAdvancedDto transactionDto);
        Task<TransactionAdvancedDto> UpdateTransactionAsync(TransactionUpdateDto transactionDto);
        Task<bool> DeleteTransactionAsync(int id);

        // Order-related operations
        Task<PagedResult<TransactionAdvancedDto>> GetTransactionsByOrderAsync(int orderId, int pageNumber = 1, int pageSize = 20);
        Task<TransactionAdvancedDto?> GetLatestTransactionByOrderAsync(int orderId);
        Task<bool> HasSuccessfulTransactionAsync(int orderId);

        // Payment processing
        Task<TransactionAdvancedDto> ProcessPaymentAsync(PaymentProcessingDto paymentDto);
        Task<string?> GetPaymentUrlByTransactionIdAsync(int transactionId);
        Task<TransactionAdvancedDto> ProcessRefundAsync(TransactionRefundDto refundDto);
        Task<bool> UpdateTransactionStatusAsync(int transactionId, TransactionStatus status, string? gatewayResponse = null);

        // Analytics and reporting
        Task<TransactionAnalyticsDto> GetTransactionAnalyticsAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<TransactionAnalyticsDto> GetTransactionAnalyticsByOrderAsync(int orderId);
        Task<Dictionary<PaymentMethod, decimal>> GetRevenueByPaymentMethodAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<List<TransactionTrendDto>> GetTransactionTrendsAsync(DateTime startDate, DateTime endDate, string period = "daily");

        // Status management
        Task<bool> MarkTransactionAsCompletedAsync(int transactionId, string? reference = null);
        Task<bool> MarkTransactionAsFailedAsync(int transactionId, string reason);
        Task<bool> MarkTransactionAsPendingAsync(int transactionId);

        // Refund operations
        Task<List<TransactionAdvancedDto>> GetRefundedTransactionsAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<decimal> GetTotalRefundedAmountAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<bool> CanRefundTransactionAsync(int transactionId);

        // Search and filtering
        Task<List<TransactionSummaryDto>> SearchTransactionsAsync(string searchTerm, int limit = 50);
        Task<List<TransactionAdvancedDto>> GetTransactionsByStatusAsync(TransactionStatus status, int pageNumber = 1, int pageSize = 20);
        Task<List<TransactionAdvancedDto>> GetTransactionsByPaymentMethodAsync(PaymentMethod paymentMethod, int pageNumber = 1, int pageSize = 20);

        // Export functionality
        Task<byte[]> ExportTransactionsToCsvAsync(TransactionFilterDto filter);
        Task<byte[]> ExportTransactionsToExcelAsync(TransactionFilterDto filter);

        /// <summary>
        /// Handles Sadad paid webhook: verifies payment via Sadad API, then updates transaction and order status.
        /// </summary>
        Task<bool> HandleSadadPaidWebhookAsync(SadadWebhookDto webhookPayload);
    }
}
