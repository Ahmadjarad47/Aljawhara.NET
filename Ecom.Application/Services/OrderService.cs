using AutoMapper;
using Ecom.Application.DTOs.Coupon;
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
        private readonly ICouponService _couponService;
        private readonly IProductService _productService;

        public OrderService(IUnitOfWork unitOfWork, IMapper mapper, ICouponService couponService, IProductService productService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _couponService = couponService;
            _productService = productService;
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
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentException("User ID is required to create an order.");

            var context = _unitOfWork.Context;
            var strategy = context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(
                state: orderDto,
                operation: async (db, state, token) =>
                {
                    await _unitOfWork.BeginTransactionAsync();

                    try
                    {
                        // 1️⃣ Get shipping
                        var shipping = await _unitOfWork.ShippingAddresses
                            .FirstOrDefaultAsync(s => s.AppUserId == userId);

                        if (shipping == null)
                            throw new ArgumentException("No shipping address found.");

                        // 2️⃣ Create order
                        var order = _mapper.Map<Order>(state);
                        order.AppUserId = userId;
                        order.ShippingAddressId = shipping.Id;
                        order.OrderNumber = await GenerateOrderNumberAsync();
                        order.Items.Clear(); // Ensure Items collection is empty since we'll add them manually

                        decimal subtotal = 0;
                        var orderItems = new List<OrderItem>();

                        // 3️⃣ Items
                        foreach (var itemDto in state.Items)
                        {
                            var product = await _unitOfWork.Products.GetByIdAsync(itemDto.ProductId);

                            if (product == null || product.IsDeleted)
                                throw new ArgumentException($"Product '{itemDto.ProductId}' not found.");

                            if (!product.IsInStock || product.TotalInStock < itemDto.Quantity)
                                throw new ArgumentException($"Insufficient stock for '{product.Title}'.");

                            var price = product.newPrice;
                            subtotal += price * itemDto.Quantity;

                            orderItems.Add(new OrderItem
                            {
                                ProductId = product.Id,
                                Name = product.Title,
                                Image = product.Images?.FirstOrDefault() ?? "",
                                Price = price,
                                Quantity = itemDto.Quantity
                            });
                        }

                        order.Subtotal = subtotal;

                        // 4️⃣ Coupon
                        decimal discount = 0;

                        if (!string.IsNullOrEmpty(state.CouponCode))
                        {
                            var coupon = await _couponService.ApplyCouponToOrderAsync(
                                state.CouponCode,
                                subtotal,
                                userId);

                            order.CouponId = coupon.Id;
                            discount = CalculateCouponDiscount(coupon, subtotal);
                            order.CouponDiscountAmount = discount;
                        }

                        // 5️⃣ Totals
                        order.Shipping = CalculateShipping(subtotal, state.CouponCode);
                        order.Tax = CalculateTax(subtotal);
                        order.Total = subtotal + order.Shipping + order.Tax - discount;

                        // 6️⃣ Save order
                        await _unitOfWork.Orders.AddAsync(order);
                        await _unitOfWork.SaveChangesAsync();

                        // 7️⃣ Save items
                        foreach (var item in orderItems)
                        {
                            item.OrderId = order.Id;
                            await _unitOfWork.OrderItems.AddAsync(item);
                        }

                        await _unitOfWork.SaveChangesAsync();

                        // 8️⃣ Stock reduction
                        foreach (var item in orderItems)
                        {
                            var ok = await _productService.ReduceProductStockAsync(item.ProductId, item.Quantity);

                            if (!ok)
                                throw new InvalidOperationException($"Failed to reduce stock for {item.ProductId}");
                        }

                        // 9️⃣ Coupon usage
                        if (order.CouponId.HasValue)
                            await _couponService.IncrementCouponUsageAsync(order.CouponId.Value);

                        // 10️⃣ Commit
                        await _unitOfWork.CommitTransactionAsync();

                        var created = await _unitOfWork.Orders.GetOrderWithItemsAsync(order.Id);
                        return _mapper.Map<OrderDto>(created);
                    }
                    catch
                    {
                        await _unitOfWork.RollbackTransactionAsync();
                        throw;
                    }
                },
                verifySucceeded: null,
                cancellationToken: default
            );
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
            await _unitOfWork.BeginTransactionAsync();
            
            try
            {
                var order = await _unitOfWork.Orders.GetOrderWithItemsAsync(orderId);
                if (order == null)
                    return false;

                if (order.Status == OrderStatus.Shipped || order.Status == OrderStatus.Delivered)
                    throw new InvalidOperationException("Cannot cancel shipped or delivered orders.");

                // Restore stock for all products in the cancelled order
                foreach (var orderItem in order.Items)
                {
                    var stockIncreased = await _productService.IncreaseProductStockAsync(orderItem.ProductId, orderItem.Quantity);
                    if (!stockIncreased)
                    {
                        throw new InvalidOperationException($"Failed to restore stock for product ID {orderItem.ProductId}");
                    }
                }

                order.Status = OrderStatus.Cancelled;
                _unitOfWork.Orders.Update(order);
                await _unitOfWork.SaveChangesAsync();
                
                await _unitOfWork.CommitTransactionAsync();
                return true;
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync();
                throw;
            }
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

        private static decimal CalculateShipping(decimal subtotal, string? couponCode = null)
        {
            // Check if coupon provides free shipping
            if (!string.IsNullOrEmpty(couponCode))
            {
                // This would be checked against coupon type in real implementation
                // For now, we'll use a simple check
                if (couponCode.ToUpper().Contains("FREESHIP"))
                {
                    return 0;
                }
            }
            
            // Fixed shipping cost: always 2
            return 2;
        }

        private static decimal CalculateTax(decimal subtotal)
        {
            // No tax - always return 0
            return 0;
        }

        private static decimal CalculateCouponDiscount(CouponDto coupon, decimal subtotal)
        {
            decimal discountAmount = 0;

            switch (coupon.Type)
            {
                case CouponType.Percentage:
                    discountAmount = subtotal * (coupon.Value / 100);
                    break;
                case CouponType.FixedAmount:
                    discountAmount = coupon.Value;
                    break;
                case CouponType.FreeShipping:
                    // Free shipping is handled in shipping calculation
                    discountAmount = 0;
                    break;
            }

            // Apply maximum discount limit if specified
            if (coupon.MaximumDiscountAmount.HasValue && discountAmount > coupon.MaximumDiscountAmount.Value)
            {
                discountAmount = coupon.MaximumDiscountAmount.Value;
            }

            // Ensure discount doesn't exceed order amount
            if (discountAmount > subtotal)
            {
                discountAmount = subtotal;
            }

            return discountAmount;
        }
    }
}


