using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ecom.Domain.constant;
using Ecom.Domain.Interfaces;
using Ecom.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ecom.Application.Telegram;

public sealed class TelegramOrderNotificationService(
    EcomDbContext db,
    IInvoicePdfRenderer invoiceRenderer,
    HttpClient client,
    IOptions<TelegramOptions> options,
    ILogger<TelegramOrderNotificationService> logger)
{
    private const int CaptionLimit = 1024;

    public async Task SendPaidOrderAsync(int orderId, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.Enabled) return;

        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.ShippingAddress)
            .Include(o => o.Coupon)
            .SingleAsync(o => o.Id == orderId, cancellationToken);
        var transaction = await db.Transactions.AsNoTracking()
            .Where(t => t.OrderId == orderId &&
                        (t.Status == TransactionStatus.Paid || t.Status == TransactionStatus.Collected))
            .OrderByDescending(t => t.ProcessedDate ?? t.TransactionDate)
            .ThenByDescending(t => t.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new TelegramDeliveryException(0, false, null);

        var isVerifiedElectronicPayment = transaction.Status == TransactionStatus.Paid &&
                                          transaction.GatewayVerifiedAtUtc is not null;
        var isCollectedCashOnDelivery = transaction.Status == TransactionStatus.Collected &&
                                        transaction.PaymentMethod == PaymentMethod.Cod;
        if (!isVerifiedElectronicPayment && !isCollectedCashOnDelivery)
            throw new TelegramDeliveryException(0, false, null);

        var gatewayTransactionId = await db.PaymentAttempts.AsNoTracking()
            .Where(a => a.TransactionId == transaction.Id)
            .OrderByDescending(a => a.Id)
            .Select(a => a.GatewayTransactionId)
            .FirstOrDefaultAsync(cancellationToken);
        var paidAtUtc = transaction.GatewayVerifiedAtUtc ?? transaction.ProcessedDate ?? transaction.TransactionDate;
        var invoiceNumber = $"INV-{order.OrderNumber}";
        var shippingAddress = FormatAddress(order.ShippingAddress);
        var paymentMethodName = PaymentMethodName(transaction.PaymentMethod);
        var invoice = new InvoiceData(
            invoiceNumber,
            order.OrderNumber,
            order.ShippingAddress.FullName,
            paidAtUtc,
            order.Items.Select(i => new InvoiceLine(i.Name, i.Quantity, i.Price)).ToList(),
            order.Subtotal,
            order.CouponDiscountAmount ?? 0m,
            order.Shipping,
            order.Tax,
            order.Total,
            CustomerPhone: order.ShippingAddress.PhoneNumber,
            ShippingAddress: shippingAddress,
            PaymentMethod: paymentMethodName,
            TransactionReference: transaction.TransactionReference,
            CouponCode: order.Coupon?.Code);

        var pdf = await invoiceRenderer.RenderAsync(invoice, cancellationToken);
        var caption = BuildCaption(order.OrderNumber, order.Total, transaction.PaymentMethod,
            order.ShippingAddress.FullName, order.ShippingAddress.PhoneNumber,
            shippingAddress, order.Items.Sum(i => i.Quantity), paidAtUtc,
            transaction.TransactionReference, gatewayTransactionId, settings.TimeZoneId);
        await SendDocumentAsync(settings, order.OrderNumber, caption, pdf, cancellationToken);
        logger.LogInformation("Telegram paid-order invoice sent for OrderId={OrderId}.", orderId);
    }

    internal async Task SendDocumentAsync(TelegramOptions settings, string orderNumber, string caption,
        byte[] pdf, CancellationToken cancellationToken)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(settings.ChatId, Encoding.UTF8), "chat_id");
        form.Add(new StringContent(caption, Encoding.UTF8), "caption");
        if (settings.MessageThreadId is not null)
            form.Add(new StringContent(settings.MessageThreadId.Value.ToString(CultureInfo.InvariantCulture)), "message_thread_id");
        if (settings.ProtectContent)
            form.Add(new StringContent("true"), "protect_content");

        var document = new ByteArrayContent(pdf);
        document.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        var safeOrderNumber = string.Concat(orderNumber.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_'));
        form.Add(document, "document", $"invoice-{safeOrderNumber}.pdf");

        // A bot token contains ':'. Prefixing the relative path with "./" prevents
        // Uri from interpreting the token prefix as a custom URI scheme.
        var endpoint = new Uri(client.BaseAddress!, $"./bot{settings.BotToken}/sendDocument");
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = form
        };
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            TelegramApiResponse? apiResponse = null;
            try
            {
                apiResponse = JsonSerializer.Deserialize<TelegramApiResponse>(body);
            }
            catch (JsonException)
            {
                // The status code still determines whether the delivery can be retried.
            }

            if (response.IsSuccessStatusCode && apiResponse?.Ok == true) return;

            var retryAfter = apiResponse?.Parameters?.RetryAfter is int seconds
                ? TimeSpan.FromSeconds(Math.Clamp(seconds, 1, 3600))
                : (TimeSpan?)null;
            var retryable = response.StatusCode == HttpStatusCode.TooManyRequests ||
                            (int)response.StatusCode >= 500;
            throw new TelegramDeliveryException((int)response.StatusCode, retryable, retryAfter);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException ||
                                   ex is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            // Transport errors can include the full request URI, which contains the bot token.
            throw new TelegramDeliveryException(0, true, null);
        }
    }

    internal static string BuildCaption(string orderNumber, decimal total, PaymentMethod paymentMethod,
        string customerName, string phone, string address, int itemCount, DateTime paidAtUtc,
        string? transactionReference, string? gatewayTransactionId, string timeZoneId)
    {
        var paidAt = ConvertTime(paidAtUtc, timeZoneId);
        var builder = new StringBuilder()
            .AppendLine("✅ عملية شراء ناجحة")
            .AppendLine($"رقم الطلب: {orderNumber}")
            .AppendLine($"المبلغ المدفوع: {total.ToString("0.000", CultureInfo.InvariantCulture)} د.ك")
            //.AppendLine($"طريقة الدفع: {PaymentMethodName(paymentMethod)}")
            .AppendLine($"العميل: {customerName}")
            .AppendLine($"الهاتف: {phone}")
            .AppendLine($"العنوان: {address}")
            .AppendLine($"عدد القطع: {itemCount}")
            .AppendLine($"وقت الدفع: {paidAt:yyyy-MM-dd HH:mm}");
        if (!string.IsNullOrWhiteSpace(transactionReference))
            builder.AppendLine($"مرجع العملية: {transactionReference}");
        if (!string.IsNullOrWhiteSpace(gatewayTransactionId))
            builder.AppendLine($"رقم عملية بوابة الدفع: {gatewayTransactionId}");
        builder.Append("📎 الفاتورة التفصيلية مرفقة بصيغة PDF");

        var caption = builder.ToString();
        return caption.Length <= CaptionLimit ? caption : caption[..(CaptionLimit - 3)] + "...";
    }

    private static string FormatAddress(Ecom.Domain.Entity.ShippingAddress address)
    {
        var parts = new[]
        {
            address.AddressLine1, address.AddressLine2, address.State, address.City,
            Value("القطعة", address.AlQataa), Value("الشارع", address.AlSharee),
            Value("الجادة", address.AlJada), Value("المنزل", address.AlManzil),
            Value("الدور", address.AlDor), Value("الشقة", address.AlShakka),
            address.PostalCode, address.Country
        };
        return string.Join("، ", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct());
    }

    private static string? Value(string label, string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : $"{label} {value}";

    private static DateTime ConvertTime(DateTime value, string timeZoneId)
    {
        var utc = value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        try { return TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId)); }
        catch (TimeZoneNotFoundException) { return utc; }
        catch (InvalidTimeZoneException) { return utc; }
    }

    private static string PaymentMethodName(PaymentMethod method) => method switch
    {
        PaymentMethod.Card => "بطاقة / سداد",
        PaymentMethod.Cod => "الدفع عند الاستلام",
        PaymentMethod.Paypal => "PayPal",
        PaymentMethod.Bank => "تحويل بنكي",
        _ => method.ToString()
    };

    private sealed class TelegramApiResponse
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; init; }

        [JsonPropertyName("parameters")]
        public TelegramResponseParameters? Parameters { get; init; }
    }

    private sealed class TelegramResponseParameters
    {
        [JsonPropertyName("retry_after")]
        public int? RetryAfter { get; init; }
    }
}

public sealed class TelegramDeliveryException(
    int statusCode,
    bool retryable,
    TimeSpan? retryAfter)
    : Exception($"TELEGRAM_DELIVERY_FAILED_{statusCode}")
{
    public int StatusCode { get; } = statusCode;
    public bool Retryable { get; } = retryable;
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
