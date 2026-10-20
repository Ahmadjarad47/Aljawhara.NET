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
    private const string DiagnosticsEnabledKey = "Sadad:Diagnostics:EnableInvoiceLogging";
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

        var payloadJson = JsonSerializer.Serialize(new { Invoices = new[] { invoice } });
        LogCreateRequest(attempt, order, payloadJson);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/Invoice/insert");
            request.Headers.TryAddWithoutValidation("authorization", $"Bearer {tokens.AccessToken}");
            request.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
            using var response = await httpClientFactory.CreateClient("Sadad").SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            LogCreateResponse(attempt.Id, response.StatusCode, body);
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
                var getBody = await getResponse.Content.ReadAsStringAsync(cancellationToken);
                LogGetByIdAmounts(attempt.Id, getResponse.StatusCode, getBody);
                var envelope = JsonSerializer.Deserialize<InvoiceEnvelope>(getBody);
                if (envelope?.isValid == true) url = envelope.response?.url?.Trim();
            }
            else
            {
                // Preserve the existing non-success path: do not consume the body just for diagnostics.
                LogGetByIdAmounts(attempt.Id, getResponse.StatusCode, null);
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
            LogGetByIdAmounts(attempt.Id, response.StatusCode, body);
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
        if (!HasKwdScale(amount) || !HasKwdScale(total) || !HasKwdScale(subtotal) || !HasKwdScale(refundedAmount))
            return new(SadadInvoiceVerificationOutcome.Invalid, "KWD_SCALE_INVALID");
        if (amount != attempt.Amount || total != attempt.Amount || subtotal != attempt.Amount)
        {
            logger.LogWarning(
                "Sadad amount comparison failed for PaymentAttemptId={PaymentAttemptId}. ReasonCode={ReasonCode}. ExpectedAmount={ExpectedAmount}, ExpectedGrossAmount={ExpectedGrossAmount}, ExpectedDiscountAmount={ExpectedDiscountAmount}, GatewayAmount={GatewayAmount}, GatewayTotal={GatewayTotal}, GatewaySubtotal={GatewaySubtotal}",
                attempt.Id, "AMOUNT_FIELDS_MISMATCH", attempt.Amount, orderAmounts.Gross, orderAmounts.Discount,
                amount, total, subtotal);
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
            refundedAmount, currency, transactionId);
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

    private static bool HasKwdScale(decimal value) => decimal.Round(value, KwdMoney.Scale, MidpointRounding.AwayFromZero) == value;

    private bool InvoiceDiagnosticsEnabled => configuration.GetValue<bool>(DiagnosticsEnabledKey);

    private void LogCreateRequest(Domain.Entity.PaymentAttempt attempt, Domain.Entity.Order order, string payloadJson)
    {
        if (!InvoiceDiagnosticsEnabled || !logger.IsEnabled(LogLevel.Information)) return;

        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            var serializedInvoice = document.RootElement.GetProperty("Invoices")[0];
            var serializedItems = serializedInvoice.GetProperty("items");
            var itemTotal = 0m;
            foreach (var item in serializedItems.EnumerateArray())
                itemTotal += item.GetProperty("quantity").GetDecimal() * item.GetProperty("amount").GetDecimal();

            var itemValues = WriteSelectedJson(serializedItems, static name => name is "quantity" or "amount");
            var financialValues = WriteCreateFinancialJson(serializedInvoice);
            logger.LogInformation(
                "[Sadad CREATE REQUEST] PaymentAttemptId={PaymentAttemptId} MerchantReference={MerchantReference} OrderSubtotal={OrderSubtotal} Shipping={Shipping} Tax={Tax} Discount={Discount} OrderTotal={OrderTotal} PaymentAttemptAmount={PaymentAttemptAmount} InvoiceAmount={InvoiceAmount} Currency={Currency} InvoiceItems={InvoiceItems} InvoiceItemsTotal={InvoiceItemsTotal} SerializedFinancialValues={SerializedFinancialValues}",
                attempt.Id, attempt.MerchantReference, order.Subtotal, order.Shipping, order.Tax,
                order.CouponDiscountAmount ?? 0m, order.Total, attempt.Amount,
                serializedInvoice.GetProperty("amount").GetDecimal(),
                serializedInvoice.GetProperty("currency_Code").GetString(), itemValues, itemTotal, financialValues);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            logger.LogWarning(ex,
                "Sadad create-request diagnostics could not inspect the serialized financial fields for PaymentAttemptId={PaymentAttemptId}.",
                attempt.Id);
        }
    }

    private void LogCreateResponse(int paymentAttemptId, HttpStatusCode statusCode, string body)
    {
        if (!InvoiceDiagnosticsEnabled || !logger.IsEnabled(LogLevel.Information)) return;

        bool? isValid = null;
        string? invoiceId = null;
        string? errorKey = null;
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("isValid", out var valid) && valid.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    isValid = valid.GetBoolean();
                if (root.TryGetProperty("errorKey", out var rootError)) errorKey = ReadSafeScalar(rootError);
                if (root.TryGetProperty("response", out var response) && response.ValueKind == JsonValueKind.Object)
                {
                    if (response.TryGetProperty("invoiceId", out var id)) invoiceId = ReadSafeScalar(id);
                    if (errorKey == null && response.TryGetProperty("errorKey", out var responseError))
                        errorKey = ReadSafeScalar(responseError);
                }
            }
        }
        catch (JsonException)
        {
            // The operational path still handles the response exactly as before; diagnostics never log the raw body.
        }

        logger.LogInformation(
            "[Sadad CREATE RESPONSE] PaymentAttemptId={PaymentAttemptId} StatusCode={StatusCode} IsValid={IsValid} InvoiceId={InvoiceId} ErrorKey={ErrorKey}",
            paymentAttemptId, (int)statusCode, isValid, invoiceId, errorKey);
    }

    private void LogGetByIdAmounts(int paymentAttemptId, HttpStatusCode statusCode, string? body)
    {
        if (!InvoiceDiagnosticsEnabled || !logger.IsEnabled(LogLevel.Information)) return;

        decimal? amount = null;
        decimal? total = null;
        decimal? subtotal = null;
        var discountFields = "{}";
        var feeFields = "{}";
        var serializedAmountFields = "{}";
        try
        {
            if (body != null)
            {
                using var document = JsonDocument.Parse(body);
                var root = document.RootElement;
                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("response", out var invoice) &&
                    invoice.ValueKind == JsonValueKind.Object)
                {
                    amount = ReadOptionalDecimal(invoice, "amount");
                    total = ReadOptionalDecimal(invoice, "total");
                    subtotal = ReadOptionalDecimal(invoice, "subtotal");
                    discountFields = WriteSelectedNumericJson(invoice, static name =>
                        name.Contains("discount", StringComparison.OrdinalIgnoreCase));
                    feeFields = WriteSelectedNumericJson(invoice, static name =>
                        name.Contains("fee", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("charge", StringComparison.OrdinalIgnoreCase));
                    serializedAmountFields = WriteSelectedNumericJson(invoice, static name =>
                        name is "amount" or "total" or "subtotal" ||
                        name.Contains("discount", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("fee", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("charge", StringComparison.OrdinalIgnoreCase));
                }
            }
        }
        catch (JsonException)
        {
            // Keep malformed/non-JSON gateway bodies out of logs.
        }

        logger.LogInformation(
            "[Sadad GETBYID AMOUNTS] PaymentAttemptId={PaymentAttemptId} StatusCode={StatusCode} Amount={GatewayAmount} Total={GatewayTotal} Subtotal={GatewaySubtotal} DiscountFields={GatewayDiscountFields} FeeFields={GatewayFeeFields} SerializedAmountFields={GatewaySerializedAmountFields}",
            paymentAttemptId, (int)statusCode, amount, total, subtotal, discountFields, feeFields, serializedAmountFields);
    }

    private static decimal? ReadOptionalDecimal(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out var value)
            ? value
            : null;

    private static string? ReadSafeScalar(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => SafeCode(element.GetString()),
        JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => element.GetRawText(),
        _ => null
    };

    private static string? SafeCode(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 128 &&
        value.All(character => char.IsLetterOrDigit(character) || character is '_' or '-' or '.')
            ? value
            : null;

    private static string WriteCreateFinancialJson(JsonElement invoice)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var property in invoice.EnumerateObject())
            {
                if (property.Name is not ("amount" or "currency_Code" or "discount_Amount" or "discount_Amount_Total" or "discount_Type" or "items"))
                    continue;
                writer.WritePropertyName(property.Name);
                if (property.Name == "items") WriteSelectedJson(writer, property.Value, static name => name is "quantity" or "amount");
                else property.Value.WriteTo(writer);
            }
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string WriteSelectedJson(JsonElement element, Func<string, bool> include)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) WriteSelectedJson(writer, element, include);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string WriteSelectedNumericJson(JsonElement element, Func<string, bool> include)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var property in element.EnumerateObject())
            {
                if (!include(property.Name) || property.Value.ValueKind is not (JsonValueKind.Number or JsonValueKind.Null))
                    continue;
                writer.WritePropertyName(property.Name);
                property.Value.WriteTo(writer);
            }
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteSelectedJson(Utf8JsonWriter writer, JsonElement element, Func<string, bool> include)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var item in element.EnumerateArray()) WriteSelectedJson(writer, item, include);
            writer.WriteEndArray();
            return;
        }

        writer.WriteStartObject();
        foreach (var property in element.EnumerateObject())
        {
            if (!include(property.Name)) continue;
            writer.WritePropertyName(property.Name);
            property.Value.WriteTo(writer);
        }
        writer.WriteEndObject();
    }
}
