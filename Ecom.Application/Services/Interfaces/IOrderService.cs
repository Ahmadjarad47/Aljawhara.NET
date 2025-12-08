using Ecom.Application.DTOs.Order;
using Ecom.Application.Mappings;
using Ecom.Domain.constant;

namespace Ecom.Application.Services.Interfaces
{
    public interface IOrderService
    {
        Task<OrderDto?> GetOrderByIdAsync(int id);
        Task<OrderDto?> GetOrderByNumberAsync(string orderNumber);
        Task<IEnumerable<OrderSummaryDto>> GetOrdersByUserAsync(string userId);
        Task<IEnumerable<OrderSummaryDto>> GetOrdersByStatusAsync(OrderStatus status);
        Task<IEnumerable<OrderSummaryDto>> GetRecentOrdersAsync(int count = 10);
        
        Task<OrderDto> CreateOrderAsync(OrderCreateDto orderDto, string? userId = null);
        Task<OrderDto> UpdateOrderStatusAsync(OrderUpdateStatusDto orderUpdateDto);
        Task<bool> CancelOrderAsync(int orderId);
        
        Task<decimal> GetTotalSalesAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<Dictionary<OrderStatus, int>> GetOrderStatisticsAsync();
        
        Task<TransactionDto> ProcessPaymentAsync(TransactionCreateDto transactionDto);
        Task<IEnumerable<TransactionDto>> GetOrderTransactionsAsync(int orderId);
        
        Task<InvoicePaymentDto?> GetInvoicePaymentDataAsync(int orderId);
    }
}


