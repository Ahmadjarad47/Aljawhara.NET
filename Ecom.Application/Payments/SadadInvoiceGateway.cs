using System.Net;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ecom.Application.Services.Interfaces;
using Ecom.Application.Services;
using Ecom.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Ecom.Application.Payments;

public sealed class SadadInvoiceGateway(
    EcomDbContext db,
    ITransactionService transactions,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<SadadInvoiceGateway> logger) : ISadadInvoiceGateway
{
    private sealed class InsertEnvelope { public bool isValid { get; set; } public InsertData? response { get; set; } }
    private sealed class InsertData { public string? invoiceId { get; set; } }
    private sealed class InvoiceEnvelope { public bool isValid { get; set; } public InvoiceData? response { get; set; } }
    private sealed class InvoiceData { public string? url { get; set; } }
    private sealed record OrderAmounts(decimal Gross, decimal Discount);

    public async Task<GatewayDispatchResult> CreateInvoiceAsync(Domain.Entity.PaymentAttempt attempt, CancellationToken cancellationToken)
    {
        var order = await db.Orders.Include(o => o.Items).Include(o => o.ShippingAddress).Include(o => o.AppUser)
            .Include(o => o.Coupon).AsNoTracking().SingleAsync(o => o.Id == attempt.OrderId, cancellationToken);
        TokenResponse tokens;
        try { tokens = await transactions.GenerateTokens(cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Sadad authentication failed before invoice dispatch for PaymentAttemptId={PaymentAttemptId}.", attempt.Id);
            return new(GatewayDispatchOutcome.RetryableBeforeSend, ErrorCode: "AUTH_BEFORE_SEND");
        }

        var items = order.Items.Select(i => new Dictionary<string, object>
        {
            ["name"] = i.Name, ["quantity"] = i.Quantity, ["amount"] = KwdMoney.Round(i.Price)
        }).ToList();
        if (order.Shipping > 0) items.Add(new() { ["name"] = "Shipping", ["quantity"] = 1, ["amount"] = KwdMoney.Round(order.Shipping) });
        if (order.Tax > 0) items.Add(new() { ["name"] = "Tax", ["quantity"] = 1, ["amount"] = KwdMoney.Round(order.Tax) });
        var invoice = new Dictionary<string, object>
        {
            ["ref_Number"] = attempt.MerchantReference,
            ["amount"] = KwdMoney.Round(order.Subtotal + order.Shipping + order.Tax),
            ["customer_Name"] = order.AppUser?.UserName ?? order.ShippingAddress.FullName,
            ["customer_Mobile"] = order.ShippingAddress.Phone,
            ["customer_Email"] = order.AppUser?.Email ?? "guest@example.com",
            ["lang"] = "ar", ["currency_Code"] = "KWD", ["items"] = items,
            ["transactionID"] = attempt.MerchantReference
        };
        if (order.CouponDiscountAmount is > 0)
        {
            invoice["discount_Amount"] = KwdMoney.Round(order.CouponDiscountAmount.Value);
            invoice["discount_Amount_Total"] = KwdMoney.Round(order.CouponDiscountAmount.Value);
            invoice["discount_Type"] = order.Coupon?.Type.ToString() ?? "FixedAmount";
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/Invoice/insert");
            request.Headers.TryAddWithoutValidation("authorization", $"Bearer {tokens.AccessToken}");
            request.Content = new StringContent(JsonSerializer.Serialize(new { Invoices = new[] { invoice } }), Encoding.UTF8, "application/json");
            using var response = await httpClientFactory.CreateClient("Sadad").SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if ((int)response.StatusCode >= 500)
                return new(GatewayDispatchOutcome.Unknown, ErrorCode: $"HTTP_{(int)response.StatusCode}");
            if (!response.IsSuccessStatusCode)
                return new(GatewayDispatchOutcome.DefinitiveFailure, ErrorCode: $"HTTP_{(int)response.StatusCode}");
            var inserted = JsonSerializer.Deserialize<InsertEnvelope>(body);
            if (inserted?.isValid != true || string.IsNullOrWhiteSpace(inserted.response?.invoiceId))
                return new(GatewayDispatchOutcome.Unknown, ErrorCode: "UNRECOGNIZED_SUCCESS_RESPONSE");

            string? url = null;
            using var get = new HttpRequestMessage(HttpMethod.Get, $"api/Invoice/getbyid?id={Uri.EscapeDataString(inserted.response.invoiceId)}");
            get.Headers.TryAddWithoutValidation("authorization", $"Bearer {tokens.AccessToken}");
            using var getResponse = await httpClientFactory.CreateClient("Sadad").SendAsync(get, cancellationToken);
            if (getResponse.IsSuccessStatusCode)
            {
                var envelope = JsonSerializer.Deserialize<InvoiceEnvelope>(await getResponse.Content.ReadAsStringAsync(cancellationToken));
                if (envelope?.isValid == true) url = envelope.response?.url?.Trim();
            }
            return new(GatewayDispatchOutcome.Created, inserted.response.invoiceId, url);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(GatewayDispatchOutcome.Unknown, ErrorCode: "TIMEOUT_UNKNOWN_OUTCOME");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Sadad invoice outcome is unknown for PaymentAttemptId={PaymentAttemptId}.", attempt.Id);
            return new(GatewayDispatchOutcome.Unknown, ErrorCode: "NETWORK_UNKNOWN_OUTCOME");
        }
    }

    public async Task<SadadInvoiceVerificationResult> VerifyInvoiceAsync(
        Domain.Entity.PaymentAttempt attempt,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(attempt.GatewayInvoiceId))
            return new(SadadInvoiceVerificationOutcome.Invalid, "MISSING_EXPECTED_INVOICE_ID");

        if (!long.TryParse(configuration["Sadad:MerchantVendorId"], NumberStyles.None, CultureInfo.InvariantCulture, out var expectedVendorId) || expectedVendorId <= 0)
            return new(SadadInvoiceVerificationOutcome.Unsupported, "MERCHANT_VENDOR_ID_NOT_CONFIGURED");

        var orderAmounts = await db.Orders.AsNoTracking()
            .Where(o => o.Id == attempt.OrderId)
            .Select(o => new OrderAmounts(o.Subtotal + o.Shipping + o.Tax, o.CouponDiscountAmount ?? 0m))
            .SingleAsync(cancellationToken);
        if (orderAmounts.Discount > 0m && !configuration.GetValue<bool>("Sadad:Verification:DiscountSemanticsCertified"))
            return new(SadadInvoiceVerificationOutcome.Unsupported, "DISCOUNT_SEMANTICS_NOT_CERTIFIED");

        try
        {
            var tokens = await transactions.GenerateTokens(cancellationToken);
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"api/Invoice/getbyid?id={Uri.EscapeDataString(attempt.GatewayInvoiceId)}");
            request.Headers.TryAddWithoutValidation("authorization", $"Bearer {tokens.AccessToken}");
            using var response = await httpClientFactory.CreateClient("Sadad").SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new(SadadInvoiceVerificationOutcome.RequiresReconciliation, $"GETBYID_HTTP_{(int)response.StatusCode}");

            return ParseVerificationResponse(body, attempt, expectedVendorId, orderAmounts);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(SadadInvoiceVerificationOutcome.RequiresReconciliation, "GETBYID_TIMEOUT");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Sadad GetById failed for PaymentAttemptId={PaymentAttemptId}.", attempt.Id);
            return new(SadadInvoiceVerificationOutcome.RequiresReconciliation, "GETBYID_NETWORK_ERROR");
        }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Sadad GetById response was invalid for PaymentAttemptId={PaymentAttemptId}.", attempt.Id);
            return new(SadadInvoiceVerificationOutcome.Invalid, "GETBYID_MALFORMED_RESPONSE");
        }
    }

    public async Task<SadadInvoiceLookupResult> FindInvoiceByReferenceAsync(
        Domain.Entity.PaymentAttempt attempt,
        CancellationToken cancellationToken)
    {
        if (!configuration.GetValue<bool>("Sadad:Verification:GetAllContractCertified"))
            return new(SadadInvoiceLookupOutcome.Unsupported, "GETALL_CONTRACT_NOT_CERTIFIED");
        if (!long.TryParse(configuration["Sadad:MerchantVendorId"], NumberStyles.None, CultureInfo.InvariantCulture, out var expectedVendorId) || expectedVendorId <= 0)
            return new(SadadInvoiceLookupOutcome.Unsupported, "MERCHANT_VENDOR_ID_NOT_CONFIGURED");

        try
        {
            var tokens = await transactions.GenerateTokens(cancellationToken);
            var uri = $"api/Invoice/getall?Ref_Number={Uri.EscapeDataString(attempt.MerchantReference)}&vendor_Id={expectedVendorId.ToString(CultureInfo.InvariantCulture)}";
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.TryAddWithoutValidation("authorization", $"Bearer {tokens.AccessToken}");
            using var response = await httpClientFactory.CreateClient("Sadad").SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new(SadadInvoiceLookupOutcome.Unavailable, $"GETALL_HTTP_{(int)response.StatusCode}");

            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (!TryRequiredBoolean(root, "isValid", out var valid) || !valid ||
                !root.TryGetProperty("response", out var responseElement) || responseElement.ValueKind != JsonValueKind.Array)
                return new(SadadInvoiceLookupOutcome.Unavailable, "GETALL_MALFORMED_RESPONSE");

            var matches = new List<string>();
            foreach (var item in responseElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object ||
                    !TryRequiredIdentifier(item, "id", out var invoiceId) ||
                    !TryRequiredInt64(item, "vendor_Id", out var vendorId) ||
                    !TryRequiredString(item, "ref_Number", out var reference))
                    return new(SadadInvoiceLookupOutcome.Unavailable, "GETALL_INCOMPLETE_ITEM");
                if (vendorId == expectedVendorId && string.Equals(reference, attempt.MerchantReference, StringComparison.Ordinal))
                    matches.Add(invoiceId);
            }

            return matches.Count switch
            {
                0 => new(SadadInvoiceLookupOutcome.NotFound, "GETALL_NO_EXACT_MATCH"),
                1 => new(SadadInvoiceLookupOutcome.FoundUnique, "GETALL_ONE_EXACT_MATCH", matches[0]),
                _ => new(SadadInvoiceLookupOutcome.Ambiguous, "GETALL_MULTIPLE_EXACT_MATCHES")
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(SadadInvoiceLookupOutcome.Unavailable, "GETALL_TIMEOUT");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Sadad GetAll reconciliation failed for PaymentAttemptId={PaymentAttemptId}.", attempt.Id);
            return new(SadadInvoiceLookupOutcome.Unavailable, "GETALL_UNAVAILABLE");
        }
    }

    private SadadInvoiceVerificationResult ParseVerificationResponse(
        string body,
        Domain.Entity.PaymentAttempt attempt,
        long expectedVendorId,
        OrderAmounts orderAmounts)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        if (!TryRequiredBoolean(root, "isValid", out var valid) || !valid ||
            !root.TryGetProperty("response", out var invoice) || invoice.ValueKind != JsonValueKind.Object)
            return new(SadadInvoiceVerificationOutcome.Invalid, "INVALID_ENVELOPE");

        if (!TryRequiredIdentifier(invoice, "id", out var invoiceId) ||
            !TryRequiredInt64(invoice, "vendor_Id", out var vendorId) ||
            !TryRequiredString(invoice, "status", out var status) ||
            !TryRequiredDecimal(invoice, "amount", out var amount) ||
            !TryRequiredDecimal(invoice, "total", out var total) ||
            !TryRequiredDecimal(invoice, "subtotal", out var subtotal) ||
            !TryRequiredDecimal(invoice, "refunded_Amount", out var refundedAmount) ||
            !TryRequiredString(invoice, "ref_Number", out var reference) ||
            !TryRequiredString(invoice, "currency_Code", out var currency))
            return new(SadadInvoiceVerificationOutcome.Invalid, "MISSING_REQUIRED_FIELD");

        if (!string.Equals(invoiceId, attempt.GatewayInvoiceId, StringComparison.Ordinal))
            return new(SadadInvoiceVerificationOutcome.Invalid, "INVOICE_ID_MISMATCH");
        if (vendorId != expectedVendorId)
            return new(SadadInvoiceVerificationOutcome.Invalid, "VENDOR_ID_MISMATCH");
        if (!string.Equals(reference, attempt.MerchantReference, StringComparison.Ordinal))
            return new(SadadInvoiceVerificationOutcome.Invalid, "MERCHANT_REFERENCE_MISMATCH");
        if (!string.Equals(currency, attempt.Currency, StringComparison.Ordinal) || !string.Equals(currency, "KWD", StringComparison.Ordinal))
            return new(SadadInvoiceVerificationOutcome.Invalid, "CURRENCY_MISMATCH");
        // Older zero-fee responses omit fees. A different subtotal is accepted
        // only when an explicit numeric fee reconciles it to the full total.
        var hasFees = invoice.TryGetProperty("fees", out _);
        decimal fees = 0m;
        if (hasFees && !TryRequiredDecimal(invoice, "fees", out fees))
            return new(SadadInvoiceVerificationOutcome.Invalid, "MISSING_REQUIRED_FIELD");

        if (!SadadInvoiceAmounts.HasKwdScale(amount) || !SadadInvoiceAmounts.HasKwdScale(total) ||
            !SadadInvoiceAmounts.HasKwdScale(subtotal) || !SadadInvoiceAmounts.HasKwdScale(refundedAmount) ||
            !SadadInvoiceAmounts.HasKwdScale(fees))
            return new(SadadInvoiceVerificationOutcome.Invalid, "KWD_SCALE_INVALID");
        if (!SadadInvoiceAmounts.MatchesExpected(attempt.Amount, amount, total, subtotal, fees))
        {
            logger.LogWarning(
                "Sadad amount comparison failed for PaymentAttemptId={PaymentAttemptId}. ReasonCode={ReasonCode}. ExpectedAmount={ExpectedAmount}, ExpectedGrossAmount={ExpectedGrossAmount}, ExpectedDiscountAmount={ExpectedDiscountAmount}, GatewayAmount={GatewayAmount}, GatewayTotal={GatewayTotal}, GatewaySubtotal={GatewaySubtotal}, GatewayFees={GatewayFees}",
                attempt.Id, "AMOUNT_FIELDS_MISMATCH", attempt.Amount, orderAmounts.Gross, orderAmounts.Discount,
                amount, total, subtotal, hasFees ? fees : (decimal?)null);
            return new(SadadInvoiceVerificationOutcome.Invalid, "AMOUNT_FIELDS_MISMATCH");
        }
        if (refundedAmount != 0m)
            return new(SadadInvoiceVerificationOutcome.Unsupported, "REFUND_OR_PARTIAL_REFUND_REQUIRES_RECONCILIATION");

        if (string.Equals(status, "Unpaid", StringComparison.OrdinalIgnoreCase))
            return new(SadadInvoiceVerificationOutcome.NotPaid, "INVOICE_UNPAID");
        if (!string.Equals(status, "Paid", StringComparison.OrdinalIgnoreCase))
            return new(SadadInvoiceVerificationOutcome.Invalid, "UNKNOWN_PAYMENT_STATUS");

        if (!TryRequiredIdentifier(invoice, "transactionID", out var transactionId) ||
            !invoice.TryGetProperty("transactions", out var transactionsElement) ||
            transactionsElement.ValueKind != JsonValueKind.Array || transactionsElement.GetArrayLength() == 0)
            return new(SadadInvoiceVerificationOutcome.Invalid, "MISSING_PAYMENT_TRANSACTION_EVIDENCE");

        var evidenceId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body)));
        var payment = new SadadVerifiedPayment(
            evidenceId, invoiceId, vendorId, reference, amount, total, subtotal,
            refundedAmount, currency, transactionId, fees);
        return new(SadadInvoiceVerificationOutcome.VerifiedPaid, "STRICT_GETBYID_VERIFIED", payment);
    }

    private static bool TryRequiredBoolean(JsonElement parent, string name, out bool value)
    {
        value = false;
        if (parent.ValueKind != JsonValueKind.Object || !parent.TryGetProperty(name, out var element) ||
            (element.ValueKind != JsonValueKind.True && element.ValueKind != JsonValueKind.False))
            return false;
        value = element.GetBoolean();
        return true;
    }

    private static bool TryRequiredString(JsonElement parent, string name, out string value)
    {
        value = string.Empty;
        if (parent.ValueKind != JsonValueKind.Object || !parent.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.String)
            return false;
        value = element.GetString()?.Trim() ?? string.Empty;
        return value.Length > 0;
    }

    private static bool TryRequiredIdentifier(JsonElement parent, string name, out string value)
    {
        value = string.Empty;
        if (parent.ValueKind != JsonValueKind.Object || !parent.TryGetProperty(name, out var element)) return false;
        value = element.ValueKind switch
        {
            JsonValueKind.String => element.GetString()?.Trim() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(),
            _ => string.Empty
        };
        return value.Length > 0;
    }

    private static bool TryRequiredInt64(JsonElement parent, string name, out long value)
    {
        value = 0;
        return parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var element) &&
            element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out value);
    }

    private static bool TryRequiredDecimal(JsonElement parent, string name, out decimal value)
    {
        value = 0m;
        return parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var element) &&
            element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out value);
    }

}
