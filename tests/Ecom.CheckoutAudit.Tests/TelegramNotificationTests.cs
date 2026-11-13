using Ecom.Application.Telegram;
using Ecom.Domain.Interfaces;
using Ecom.Infrastructure.WhatsApp;

namespace Ecom.CheckoutAudit.Tests;

public sealed class TelegramNotificationTests
{
    [Fact]
    public void Enabled_notifications_require_a_well_formed_token_and_destination()
    {
        Assert.True(new TelegramOptions { Enabled = false }.IsValid());
        Assert.False(new TelegramOptions { Enabled = true, BotToken = "not-a-token", ChatId = "-1001" }.IsValid());
        Assert.False(new TelegramOptions { Enabled = true, BotToken = "123456:ABC_def", ChatId = "" }.IsValid());
        Assert.True(new TelegramOptions { Enabled = true, BotToken = "123456:ABC_def", ChatId = "-100123456" }.IsValid());
    }

    [Fact]
    public void Detailed_invoice_escapes_customer_data_and_includes_delivery_fields()
    {
        var invoice = new InvoiceData(
            "INV-1", "ORDER-1", "عميل <script>alert(1)</script>",
            new DateTime(2026, 11, 11, 10, 0, 0, DateTimeKind.Utc),
            [new InvoiceLine("منتج <img src=x onerror=alert(1)>", 2, 1.250m)],
            2.500m, 0m, 1m, 0m, 3.500m,
            CustomerPhone: "+96550000000",
            ShippingAddress: "الكويت، قطعة 1، منزل 10",
            PaymentMethod: "بطاقة / سداد",
            TransactionReference: "TXN-1",
            CouponCode: "SAVE10");

        var html = InvoicePdfRenderer.BuildHtml(invoice);

        Assert.Contains("+96550000000", html);
        Assert.Contains("الكويت، قطعة 1، منزل 10", html);
        Assert.Contains("بطاقة / سداد", html);
        Assert.Contains("TXN-1", html);
        Assert.Contains("SAVE10", html);
        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.DoesNotContain("<img src=x onerror=alert(1)>", html);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
        Assert.Contains("&lt;img src=x onerror=alert(1)&gt;", html);
    }
}
