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

            await _unitOfWork.BeginTransactionAsync();

            try
            {
                // 1️⃣ Get existing shipping address
                var shipping = await _unitOfWork.ShippingAddresses
                    .FirstOrDefaultAsync(s => s.AppUserId == userId);

                if (shipping == null)
                    throw new ArgumentException("No shipping address found for this user. Please add a shipping address first.");

                // 2️⃣ Map order WITHOUT mapping ShippingAddress from DTO
                var order = _mapper.Map<Order>(orderDto);
                order.AppUserId = userId;
                order.ShippingAddressId = shipping.Id;
                order.ShippingAddress = shipping;
                order.OrderNumber = await GenerateOrderNumberAsync();

                // 3️⃣ Calculate subtotal and create order items
                decimal subtotal = 0;
                var orderItems = new List<OrderItem>();

                foreach (var itemDto in orderDto.Items)
                {
                    var product = await _unitOfWork.Products.GetByIdAsync(itemDto.ProductId);
                    if (product == null || product.IsDeleted)
                        throw new ArgumentException($"Product '{itemDto.ProductId}' not found or unavailable.");

                    if (!product.IsInStock || product.TotalInStock < itemDto.Quantity)
                        throw new ArgumentException($"Insufficient stock for '{product.Title}'.");

                    var currentPrice = product.newPrice;
                    subtotal += currentPrice * itemDto.Quantity;

                    var orderItem = new OrderItem
                    {
                        ProductId = product.Id,
                        Name = product.Title,
                        Image = product.Images?.FirstOrDefault() ?? string.Empty,
                        Price = currentPrice,
                        Quantity = itemDto.Quantity
                    };

                    orderItems.Add(orderItem);
                }

                order.Subtotal = subtotal;

                // 4️⃣ Apply coupon if provided
                decimal couponDiscountAmount = 0;
                if (!string.IsNullOrEmpty(orderDto.CouponCode))
                {
                    try
                    {
                        var coupon = await _couponService.ApplyCouponToOrderAsync(orderDto.CouponCode, subtotal, userId);
                        order.CouponId = coupon.Id;
                        couponDiscountAmount = CalculateCouponDiscount(coupon, subtotal);
                        order.CouponDiscountAmount = couponDiscountAmount;
                    }
                    catch (ArgumentException ex)
                    {
                        throw new ArgumentException($"Coupon validation failed: {ex.Message}");
                    }
                }

                // 5️⃣ Calculate shipping, tax, and total
                order.Shipping = CalculateShipping(subtotal, orderDto.CouponCode);
                order.Tax = CalculateTax(subtotal);
                order.Total = order.Subtotal + order.Shipping + order.Tax - couponDiscountAmount;

                // 6️⃣ Save order
                await _unitOfWork.Orders.AddAsync(order);
                await _unitOfWork.SaveChangesAsync();

                // 7️⃣ Save order items
                foreach (var orderItem in orderItems)
                {
                    orderItem.OrderId = order.Id;
                    await _unitOfWork.OrderItems.AddAsync(orderItem);
                }

                await _unitOfWork.SaveChangesAsync();

                // 8️⃣ Reduce stock
                foreach (var orderItem in orderItems)
                {
                    var reduced = await _productService.ReduceProductStockAsync(orderItem.ProductId, orderItem.Quantity);
                    if (!reduced)
                        throw new InvalidOperationException($"Failed to reduce stock for product {orderItem.ProductId}");
                }

                // 9️⃣ Increment coupon usage
                if (order.CouponId.HasValue)
                    await _couponService.IncrementCouponUsageAsync(order.CouponId.Value);

                await _unitOfWork.CommitTransactionAsync();

                // 10️⃣ Return order with items
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
            
            // Simple shipping calculation - in real app this would be more complex
            return subtotal > 100 ? 0 : 10;
        }

        private static decimal CalculateTax(decimal subtotal)
        {
            // Simple tax calculation - in real app this would depend on location
            return subtotal * 0.08m; // 8% tax
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


