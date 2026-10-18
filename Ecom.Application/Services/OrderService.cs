using AutoMapper;
using Ecom.Application.DTOs.Coupon;
using Ecom.Application.DTOs.Order;
using Ecom.Application.Mappings;
using Ecom.Application.Services.Interfaces;
using Ecom.Domain.Entity;
using Ecom.Domain.constant;
using Ecom.Infrastructure.UnitOfWork;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Ecom.Application.Payments;
using Ecom.Infrastructure.Data;

namespace Ecom.Application.Services
{
    public class OrderService(
       IUnitOfWork unitOfWork,
       IMapper mapper,
       ICouponService couponService,
       IProductService productService,
       ITransactionService transaction,
       IShippingService shippingService,
       IConfiguration? configuration = null
   ) : IOrderService
    {
        private static readonly TimeSpan GuestAccessLifetime = TimeSpan.FromDays(30);
        private static readonly TimeSpan ElectronicReservationLifetime = TimeSpan.FromMinutes(30);
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IMapper _mapper = mapper;
        private readonly ICouponService _couponService = couponService;
        private readonly IProductService _productService = productService;
        private readonly ITransactionService _transaction = transaction;
        private readonly IConfiguration? _configuration = configuration;

    // باقي الميثودات هنا



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
        public async Task<TransactionAdvancedDto> CreateOrderAsync(OrderCreateDto orderDto, string? userId = null, string? idempotencyKey = null)
        {
            ArgumentNullException.ThrowIfNull(orderDto);
            if (orderDto.Items == null || orderDto.Items.Count == 0)
                throw new ArgumentException("At least one order item is required.");
            if (orderDto.Items.Any(item => item.ProductId <= 0 || item.Quantity <= 0))
                throw new ArgumentException("Every order item must have a valid product and a positive quantity.");
            if (!Enum.IsDefined(orderDto.PaymentMethod))
                throw new ArgumentException("Invalid payment method.");

            idempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
            if (idempotencyKey?.Length > 200)
                throw new ArgumentException("Idempotency-Key cannot exceed 200 characters.");

            var requestHash = ComputeCheckoutRequestHash(orderDto, userId);
            var keyHash = idempotencyKey == null ? null : HashGuestAccessToken($"checkout:{idempotencyKey}");
            var ecomDb = (EcomDbContext)_unitOfWork.Context;
            if (keyHash != null)
            {
                var existing = await ecomDb.Orders.AsNoTracking().SingleOrDefaultAsync(o => o.CheckoutIdempotencyKeyHash == keyHash);
                if (existing != null)
                    return await BuildCheckoutResultAsync(existing, idempotencyKey!, requestHash);
            }

            var strategy = ecomDb.Database.CreateExecutionStrategy();
            try
            {
                return await strategy.ExecuteAsync(async () =>
                {
                    await _unitOfWork.BeginTransactionAsync();
                    try
                    {
                        ShippingAddress? shipping = null;
                        if (!string.IsNullOrEmpty(userId))
                        {
                            shipping = await _unitOfWork.ShippingAddresses.FirstOrDefaultAsync(s => s.AppUserId == userId && s.IsDefault && !s.IsDeleted)
                                ?? await _unitOfWork.ShippingAddresses.FirstOrDefaultAsync(s => s.AppUserId == userId && !s.IsDeleted);
                        }
                        if (shipping == null)
                        {
                            shipping = _mapper.Map<ShippingAddress>(orderDto.ShippingAddress);
                            shipping.AppUserId = userId;
                            await _unitOfWork.ShippingAddresses.AddAsync(shipping);
                            await _unitOfWork.SaveChangesAsync();
                        }

                        var order = _mapper.Map<Order>(orderDto);
                        order.AppUserId = userId;
                        order.ShippingAddressId = shipping.Id;
                        order.OrderNumber = await GenerateOrderNumberAsync();
                        order.CheckoutIdempotencyKeyHash = keyHash;
                        order.CheckoutRequestHash = requestHash;
                        order.ReservationExpiresAtUtc = orderDto.PaymentMethod == PaymentMethod.Cod
                            ? DateTime.UtcNow.AddDays(30)
                            : DateTime.UtcNow.Add(ElectronicReservationLifetime);

                        string? guestAccessToken = null;
                        if (string.IsNullOrWhiteSpace(userId))
                        {
                            order.GuestTokenDerivationNonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
                            guestAccessToken = idempotencyKey == null ? CreateGuestAccessToken() : DeriveGuestAccessToken(idempotencyKey, order.GuestTokenDerivationNonce);
                            order.GuestAccessTokenHash = HashGuestAccessToken(guestAccessToken);
                            order.GuestAccessExpiresAtUtc = DateTime.UtcNow.Add(GuestAccessLifetime);
                        }

                        decimal subtotal = 0m;
                        var orderItems = new List<OrderItem>();
                        var normalizedItems = orderDto.Items.GroupBy(i => i.ProductId)
                            .Select(g => new { ProductId = g.Key, Quantity = checked(g.Sum(i => i.Quantity)) })
                            .OrderBy(i => i.ProductId).ToList();
                        foreach (var requested in normalizedItems)
                        {
                            var product = await _unitOfWork.Products.GetByIdAsync(requested.ProductId);
                            if (product == null || product.IsDeleted)
                                throw new ArgumentException($"Product '{requested.ProductId}' not found.");
                            if (!product.IsInStock || product.TotalInStock < requested.Quantity)
                                throw new ArgumentException($"Insufficient stock for '{product.Title}'.");
                            subtotal += product.newPrice * requested.Quantity;
                            orderItems.Add(new OrderItem { ProductId = product.Id, Name = product.Title, Image = product.Images?.FirstOrDefault() ?? "", Price = KwdMoney.Round(product.newPrice), Quantity = requested.Quantity });
                        }
                        order.Subtotal = KwdMoney.Round(subtotal);

                        decimal discount = 0m;
                        CouponType? couponType = null;
                        if (!string.IsNullOrWhiteSpace(orderDto.CouponCode))
                        {
                            var coupon = await _couponService.ApplyCouponToOrderAsync(orderDto.CouponCode, order.Subtotal, userId);
                            order.CouponId = coupon.Id;
                            discount = KwdMoney.Round(CalculateCouponDiscount(coupon, order.Subtotal));
                            order.CouponDiscountAmount = discount;
                            couponType = coupon.Type;
                        }
                        order.Shipping = KwdMoney.Round(await shippingService.CalculateAsync(order.Subtotal, couponType, shipping.City));
                        order.Tax = KwdMoney.Round(CalculateTax(order.Subtotal));
                        order.Total = KwdMoney.Round(order.Subtotal + order.Shipping + order.Tax - discount);

                        await _unitOfWork.Orders.AddAsync(order);
                        await _unitOfWork.SaveChangesAsync();
                        foreach (var item in orderItems)
                        {
                            item.OrderId = order.Id;
                            await _unitOfWork.OrderItems.AddAsync(item);
                            if (!await _productService.ReduceProductStockAsync(item.ProductId, item.Quantity))
                                throw new InvalidOperationException($"Failed to reserve stock for {item.ProductId}.");
                            ecomDb.InventoryReservations.Add(new InventoryReservation
                            {
                                OrderId = order.Id, ProductId = item.ProductId, Quantity = item.Quantity,
                                Status = orderDto.PaymentMethod == PaymentMethod.Cod ? ReservationStatus.Confirmed : ReservationStatus.Active,
                                ExpiresAtUtc = order.ReservationExpiresAtUtc.Value,
                                ConfirmedAtUtc = orderDto.PaymentMethod == PaymentMethod.Cod ? DateTime.UtcNow : null
                            });
                        }

                        if (order.CouponId.HasValue)
                        {
                            if (!await _unitOfWork.Coupons.TryReserveUsageAsync(order.CouponId.Value, DateTime.UtcNow))
                                throw new InvalidOperationException("Coupon usage limit has been reached.");
                            ecomDb.CouponReservations.Add(new CouponReservation
                            {
                                OrderId = order.Id, CouponId = order.CouponId.Value,
                                Status = orderDto.PaymentMethod == PaymentMethod.Cod ? ReservationStatus.Confirmed : ReservationStatus.Active,
                                ExpiresAtUtc = order.ReservationExpiresAtUtc.Value,
                                ConfirmedAtUtc = orderDto.PaymentMethod == PaymentMethod.Cod ? DateTime.UtcNow : null
                            });
                        }

                        var transactionEntity = new Transaction
                        {
                            OrderId = order.Id, Order = order, AppUserId = string.IsNullOrWhiteSpace(userId) ? null : userId,
                            Amount = order.Total, PaymentMethod = orderDto.PaymentMethod, Status = TransactionStatus.Pending,
                            TransactionDate = DateTime.UtcNow, TransactionReference = $"TXN-{Guid.NewGuid():N}".ToUpperInvariant()
                        };
                        ecomDb.Transactions.Add(transactionEntity);
                        await _unitOfWork.SaveChangesAsync();

                        PaymentAttempt? paymentAttempt = null;
                        if (orderDto.PaymentMethod != PaymentMethod.Cod)
                        {
                            paymentAttempt = new PaymentAttempt
                            {
                                OrderId = order.Id, TransactionId = transactionEntity.Id,
                                IdempotencyKeyHash = HashGuestAccessToken($"payment:{keyHash ?? Guid.NewGuid().ToString("N")}:initial"),
                                RequestHash = requestHash, MerchantReference = $"PAY-{Guid.NewGuid():N}".ToUpperInvariant(),
                                Amount = order.Total, Currency = "KWD", Status = PaymentAttemptStatus.PendingDispatch,
                                NextActionAtUtc = DateTime.UtcNow
                            };
                            ecomDb.PaymentAttempts.Add(paymentAttempt);
                            await _unitOfWork.SaveChangesAsync();
                            ecomDb.OutboxMessages.Add(new OutboxMessage
                            {
                                Type = "PaymentInvoiceRequested", DeduplicationKey = $"payment-attempt:{paymentAttempt.Id}:dispatch",
                                Payload = JsonSerializer.Serialize(new { PaymentAttemptId = paymentAttempt.Id }), NextAttemptAtUtc = DateTime.UtcNow
                            });
                        }
                        ecomDb.OutboxMessages.Add(new OutboxMessage
                        {
                            Type = "OrderCreatedEmail", DeduplicationKey = $"order:{order.Id}:created-email",
                            Payload = JsonSerializer.Serialize(new { OrderId = order.Id }), NextAttemptAtUtc = DateTime.UtcNow
                        });
                        await _unitOfWork.SaveChangesAsync();
                        await _unitOfWork.CommitTransactionAsync();

                        var result = _mapper.Map<TransactionAdvancedDto>(transactionEntity);
                        result.GuestAccessToken = guestAccessToken;
                        result.GuestAccessExpiresAtUtc = order.GuestAccessExpiresAtUtc;
                        result.PaymentAttemptId = paymentAttempt?.Id;
                        result.MerchantReference = paymentAttempt?.MerchantReference;
                        result.PaymentInitializationStatus = paymentAttempt == null ? PaymentInitializationStatus.NotApplicable : PaymentInitializationStatus.Pending;
                        result.ReservationExpiresAtUtc = order.ReservationExpiresAtUtc;
                        return result;
                    }
                    catch
                    {
                        await _unitOfWork.RollbackTransactionAsync();
                        ecomDb.ChangeTracker.Clear();
                        throw;
                    }
                });
            }
            catch
            {
                ecomDb.ChangeTracker.Clear();
                if (keyHash != null)
                {
                    var committed = await ecomDb.Orders.AsNoTracking().SingleOrDefaultAsync(o => o.CheckoutIdempotencyKeyHash == keyHash);
                    if (committed != null)
                        return await BuildCheckoutResultAsync(committed, idempotencyKey!, requestHash);
                }
                throw;
            }
        }

        public async Task<OrderDto> UpdateOrderStatusAsync(OrderUpdateStatusDto orderUpdateDto)
        {
            if (orderUpdateDto.Status is OrderStatus.Cancelled or OrderStatus.Expired or OrderStatus.RequiresReconciliation)
                throw new InvalidOperationException("Use the centralized cancellation, expiry, or reconciliation workflow for this transition.");
            var order = await _unitOfWork.Orders.GetByIdAsync(orderUpdateDto.Id);
            if (order == null)
                throw new ArgumentException($"Order with ID {orderUpdateDto.Id} not found.");

            if (orderUpdateDto.Status is OrderStatus.Processing or OrderStatus.Shipped or OrderStatus.Delivered)
            {
                var latestTransaction = await _transaction.GetLatestTransactionByOrderAsync(order.Id);
                var hasVerifiedElectronicPayment = latestTransaction?.Status == TransactionStatus.Paid &&
                                                   latestTransaction.GatewayVerifiedAtUtc is not null;
                var isCashOnDelivery = latestTransaction?.PaymentMethod == PaymentMethod.Cod;
                if (!hasVerifiedElectronicPayment && !isCashOnDelivery)
                    throw new InvalidOperationException("The order cannot advance before verified payment or authorized cash-on-delivery handling.");
            }

            order.Status = orderUpdateDto.Status;
            _unitOfWork.Orders.Update(order);
            var db = (EcomDbContext)_unitOfWork.Context;
            db.OutboxMessages.Add(new OutboxMessage
            {
                Type = "OrderStatusChangedEmail",
                DeduplicationKey = $"order:{order.Id}:status:{orderUpdateDto.Status}",
                Payload = JsonSerializer.Serialize(new { OrderId = order.Id }),
                NextAttemptAtUtc = DateTime.UtcNow
            });
            await _unitOfWork.SaveChangesAsync();

            var updatedOrder = await _unitOfWork.Orders.GetOrderWithItemsAsync(orderUpdateDto.Id);
            return _mapper.Map<OrderDto>(updatedOrder);
        }

        public async Task<bool> CancelOrderAsync(int orderId)
        {
            var context = _unitOfWork.Context;
            var strategy = context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(
                state: orderId,
                operation: async (db, state, token) =>
                {
                    await _unitOfWork.BeginTransactionAsync();
                    
                    try
                    {
                        // Use GetOrderWithItemsForUpdateAsync to load with tracking enabled
                        // and without Product navigation properties to avoid tracking conflicts
                        var order = await _unitOfWork.Orders.GetOrderWithItemsForUpdateAsync(state);
                        if (order == null)
                            return false;

                        if (order.Status == OrderStatus.Shipped || order.Status == OrderStatus.Delivered)
                            throw new InvalidOperationException("Cannot cancel shipped or delivered orders.");

                        if (order.Status == OrderStatus.Cancelled)
                        {
                            await _unitOfWork.RollbackTransactionAsync();
                            return true;
                        }

                        var latestTransaction = await _transaction.GetLatestTransactionByOrderAsync(order.Id);
                        if (latestTransaction?.Status == TransactionStatus.Paid || latestTransaction?.Status == TransactionStatus.Collected)
                            throw new InvalidOperationException("A captured or collected payment requires the refund workflow before cancellation.");

                        var revokedAt = DateTime.UtcNow;
                        var claimed = await _unitOfWork.Orders.TryCancelOrderAsync(state, order.Status, revokedAt);
                        if (!claimed)
                        {
                            await _unitOfWork.RollbackTransactionAsync();
                            var current = await _unitOfWork.Orders.GetByIdAsync(state);
                            return current?.Status == OrderStatus.Cancelled;
                        }

                        var ecomDb = (EcomDbContext)_unitOfWork.Context;
                        var reservations = await ecomDb.InventoryReservations
                            .Where(r => r.OrderId == state && (r.Status == ReservationStatus.Active || r.Status == ReservationStatus.Confirmed))
                            .ToListAsync(token);
                        foreach (var reservation in reservations)
                        {
                            var claimedRelease = await ecomDb.InventoryReservations
                                .Where(r => r.Id == reservation.Id && (r.Status == ReservationStatus.Active || r.Status == ReservationStatus.Confirmed))
                                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, ReservationStatus.Released)
                                    .SetProperty(r => r.ReleasedAtUtc, revokedAt), token);
                            if (claimedRelease == 0)
                                continue;
                            var stockIncreased = await _productService.IncreaseProductStockAsync(reservation.ProductId, reservation.Quantity);
                            if (!stockIncreased)
                                throw new InvalidOperationException($"Failed to restore stock for product ID {reservation.ProductId}");
                        }

                        var couponReservation = await ecomDb.CouponReservations
                            .SingleOrDefaultAsync(r => r.OrderId == state && (r.Status == ReservationStatus.Active || r.Status == ReservationStatus.Confirmed), token);
                        if (couponReservation != null)
                        {
                            var claimedCoupon = await ecomDb.CouponReservations
                                .Where(r => r.Id == couponReservation.Id && (r.Status == ReservationStatus.Active || r.Status == ReservationStatus.Confirmed))
                                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, ReservationStatus.Released)
                                    .SetProperty(r => r.ReleasedAtUtc, revokedAt), token);
                            if (claimedCoupon == 1)
                                await _unitOfWork.Coupons.ReleaseReservedUsageAsync(couponReservation.CouponId);
                        }

                        var cancellingAttempts = await ecomDb.PaymentAttempts.AsNoTracking().Where(a => a.OrderId == state &&
                            (a.Status == PaymentAttemptStatus.Dispatching || a.Status == PaymentAttemptStatus.PendingDispatch || a.Status == PaymentAttemptStatus.AwaitingPayment)).ToListAsync(token);
                        await ecomDb.PaymentAttempts.Where(a => a.OrderId == state && a.Status == PaymentAttemptStatus.Dispatching)
                            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, PaymentAttemptStatus.RequiresReconciliation)
                                .SetProperty(a => a.LastErrorCode, "ORDER_CANCELLED_DURING_DISPATCH"), token);
                        await ecomDb.PaymentAttempts.Where(a => a.OrderId == state &&
                                (a.Status == PaymentAttemptStatus.PendingDispatch || a.Status == PaymentAttemptStatus.AwaitingPayment))
                            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, PaymentAttemptStatus.Cancelled), token);
                        foreach (var paymentAttempt in cancellingAttempts)
                        {
                            var target = paymentAttempt.Status == PaymentAttemptStatus.Dispatching
                                ? PaymentAttemptStatus.RequiresReconciliation
                                : PaymentAttemptStatus.Cancelled;
                            ecomDb.PaymentTransitionAudits.Add(new PaymentTransitionAudit
                            {
                                PaymentAttemptId = paymentAttempt.Id, FromStatus = paymentAttempt.Status, ToStatus = target,
                                EventKey = $"attempt:{paymentAttempt.Id}:order-cancelled", ReasonCode = "ORDER_CANCELLED"
                            });
                        }

                        await _unitOfWork.CommitTransactionAsync();
                        return true;
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
            await Task.CompletedTask;
            throw new NotSupportedException("Legacy direct payment processing is disabled. Use the verified payment initiation flow.");
        }

        public async Task<IEnumerable<TransactionDto>> GetOrderTransactionsAsync(int orderId)
        {
            var transactions = await _unitOfWork.Transactions.GetTransactionsByOrderAsync(orderId);
            return _mapper.Map<IEnumerable<TransactionDto>>(transactions);
        }

        public async Task<string> GetOrderPaymentLinkAsync(int orderId)
        {
            var status = await GetOrderPaymentStatusAsync(orderId);
            if (status.InitializationStatus == PaymentInitializationStatus.NotApplicable)
                throw new InvalidOperationException("Cash-on-delivery orders do not have an online payment link.");
            if (string.IsNullOrWhiteSpace(status.PaymentUrl))
                throw new InvalidOperationException($"Payment initialization is {status.InitializationStatus}.");
            return status.PaymentUrl;
        }

        public async Task<PaymentStatusDto> GetOrderPaymentStatusAsync(int orderId)
        {
            var db = (EcomDbContext)_unitOfWork.Context;
            var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(o => o.Id == orderId)
                ?? throw new ArgumentException($"Order with ID {orderId} not found.");
            var transaction = await db.Transactions.AsNoTracking().Where(t => t.OrderId == orderId)
                .OrderByDescending(t => t.TransactionDate).FirstOrDefaultAsync();
            if (transaction?.PaymentMethod == PaymentMethod.Cod)
                return new PaymentStatusDto { OrderId = orderId, InitializationStatus = PaymentInitializationStatus.NotApplicable, ReservationExpiresAtUtc = order.ReservationExpiresAtUtc };
            var attempt = await db.PaymentAttempts.AsNoTracking().Where(a => a.OrderId == orderId)
                .OrderByDescending(a => a.CreatedAt).FirstOrDefaultAsync();
            var initialization = attempt?.Status switch
            {
                PaymentAttemptStatus.AwaitingPayment => PaymentInitializationStatus.Ready,
                PaymentAttemptStatus.RequiresReconciliation => PaymentInitializationStatus.RequiresReconciliation,
                PaymentAttemptStatus.Paid => PaymentInitializationStatus.Paid,
                PaymentAttemptStatus.Failed or PaymentAttemptStatus.Cancelled or PaymentAttemptStatus.Expired => PaymentInitializationStatus.Failed,
                _ => PaymentInitializationStatus.Pending
            };
            return new PaymentStatusDto
            {
                OrderId = orderId, PaymentAttemptId = attempt?.Id, MerchantReference = attempt?.MerchantReference,
                AttemptStatus = attempt?.Status, InitializationStatus = initialization,
                PaymentUrl = attempt?.PaymentUrl, ReservationExpiresAtUtc = order.ReservationExpiresAtUtc
            };
        }

        public async Task<PaymentStatusDto> RetryPaymentAsync(int orderId, string idempotencyKey)
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Trim().Length > 200)
                throw new ArgumentException("A valid Idempotency-Key is required for payment retry.");
            idempotencyKey = idempotencyKey.Trim();
            var db = (EcomDbContext)_unitOfWork.Context;
            var keyHash = HashGuestAccessToken($"payment-retry:{idempotencyKey}");
            var existing = await db.PaymentAttempts.AsNoTracking().SingleOrDefaultAsync(a => a.IdempotencyKeyHash == keyHash);
            if (existing != null)
            {
                if (existing.OrderId != orderId) throw new IdempotencyConflictException();
                return await GetOrderPaymentStatusAsync(orderId);
            }

            var strategy = db.Database.CreateExecutionStrategy();
            try
            {
                return await strategy.ExecuteAsync(async () =>
                {
                    await using var tx = await db.Database.BeginTransactionAsync();
                    var order = await db.Orders.SingleOrDefaultAsync(o => o.Id == orderId)
                        ?? throw new ArgumentException($"Order with ID {orderId} not found.");
                    await db.Orders.Where(o => o.Id == orderId)
                        .ExecuteUpdateAsync(s => s.SetProperty(o => o.UpdatedAt, DateTime.UtcNow));
                    if (order.Status != OrderStatus.Pending || order.ReservationExpiresAtUtc <= DateTime.UtcNow ||
                        !await db.InventoryReservations.AnyAsync(r => r.OrderId == orderId && r.Status == ReservationStatus.Active && r.ExpiresAtUtc > DateTime.UtcNow))
                        throw new InvalidOperationException("A payment retry requires a pending order with an active inventory reservation.");
                    if (await db.PaymentAttempts.AnyAsync(a => a.OrderId == orderId &&
                            (a.Status == PaymentAttemptStatus.PendingDispatch || a.Status == PaymentAttemptStatus.Dispatching ||
                             a.Status == PaymentAttemptStatus.AwaitingPayment || a.Status == PaymentAttemptStatus.RequiresReconciliation)))
                        throw new InvalidOperationException("The order already has an active or unreconciled payment attempt.");
                    var previous = await db.Transactions.AsNoTracking().Where(t => t.OrderId == orderId)
                        .OrderByDescending(t => t.TransactionDate).FirstAsync();
                    if (previous.PaymentMethod == PaymentMethod.Cod)
                        throw new InvalidOperationException("Cash-on-delivery orders do not support electronic payment retries.");

                    var transaction = new Transaction
                    {
                        OrderId = orderId, AppUserId = order.AppUserId, Amount = order.Total,
                        PaymentMethod = previous.PaymentMethod, Status = TransactionStatus.Pending,
                        TransactionDate = DateTime.UtcNow, TransactionReference = $"TXN-{Guid.NewGuid():N}".ToUpperInvariant()
                    };
                    db.Transactions.Add(transaction);
                    await db.SaveChangesAsync();
                    var requestHash = HashGuestAccessToken($"{orderId}:{order.Total.ToString(System.Globalization.CultureInfo.InvariantCulture)}:{(int)previous.PaymentMethod}");
                    var attempt = new PaymentAttempt
                    {
                        OrderId = orderId, TransactionId = transaction.Id, IdempotencyKeyHash = keyHash,
                        RequestHash = requestHash, MerchantReference = $"PAY-{Guid.NewGuid():N}".ToUpperInvariant(),
                        Amount = order.Total, Currency = "KWD", Status = PaymentAttemptStatus.PendingDispatch, NextActionAtUtc = DateTime.UtcNow
                    };
                    db.PaymentAttempts.Add(attempt);
                    await db.SaveChangesAsync();
                    db.OutboxMessages.Add(new OutboxMessage
                    {
                        Type = "PaymentInvoiceRequested", DeduplicationKey = $"payment-attempt:{attempt.Id}:dispatch",
                        Payload = JsonSerializer.Serialize(new { PaymentAttemptId = attempt.Id }), NextAttemptAtUtc = DateTime.UtcNow
                    });
                    await db.SaveChangesAsync();
                    await tx.CommitAsync();
                    return new PaymentStatusDto
                    {
                        OrderId = orderId, PaymentAttemptId = attempt.Id, MerchantReference = attempt.MerchantReference,
                        AttemptStatus = attempt.Status, InitializationStatus = PaymentInitializationStatus.Pending,
                        ReservationExpiresAtUtc = order.ReservationExpiresAtUtc
                    };
                });
            }
            catch
            {
                db.ChangeTracker.Clear();
                existing = await db.PaymentAttempts.AsNoTracking().SingleOrDefaultAsync(a => a.IdempotencyKeyHash == keyHash);
                if (existing != null)
                {
                    if (existing.OrderId != orderId) throw new IdempotencyConflictException();
                    return await GetOrderPaymentStatusAsync(orderId);
                }
                throw;
            }
        }

        public async Task<InvoicePaymentDto?> GetInvoicePaymentDataAsync(int orderId)
        {
            var order = await _unitOfWork.Orders.GetOrderWithItemsAsync(orderId);
            if (order == null)
                return null;

            var orderDto = _mapper.Map<OrderDto>(order);

            foreach (var item in orderDto.Items)
            {
                var sourceItem = order.Items.FirstOrDefault(oi => oi.Id == item.Id);
                if (sourceItem == null)
                    continue;

                item.Name = !string.IsNullOrWhiteSpace(sourceItem.Product?.TitleAr)
                    ? sourceItem.Product.TitleAr
                    : sourceItem.Name;
            }
            
            var invoicePaymentDto = new InvoicePaymentDto
            {
                // Order Information
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                Status = order.Status,
                OrderCreatedAt = order.CreatedAt,
                
                // Financial Information
                Subtotal = order.Subtotal,
                Shipping = order.Shipping,
                Tax = order.Tax,
                Total = order.Total,
                PaymentAmount = order.Total,
                
                // Customer Information
                AppUserId = order.AppUserId,
                CustomerName = orderDto.CustomerName,
                CustomerEmail = order.AppUser?.Email,
                CustomerPhone = order.ShippingAddress?.Phone,
                
                // Coupon Information
                CouponId = order.CouponId,
                CouponCode = orderDto.CouponCode,
                CouponDiscountAmount = order.CouponDiscountAmount,
                
                // Order Items
                Items = orderDto.Items,
                
                // Shipping Address
                ShippingAddress = orderDto.ShippingAddress,
                
                // Success indicator
                Success = true,
                Message = "تم استرجاع بيانات دفع الفاتورة بنجاح"
            };

            return invoicePaymentDto;
        }

        private async Task<string> GenerateOrderNumberAsync()
        {
            var dateKey = int.Parse(DateTime.UtcNow.ToString("yyyyMMdd"), System.Globalization.CultureInfo.InvariantCulture);
            var context = (EcomDbContext)_unitOfWork.Context;
            var connection = context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                INSERT INTO "OrderNumberCounters" ("DateKey", "NextValue") VALUES (@dateKey, 1)
                ON CONFLICT ("DateKey") DO UPDATE
                SET "NextValue" = "OrderNumberCounters"."NextValue" + 1
                RETURNING "NextValue";
                """;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@dateKey";
            parameter.Value = dateKey;
            command.Parameters.Add(parameter);
            var next = Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
            return $"ORD-{dateKey}-{next:D4}";
        }

        private async Task<TransactionAdvancedDto> BuildCheckoutResultAsync(Order order, string idempotencyKey, string requestHash)
        {
            if (!string.Equals(order.CheckoutRequestHash, requestHash, StringComparison.Ordinal))
                throw new IdempotencyConflictException();
            var db = (EcomDbContext)_unitOfWork.Context;
            var transaction = await db.Transactions.Include(t => t.Order).ThenInclude(o => o.ShippingAddress)
                .AsNoTracking().Where(t => t.OrderId == order.Id).OrderBy(t => t.TransactionDate).FirstAsync();
            var attempt = await db.PaymentAttempts.AsNoTracking().Where(a => a.OrderId == order.Id).OrderBy(a => a.CreatedAt).FirstOrDefaultAsync();
            var result = _mapper.Map<TransactionAdvancedDto>(transaction);
            if (string.IsNullOrWhiteSpace(order.AppUserId) && !string.IsNullOrWhiteSpace(order.GuestTokenDerivationNonce))
                result.GuestAccessToken = DeriveGuestAccessToken(idempotencyKey, order.GuestTokenDerivationNonce);
            result.GuestAccessExpiresAtUtc = order.GuestAccessExpiresAtUtc;
            result.PaymentAttemptId = attempt?.Id;
            result.MerchantReference = attempt?.MerchantReference;
            result.PaymentUrl = attempt?.PaymentUrl;
            result.PaymentInitializationStatus = attempt == null ? PaymentInitializationStatus.NotApplicable : attempt.Status switch
            {
                PaymentAttemptStatus.AwaitingPayment => PaymentInitializationStatus.Ready,
                PaymentAttemptStatus.RequiresReconciliation => PaymentInitializationStatus.RequiresReconciliation,
                PaymentAttemptStatus.Paid => PaymentInitializationStatus.Paid,
                PaymentAttemptStatus.Failed => PaymentInitializationStatus.Failed,
                _ => PaymentInitializationStatus.Pending
            };
            result.ReservationExpiresAtUtc = order.ReservationExpiresAtUtc;
            return result;
        }

        private static string ComputeCheckoutRequestHash(OrderCreateDto dto, string? userId)
        {
            var canonical = new
            {
                User = userId ?? "guest",
                Method = (int)dto.PaymentMethod,
                Coupon = dto.CouponCode?.Trim().ToUpperInvariant(),
                Items = dto.Items.GroupBy(i => i.ProductId).Select(g => new { ProductId = g.Key, Quantity = g.Sum(i => i.Quantity) }).OrderBy(i => i.ProductId),
                Shipping = dto.ShippingAddress
            };
            return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(canonical)));
        }

        private string DeriveGuestAccessToken(string idempotencyKey, string nonce)
        {
            var secret = _configuration?["CheckoutSecurity:GuestTokenDerivationKey"] ?? _configuration?["JwtSettings:SecretKey"];
            if (string.IsNullOrWhiteSpace(secret) || Encoding.UTF8.GetByteCount(secret) < 32)
                throw new InvalidOperationException("A stable 32-byte CheckoutSecurity:GuestTokenDerivationKey is required for idempotent guest checkout.");
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{idempotencyKey}:{nonce}"));
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        public async Task<bool> CanAccessOrderAsync(int orderId, string? userId, string? guestAccessToken, bool isAdmin = false)
        {
            if (isAdmin)
                return true;

            var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
            if (order == null)
                return false;

            if (!string.IsNullOrWhiteSpace(order.AppUserId))
                return !string.IsNullOrWhiteSpace(userId) &&
                       string.Equals(order.AppUserId, userId, StringComparison.Ordinal);

            if (string.IsNullOrWhiteSpace(guestAccessToken) ||
                string.IsNullOrWhiteSpace(order.GuestAccessTokenHash) ||
                order.GuestAccessExpiresAtUtc is null ||
                order.GuestAccessExpiresAtUtc <= DateTime.UtcNow ||
                order.GuestAccessRevokedAtUtc is not null)
                return false;

            var suppliedHash = Encoding.ASCII.GetBytes(HashGuestAccessToken(guestAccessToken));
            var storedHash = Encoding.ASCII.GetBytes(order.GuestAccessTokenHash);
            return suppliedHash.Length == storedHash.Length &&
                   CryptographicOperations.FixedTimeEquals(suppliedHash, storedHash);
        }

        public async Task<string?> RotateGuestAccessTokenAsync(int orderId)
        {
            var order = await _unitOfWork.Orders.FirstOrDefaultAsync(candidate => candidate.Id == orderId);
            if (order == null || !string.IsNullOrWhiteSpace(order.AppUserId) || order.GuestAccessRevokedAtUtc is not null)
                return null;

            var token = CreateGuestAccessToken();
            order.GuestAccessTokenHash = HashGuestAccessToken(token);
            order.GuestAccessExpiresAtUtc = DateTime.UtcNow.Add(GuestAccessLifetime);
            _unitOfWork.Orders.Update(order);
            await _unitOfWork.SaveChangesAsync();
            return token;
        }

        private static string CreateGuestAccessToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static string HashGuestAccessToken(string token) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

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


