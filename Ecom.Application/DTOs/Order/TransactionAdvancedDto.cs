using Ecom.Domain.constant;
using System.ComponentModel.DataAnnotations;

namespace Ecom.Application.DTOs.Order
{
    public class TransactionAdvancedDto
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string? AppUserId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerEmail { get; set; }
        public decimal Amount { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public string PaymentMethodName => PaymentMethod.ToString();
        public TransactionStatus Status { get; set; }
        public DateTime TransactionDate { get; set; }
        public DateTime? ProcessedDate { get; set; }
        public string? TransactionReference { get; set; }
        public string? PaymentGatewayResponse { get; set; }
        public string? Notes { get; set; }
        public bool IsRefunded { get; set; }
        public decimal? RefundAmount { get; set; }
        public DateTime? RefundDate { get; set; }
        public string? RefundReason { get; set; }
        public string? GatewayInvoiceId { get; set; }
        public string? PaymentUrl { get; set; }
        public bool IsActive { get; set; }
    }

    public class TransactionCreateAdvancedDto
    {

        [Required]
        public int OrderId { get; set; }
        
        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
        public decimal Amount { get; set; }
        public string? AppUserId { get; set; }

        [Required]
        public PaymentMethod PaymentMethod { get; set; }
        
        public TransactionStatus Status { get; set; } = TransactionStatus.Pending;
        
        public string? TransactionReference { get; set; }
        
        public string? PaymentGatewayResponse { get; set; }
        
        public string? Notes { get; set; }
        
        public string? GatewayInvoiceId { get; set; }
    }

    public class TransactionUpdateDto
    {
        [Required]
        public int Id { get; set; }
        
        public TransactionStatus Status { get; set; }
        
        public string? TransactionReference { get; set; }
        
        public string? PaymentGatewayResponse { get; set; }
        
        public string? Notes { get; set; }
        
        public string? GatewayInvoiceId { get; set; }
    }

    public class TransactionRefundDto
    {
        [Required]
        public int TransactionId { get; set; }
        
        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Refund amount must be greater than 0")]
        public decimal RefundAmount { get; set; }
        
        [Required]
        public string RefundReason { get; set; } = string.Empty;
        
        public string? Notes { get; set; }
    }

    public class TransactionFilterDto
    {
        public int? OrderId { get; set; }
        public string? OrderNumber { get; set; }
        public string? AppUserId { get; set; }
        public PaymentMethod? PaymentMethod { get; set; }
        public TransactionStatus? Status { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal? MinAmount { get; set; }
        public decimal? MaxAmount { get; set; }
        public bool? IsRefunded { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string SortBy { get; set; } = "TransactionDate";
        public string SortDirection { get; set; } = "desc";
    }

    public class TransactionAnalyticsDto
    {
        public decimal TotalTransactions { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal AverageTransactionAmount { get; set; }
        public decimal SuccessfulTransactions { get; set; }
        public decimal FailedTransactions { get; set; }
        public decimal PendingTransactions { get; set; }
        public decimal RefundedAmount { get; set; }
        public decimal NetAmount { get; set; }
        public Dictionary<string, int> TransactionsByStatus { get; set; } = new();
        public Dictionary<string, decimal> AmountByPaymentMethod { get; set; } = new();
        public Dictionary<string, int> CountByPaymentMethod { get; set; } = new();
        public List<TransactionTrendDto> DailyTrends { get; set; } = new();
        public List<TransactionTrendDto> MonthlyTrends { get; set; } = new();
    }

    public class TransactionTrendDto
    {
        public string Period { get; set; } = string.Empty;
        public int TransactionCount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal AverageAmount { get; set; }
    }

    public class TransactionSummaryDto
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public TransactionStatus Status { get; set; }
        public DateTime TransactionDate { get; set; }
        public string? GatewayInvoiceId { get; set; }
        public bool IsRefunded { get; set; }
        public bool IsActive { get; set; }
    }

    public class PaymentProcessingDto
    {
        [Required]
        public int OrderId { get; set; }
        
        [Required]
        public PaymentMethod PaymentMethod { get; set; }
        
        public string? CardNumber { get; set; }
        public string? CardExpiryMonth { get; set; }
        public string? CardExpiryYear { get; set; }
        public string? CardCvv { get; set; }
        public string? CardHolderName { get; set; }
        
        public string? PayPalEmail { get; set; }
        
        public string? BankAccountNumber { get; set; }
        public string? BankRoutingNumber { get; set; }
        public string? BankName { get; set; }
        
        public string? Notes { get; set; }
    }
}
