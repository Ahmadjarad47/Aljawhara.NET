using AutoMapper;
using Ecom.Application.DTOs.Order;
using Ecom.Application.Mappings;
using Ecom.Application.Services.Interfaces;
using Ecom.Domain.Entity;
using Ecom.Domain.constant;
using Ecom.Infrastructure.UnitOfWork;

namespace Ecom.Application.Services
{
    public class OrderService : IOrderService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public OrderService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<OrderDto?> GetOrderByIdAsync(int id)
        {
            var order = await _unitOfWork.Orders.GetOrderWithItemsAsync(id);
            return order != null ? _mapper.Map<OrderDto>(order) : null;
        }

        public async Task<OrderDto?> GetOrderByNumberAsync(string orderNumber)
        {
            var order = await _unitOfWork.Orders.GetOrderByNumberAsync(orderNumber);
            return order != null ? _mapper.Map<OrderDto>(order) : null;
        }

        public async Task<IEnumerable<OrderSummaryDto>> GetOrdersByUserAsync(string userId)
        {
            var orders = await _unitOfWork.Orders.GetOrdersByUserAsync(userId);
            return _mapper.Map<IEnumerable<OrderSummaryDto>>(orders);
        }

        public async Task<IEnumerable<OrderSummaryDto>> GetOrdersByStatusAsync(OrderStatus status)
        {
            var orders = await _unitOfWork.Orders.GetOrdersByStatusAsync(status);
            return _mapper.Map<IEnumerable<OrderSummaryDto>>(orders);
        }

        public async Task<IEnumerable<OrderSummaryDto>> GetRecentOrdersAsync(int count = 10)
        {
            var orders = await _unitOfWork.Orders.GetRecentOrdersAsync(count);
            return _mapper.Map<IEnumerable<OrderSummaryDto>>(orders);
        }

        public async Task<OrderDto> CreateOrderAsync(OrderCreateDto orderDto, string? userId = null)
        {
            await _unitOfWork.BeginTransactionAsync();
            
            try
            {
                // Create shipping address
                var shippingAddress = _mapper.Map<ShippingAddress>(orderDto.ShippingAddress);
                shippingAddress.AppUserId = userId;
                await _unitOfWork.ShippingAddresses.AddAsync(shippingAddress);
                await _unitOfWork.SaveChangesAsync();

                // Create order
                var order = _mapper.Map<Order>(orderDto);
                order.AppUserId = userId;
                order.ShippingAddressId = shippingAddress.Id;
                order.OrderNumber = await GenerateOrderNumberAsync();
                
                // Calculate totals
                decimal subtotal = 0;
                foreach (var itemDto in orderDto.Items)
                {
                    subtotal += itemDto.Price * itemDto.Quantity;
                }
                
                order.Subtotal = subtotal;
                order.Shipping = CalculateShipping(subtotal);
                order.Tax = CalculateTax(subtotal);
                order.Total = order.Subtotal + order.Shipping + order.Tax - (order.Discount ?? 0);

                await _unitOfWork.Orders.AddAsync(order);
                await _unitOfWork.SaveChangesAsync();

                // Create order items
                foreach (var itemDto in orderDto.Items)
                {
                    var orderItem = _mapper.Map<OrderItem>(itemDto);
                    orderItem.OrderId = order.Id;
                    await _unitOfWork.OrderItems.AddAsync(orderItem);
                }

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransactionAsync();

                var createdOrder = await _unitOfWork.Orders.GetOrderWithItemsAsync(order.Id);
                return _mapper.Map<OrderDto>(createdOrder);
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync();
                throw;
            }
        }

        public async Task<OrderDto> UpdateOrderStatusAsync(OrderUpdateStatusDto orderUpdateDto)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(orderUpdateDto.Id);
            if (order == null)
                throw new ArgumentException($"Order with ID {orderUpdateDto.Id} not found.");

            order.Status = orderUpdateDto.Status;
            _unitOfWork.Orders.Update(order);
            await _unitOfWork.SaveChangesAsync();

            var updatedOrder = await _unitOfWork.Orders.GetOrderWithItemsAsync(orderUpdateDto.Id);
            return _mapper.Map<OrderDto>(updatedOrder);
        }

        public async Task<bool> CancelOrderAsync(int orderId)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
            if (order == null)
                return false;

            if (order.Status == OrderStatus.Shipped || order.Status == OrderStatus.Delivered)
                throw new InvalidOperationException("Cannot cancel shipped or delivered orders.");

            order.Status = OrderStatus.Cancelled;
            _unitOfWork.Orders.Update(order);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<decimal> GetTotalSalesAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            return await _unitOfWork.Orders.GetTotalSalesAsync(startDate, endDate);
        }

        public async Task<Dictionary<OrderStatus, int>> GetOrderStatisticsAsync()
        {
            var statistics = new Dictionary<OrderStatus, int>();
            
            foreach (OrderStatus status in Enum.GetValues<OrderStatus>())
            {
                var count = await _unitOfWork.Orders.GetOrderCountByStatusAsync(status);
                statistics[status] = count;
            }
            
            return statistics;
        }

        public async Task<TransactionDto> ProcessPaymentAsync(TransactionCreateDto transactionDto)
        {
            var transaction = _mapper.Map<Transaction>(transactionDto);
            transaction.Status = "Completed"; // In real app, this would be determined by payment processor
            
            await _unitOfWork.Transactions.AddAsync(transaction);
            await _unitOfWork.SaveChangesAsync();
            
            return _mapper.Map<TransactionDto>(transaction);
        }

        public async Task<IEnumerable<TransactionDto>> GetOrderTransactionsAsync(int orderId)
        {
            var transactions = await _unitOfWork.Transactions.FindAsync(t => t.OrderId == orderId);
            return _mapper.Map<IEnumerable<TransactionDto>>(transactions);
        }

        private async Task<string> GenerateOrderNumberAsync()
        {
            var today = DateTime.UtcNow.ToString("yyyyMMdd");
            var count = await _unitOfWork.Orders.CountAsync(o => o.CreatedAt.Date == DateTime.UtcNow.Date);
            return $"ORD-{today}-{(count + 1):D4}";
        }

        private static decimal CalculateShipping(decimal subtotal)
        {
            // Simple shipping calculation - in real app this would be more complex
            return subtotal > 100 ? 0 : 10;
        }

        private static decimal CalculateTax(decimal subtotal)
        {
            // Simple tax calculation - in real app this would depend on location
            return subtotal * 0.08m; // 8% tax
        }
    }
}


