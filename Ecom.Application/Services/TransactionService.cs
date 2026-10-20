using AutoMapper;
using Ecom.Application.DTOs.Common;
using Ecom.Application.DTOs.Order;
using Ecom.Application.Services.Interfaces;
using Ecom.Domain.constant;
using Ecom.Domain.Entity;
using Ecom.Infrastructure.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Ecom.Application.Services
{
    internal class SadadRefreshResponse
    {
        public bool isValid { get; set; }
        public string? errorKey { get; set; }
        public SadadRefreshData? response { get; set; }
    }

    internal class SadadAccessResponse
    {
        public bool isValid { get; set; }
        public string? errorKey { get; set; }
        public SadadAccessData? response { get; set; }
    }

    internal class SadadRefreshData
    {
        public string refreshToken { get; set; } = string.Empty;
    }

    internal class SadadAccessData
    {
        public string accessToken { get; set; } = string.Empty;
        public int expiredAfterSeconds { get; set; }
    }

    internal class SadadInvoiceInsertResponse
    {
        public bool isValid { get; set; }
        public string? errorKey { get; set; }
        public SadadInvoiceInsertData? response { get; set; }
    }

    internal class SadadInvoiceInsertData
    {
        public string? invoiceId { get; set; }
    }

    internal class SadadInvoiceGetByIdResponse
    {
        public bool isValid { get; set; }
        public string? errorKey { get; set; }
        public SadadInvoiceGetByIdData? response { get; set; }
    }

    internal class SadadInvoiceGetByIdData
    {
        public string? status { get; set; }
        public string? url { get; set; }
    }

    public class TransactionService : ITransactionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TransactionService> _logger;
        private readonly IConfiguration? _configuration;

        public TransactionService(IUnitOfWork unitOfWork, IMapper mapper, IHttpClientFactory httpClientFactory, ILogger<TransactionService> logger, IConfiguration? configuration = null)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _configuration = configuration;
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

            if (filter.Status.HasValue)
                query = query.Where(t => t.Status == filter.Status.Value);

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
            if (!Enum.IsDefined(transactionDto.PaymentMethod))
                throw new ArgumentException("Invalid payment method.");

            var transaction = _mapper.Map<Transaction>(transactionDto);
            transaction.AppUserId = string.IsNullOrWhiteSpace(transaction.AppUserId) ? null : transaction.AppUserId;
            transaction.TransactionDate = DateTime.UtcNow;
            transaction.TransactionReference = transactionDto.TransactionReference ?? GenerateTransactionReference();

            var order = await _unitOfWork.Orders.GetOrderWithItemsAsync(transactionDto.OrderId);
            if (order == null)
                throw new ArgumentException($"Order with ID {transactionDto.OrderId} not found.");

            if (order.Total <= 0)
                throw new InvalidOperationException("The order total must be positive before payment can be initiated.");

            // Financial values and state are server-owned. Callers may request a
            // payment method, but cannot choose the amount or create a Paid row.
            transaction.Amount = order.Total;
            transaction.Status = TransactionStatus.Pending;
            transaction.GatewayVerifiedAtUtc = null;

            if (transaction.PaymentMethod == PaymentMethod.Cod)
            {
                transaction.Status = TransactionStatus.Pending;
                transaction.GatewayInvoiceId = null;
                transaction.PaymentGatewayResponse = null;
                transaction.ProcessedDate = null;
                await _unitOfWork.Transactions.AddAsync(transaction);
                await _unitOfWork.SaveChangesAsync();
                var storedTransaction = await _unitOfWork.Transactions.GetTransactionWithDetailsAsync(transaction.Id);
                var cashOnDelivery = _mapper.Map<TransactionAdvancedDto>(storedTransaction ?? transaction);
                cashOnDelivery.PaymentUrl = null;
                return cashOnDelivery;
            }

            var customerName = order.AppUser?.UserName ?? order.ShippingAddress?.FullName ?? "Guest";
            var customerEmail = order.AppUser?.Email ?? "guest@example.com";
            var customerMobile = order.ShippingAddress?.Phone ?? "0000000";

            var hasCoupon = order.CouponId.HasValue && order.CouponDiscountAmount.HasValue && order.CouponDiscountAmount > 0;
            if (hasCoupon && order.Coupon == null)
                throw new InvalidOperationException("The order discount cannot be verified for payment initiation.");

            var tokens = await GenerateTokens();

            // Sadad expects item.amount = price per unit (not total). Total per item = amount * quantity.
            var invoiceItems = new List<Dictionary<string, object>>();
            if (order.Items != null)
            {
                foreach (var item in order.Items)
                {
                    invoiceItems.Add(new Dictionary<string, object>
                    {
                        ["name"] = item.Name ?? "Item",
                        ["quantity"] = item.Quantity,
                        ["amount"] = decimal.Round(item.Price, 3, MidpointRounding.AwayFromZero)
                    });
                }
            }

            // Add shipping and tax as line items so invoice amount equals sum of all items (Sadad validation)
            var itemsTotal = (order.Items?.Sum(i => i.Price * i.Quantity) ?? 0) + order.Shipping + order.Tax;
            if (order.Shipping > 0)
            {
                invoiceItems.Add(new Dictionary<string, object> { ["name"] = "Shipping", ["quantity"] = 1, ["amount"] = decimal.Round(order.Shipping, 3, MidpointRounding.AwayFromZero) });
            }
            if (order.Tax > 0)
            {
                invoiceItems.Add(new Dictionary<string, object> { ["name"] = "Tax", ["quantity"] = 1, ["amount"] = decimal.Round(order.Tax, 3, MidpointRounding.AwayFromZero) });
            }

            // Sadad requires: invoice amount = sum of (item.amount * item.quantity). With discount, charge = amount - discount.
            var invoiceAmount = itemsTotal;

            var invoice = new Dictionary<string, object>
            {
                ["ref_Number"] = order.Id.ToString(),
                ["amount"] = decimal.Round(invoiceAmount, 3, MidpointRounding.AwayFromZero),
                ["customer_Name"] = customerName,
                ["customer_Mobile"] = customerMobile,
                ["customer_Email"] = customerEmail,
                ["lang"] = "ar",
                ["currency_Code"] = "KWD"
            };

            if (invoiceItems.Count > 0)
                invoice["items"] = invoiceItems;

            if (hasCoupon && order.Coupon != null)
            {
                invoice["discount_Type"] = MapCouponTypeToSadad(order.Coupon.Type);
                invoice["discount_Amount"] = order.CouponDiscountAmount!.Value;
                invoice["discount_Amount_Total"] = order.CouponDiscountAmount.Value;
                invoice["transactionID"] = transaction.TransactionReference;
            }

            var invoicePayload = new { Invoices = new[] { invoice } };

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri("api/Invoice/insert", UriKind.Relative),
                Headers = { { "authorization", $"Bearer {tokens.AccessToken}" } },
                Content = new StringContent(
                    JsonSerializer.Serialize(invoicePayload, new JsonSerializerOptions { PropertyNamingPolicy = null }),
                    Encoding.UTF8,
                    "application/json")
            };

            var httpClient = _httpClientFactory.CreateClient("Sadad");
            using var response = await httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Sadad invoice creation failed with HTTP status {StatusCode}.", (int)response.StatusCode);
                throw new InvalidOperationException("Payment gateway invoice creation failed.");
            }

            var insertResponse = JsonSerializer.Deserialize<SadadInvoiceInsertResponse>(body);
            if (insertResponse == null || !insertResponse.isValid)
            {
                _logger.LogWarning("Sadad returned an invalid invoice creation response.");
                throw new InvalidOperationException("Payment gateway invoice creation failed.");
            }

            if (string.IsNullOrWhiteSpace(insertResponse.response?.invoiceId))
                throw new InvalidOperationException("Payment gateway did not return an invoice identifier.");

            transaction.GatewayInvoiceId = insertResponse.response.invoiceId;
            transaction.PaymentGatewayResponse = "InvoiceCreated";

            transaction.Status = TransactionStatus.Pending;

            await _unitOfWork.Transactions.AddAsync(transaction);
            await _unitOfWork.SaveChangesAsync();
            var createdTransaction = await _unitOfWork.Transactions.GetTransactionWithDetailsAsync(transaction.Id);

            var result = _mapper.Map<TransactionAdvancedDto>(createdTransaction);

            // Fetch payment URL from Sadad after invoice creation
            if (!string.IsNullOrEmpty(transaction.GatewayInvoiceId))
            {
                result.PaymentUrl = await GetSadadPaymentUrlAsync(transaction.GatewayInvoiceId, tokens.AccessToken);
            }

            return result;
        }

        public async Task<string?> GetPaymentUrlByTransactionIdAsync(int transactionId)
        {
            var transaction = await _unitOfWork.Transactions.GetByIdAsync(transactionId);
            if (transaction == null || transaction.PaymentMethod == PaymentMethod.Cod || string.IsNullOrEmpty(transaction.GatewayInvoiceId))
                return null;

            var tokens = await GenerateTokens();
            return await GetSadadPaymentUrlAsync(transaction.GatewayInvoiceId, tokens.AccessToken);
        }

        private async Task<string?> GetSadadPaymentUrlAsync(string invoiceId, string accessToken)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri($"api/Invoice/getbyid?id={invoiceId}", UriKind.Relative),
                Headers = { { "authorization", $"Bearer {accessToken}" } },
            };

            var httpClient = _httpClientFactory.CreateClient("Sadad");
            using var response = await httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return null;

            var getByIdResponse = JsonSerializer.Deserialize<SadadInvoiceGetByIdResponse>(body);
            if (getByIdResponse == null || !getByIdResponse.isValid || getByIdResponse.response == null)
                return null;

            return string.IsNullOrEmpty(getByIdResponse.response.url) ? null : getByIdResponse.response.url.Trim();
        }

        public async Task<TokenResponse> GenerateTokens(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Prefer the Sadad section, while preserving existing flat environment settings.
            var clientKey = _configuration?["Sadad:ClientKey"];
            var clientSecret = _configuration?["Sadad:ClientSecret"];
            if (clientKey == null && clientSecret == null)
            {
                clientKey = _configuration?["ClientKey"] ?? Environment.GetEnvironmentVariable("ClientKey");
                clientSecret = _configuration?["ClientSecret"] ?? Environment.GetEnvironmentVariable("ClientSecret");
            }

            if (string.IsNullOrWhiteSpace(clientKey) || string.IsNullOrWhiteSpace(clientSecret))
                throw AuthenticationFailure("SADAD_CREDENTIALS_MISSING");

            var httpClient = _httpClientFactory.CreateClient("Sadad");

            // =========================
            // 1️⃣ Generate Refresh Token (Basic Auth)
            // =========================

            var authValue = Convert.ToBase64String(
                Encoding.ASCII.GetBytes($"{clientKey}:{clientSecret}")
            );

            using var refreshRequest = new HttpRequestMessage(
                HttpMethod.Post,
                "api/User/GenerateRefreshToken"
            );

            refreshRequest.Headers.Authorization =
                new AuthenticationHeaderValue("Basic", authValue);

            var refreshObj = await ReadAuthenticationResponseAsync<SadadRefreshResponse>(httpClient, refreshRequest, "REFRESH", cancellationToken);

            if (refreshObj == null || !refreshObj.isValid || refreshObj.response == null)
                throw AuthenticationFailure("SADAD_REFRESH_REJECTED");

            var refreshToken = refreshObj.response.refreshToken;

            if (string.IsNullOrWhiteSpace(refreshToken))
                throw AuthenticationFailure("SADAD_REFRESH_TOKEN_EMPTY");

            // =========================
            // 2️⃣ Generate Access Token (Body only)
            // =========================

            using var accessRequest = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri("api/User/GenerateAccessToken", UriKind.Relative),
                Headers =
                {
                    { "accept", "application/json" },
                    { "authorization", $"Bearer {refreshToken}" },
                },
            };
            accessRequest.Content = new StringContent(
                JsonSerializer.Serialize(new { refreshToken }),
                Encoding.UTF8,
                "application/json"
            );

            var accessObj = await ReadAuthenticationResponseAsync<SadadAccessResponse>(httpClient, accessRequest, "ACCESS", cancellationToken);

            if (accessObj == null || !accessObj.isValid || accessObj.response == null)
                throw AuthenticationFailure("SADAD_ACCESS_REJECTED");

            if (string.IsNullOrWhiteSpace(accessObj.response.accessToken))
                throw AuthenticationFailure("SADAD_ACCESS_TOKEN_EMPTY");

            return new TokenResponse
            {
                AccessToken = accessObj.response.accessToken,
                RefreshToken = refreshToken,
                ExpiredAfterSeconds = accessObj.response.expiredAfterSeconds
            };
        }

        private async Task<T?> ReadAuthenticationResponseAsync<T>(HttpClient client, HttpRequestMessage request, string stage, CancellationToken cancellationToken)
        {
            try
            {
                using var response = await client.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                    throw AuthenticationFailure($"SADAD_{stage}_HTTP_{(int)response.StatusCode}");

                return JsonSerializer.Deserialize<T>(await response.Content.ReadAsStringAsync(cancellationToken));
            }
            catch (JsonException)
            {
                throw AuthenticationFailure($"SADAD_{stage}_INVALID_RESPONSE");
            }
            catch (HttpRequestException)
            {
                throw AuthenticationFailure($"SADAD_{stage}_NETWORK_ERROR");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw AuthenticationFailure($"SADAD_{stage}_TIMEOUT");
            }
        }

        private InvalidOperationException AuthenticationFailure(string reasonCode)
        {
            // Only fixed diagnostic codes go to monitoring; never log credentials or token responses.
            _logger.LogWarning("Sadad authentication failed. ReasonCode={ReasonCode}", reasonCode);
            return new InvalidOperationException($"Payment gateway authentication failed ({reasonCode}).");
        }




        public async Task<TransactionAdvancedDto> UpdateTransactionAsync(TransactionUpdateDto transactionDto)
        {
            var transaction = await _unitOfWork.Transactions.GetByIdAsync(transactionDto.Id);
            if (transaction == null)
                throw new ArgumentException($"Transaction with ID {transactionDto.Id} not found.");

            EnsureAdministrativeStatusTransitionAllowed(transaction, transactionDto.Status);

            transaction.Status = transactionDto.Status;
            transaction.TransactionReference = transactionDto.TransactionReference ?? transaction.TransactionReference;
            transaction.PaymentGatewayResponse = transactionDto.PaymentGatewayResponse ?? transaction.PaymentGatewayResponse;
            transaction.Notes = transactionDto.Notes ?? transaction.Notes;
            transaction.GatewayInvoiceId = transactionDto.GatewayInvoiceId ?? transaction.GatewayInvoiceId;

            if (transactionDto.Status == TransactionStatus.Collected)
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
            if (!Enum.IsDefined(paymentDto.PaymentMethod))
                throw new ArgumentException("Invalid payment method.");

            // Validate order exists and get amount
            var order = await _unitOfWork.Orders.GetByIdAsync(paymentDto.OrderId);
            if (order == null)
                throw new ArgumentException($"Order with ID {paymentDto.OrderId} not found.");

            // Check if order already has successful transaction
            if (await HasSuccessfulTransactionAsync(paymentDto.OrderId))
                throw new InvalidOperationException("Order already has a successful transaction.");

            if (paymentDto.PaymentMethod == PaymentMethod.Cod)
            {
                return await CreateTransactionAsync(new TransactionCreateAdvancedDto
                {
                    OrderId = order.Id,
                    Amount = order.Total,
                    AppUserId = order.AppUserId,
                    PaymentMethod = PaymentMethod.Cod,
                    Notes = paymentDto.Notes
                });
            }

            return await CreateTransactionAsync(new TransactionCreateAdvancedDto
            {
                OrderId = paymentDto.OrderId,
                Amount = order.Total,
                PaymentMethod = paymentDto.PaymentMethod,
                Status = TransactionStatus.Pending,
                TransactionReference = GenerateTransactionReference(),
                Notes = paymentDto.Notes
            });
        }

        public async Task<TransactionAdvancedDto> ProcessRefundAsync(TransactionRefundDto refundDto)
        {
            var transaction = await _unitOfWork.Transactions.GetByIdAsync(refundDto.TransactionId);
            if (transaction == null)
                throw new ArgumentException($"Transaction with ID {refundDto.TransactionId} not found.");

            if (transaction.Status != TransactionStatus.Paid)
                throw new InvalidOperationException("Only paid transactions can be refunded.");

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

        public async Task<bool> UpdateTransactionStatusAsync(int transactionId, TransactionStatus status, string? gatewayResponse = null)
        {
            var transaction = await _unitOfWork.Transactions.GetByIdAsync(transactionId);
            if (transaction == null)
                return false;

            EnsureAdministrativeStatusTransitionAllowed(transaction, status);

            transaction.Status = status;
            if (!string.IsNullOrEmpty(gatewayResponse))
                transaction.PaymentGatewayResponse = gatewayResponse;

            if (status == TransactionStatus.Collected)
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
                SuccessfulTransactions = transactions.Count(t => t.Status == TransactionStatus.Paid),
                FailedTransactions = transactions.Count(t => t.Status == TransactionStatus.Failed),
                PendingTransactions = transactions.Count(t => t.Status == TransactionStatus.Pending),
                RefundedAmount = transactions.Where(t => t.IsRefunded).Sum(t => t.RefundAmount ?? 0),
                NetAmount = transactions.Sum(t => t.Amount) - transactions.Where(t => t.IsRefunded).Sum(t => t.RefundAmount ?? 0)
            };

            // Group by status
            analytics.TransactionsByStatus = transactions
                .GroupBy(t => t.Status)
                .ToDictionary(g => g.Key.ToString(), g => g.Count());

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
                SuccessfulTransactions = transactionList.Count(t => t.Status == TransactionStatus.Paid),
                FailedTransactions = transactionList.Count(t => t.Status == TransactionStatus.Failed),
                PendingTransactions = transactionList.Count(t => t.Status == TransactionStatus.Pending),
                RefundedAmount = transactionList.Where(t => t.IsRefunded).Sum(t => t.RefundAmount ?? 0),
                NetAmount = transactionList.Sum(t => t.Amount) - transactionList.Where(t => t.IsRefunded).Sum(t => t.RefundAmount ?? 0)
            };

            return analytics;
        }

        public async Task<Dictionary<PaymentMethod, decimal>> GetRevenueByPaymentMethodAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _unitOfWork.Transactions.GetTransactionsQuery()
                .Where(t => t.Status == TransactionStatus.Paid);

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
            var transaction = await _unitOfWork.Transactions.GetByIdAsync(transactionId);
            if (transaction == null)
                return false;
            if (transaction.PaymentMethod != PaymentMethod.Cod)
                throw new InvalidOperationException("Electronic payments can only be completed by verified gateway confirmation.");

            transaction.Status = TransactionStatus.Collected;
            transaction.ProcessedDate = DateTime.UtcNow;
            transaction.TransactionReference = reference ?? transaction.TransactionReference;
            _unitOfWork.Transactions.Update(transaction);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> MarkTransactionAsFailedAsync(int transactionId, string reason)
        {
            return await UpdateTransactionStatusAsync(transactionId, TransactionStatus.Failed, reason);
        }

        public async Task<bool> MarkTransactionAsPendingAsync(int transactionId)
        {
            return await UpdateTransactionStatusAsync(transactionId, TransactionStatus.Pending);
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
                   transaction.Status == TransactionStatus.Paid && 
                   !transaction.IsRefunded;
        }

        public async Task<List<TransactionSummaryDto>> SearchTransactionsAsync(string searchTerm, int limit = 50)
        {
            var transactions = await _unitOfWork.Transactions.SearchTransactionsAsync(searchTerm, limit);
            return _mapper.Map<List<TransactionSummaryDto>>(transactions);
        }

        public async Task<List<TransactionAdvancedDto>> GetTransactionsByStatusAsync(TransactionStatus status, int pageNumber = 1, int pageSize = 20)
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
                csv.AppendLine($"{transaction.Id},{transaction.OrderNumber},{transaction.CustomerName},{transaction.Amount},{transaction.PaymentMethodName},{transaction.Status.ToString()},{transaction.TransactionDate:yyyy-MM-dd HH:mm:ss},{transaction.IsRefunded}");
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

        private static string MapCouponTypeToSadad(CouponType couponType)
        {
            return couponType switch
            {
                CouponType.Percentage => "Percentage",
                CouponType.FixedAmount => "Fixed",
                CouponType.FreeShipping => "FreeShipping",
                _ => "Fixed"
            };
        }

        private static void EnsureAdministrativeStatusTransitionAllowed(Transaction transaction, TransactionStatus status)
        {
            if (status == TransactionStatus.Paid)
                throw new InvalidOperationException("Paid status requires verified gateway confirmation.");
            if (status == TransactionStatus.Collected && transaction.PaymentMethod != PaymentMethod.Cod)
                throw new InvalidOperationException("Collected status is only valid for cash-on-delivery transactions.");
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
