using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Xml.Linq;
using Ecom.Application.DTOs.Order;
using Ecom.Application.Services;
using Ecom.Domain.Entity;
using ExcelDataReader;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualBasic.FileIO;
using MimeKit;

namespace Ecom.CheckoutAudit.Tests;

public sealed class P2OutputSecurityTests
{
    static P2OutputSecurityTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    [Fact]
    public void Csv_and_excel_preserve_cells_and_numeric_totals_under_non_English_culture()
    {
        var names = new[] { "مرحبا, \"Quoted\"\r\nSecond line", "=unsafe", "+unsafe", "-unsafe", "@unsafe",
            " \t=unsafe", "\runsafe", "\nunsafe", "\tunsafe", "\uFEFF  ＝unsafe", "ordinary = text", "_x0041_" };
        var date = new DateTime(2026, 11, 14, 2, 30, 45);
        var rows = names.Select((name, i) => new TransactionAdvancedDto
        {
            Id = i + 1, CustomerName = name, OrderNumber = i == 0 ? "=order" : "Order-" + i,
            Amount = i == 0 ? -1.125m : 12.345m, TransactionDate = date, IsRefunded = i == 0
        }).ToArray();
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            using var csv = new TextFieldParser(new MemoryStream(TransactionExport.Csv(rows)), Encoding.UTF8)
                { HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = false };
            csv.SetDelimiters(",");
            Assert.Equal(8, csv.ReadFields()!.Length);
            for (var i = 0; i < rows.Length; i++)
            {
                var fields = csv.ReadFields()!;
                Assert.Equal(8, fields.Length);
                Assert.Equal(i == 0 ? "'=order" : rows[i].OrderNumber, fields[1]);
                Assert.Equal(i is >= 1 and <= 9 ? "'" + names[i] : names[i], fields[2]);
                Assert.Equal(rows[i].Amount, decimal.Parse(fields[3], CultureInfo.InvariantCulture));
                Assert.Equal("2026-11-14 02:30:45", fields[6]);
            }
            Assert.True(csv.EndOfData);

            var xlsx = TransactionExport.Excel(rows);
            using var workbook = ExcelReaderFactory.CreateOpenXmlReader(new MemoryStream(xlsx));
            Assert.True(workbook.Read());
            Assert.Equal("Transactions", workbook.Name);
            Assert.Equal("Customer Name", workbook.GetString(2));
            decimal total = 0;
            for (var i = 0; i < rows.Length; i++)
            {
                Assert.True(workbook.Read());
                Assert.Equal(rows[i].OrderNumber, workbook.GetString(1));
                Assert.Equal(names[i], workbook.GetString(2));
                total += (decimal)workbook.GetDouble(3);
                Assert.Equal(date, workbook.GetDateTime(6));
                Assert.Equal(i == 0, workbook.GetBoolean(7));
            }
            Assert.False(workbook.Read());
            Assert.Equal(rows.Sum(r => r.Amount), total);

            using var zip = new ZipArchive(new MemoryStream(xlsx));
            var worksheet = Assert.Single(zip.Entries, e => e.FullName.StartsWith("xl/worksheets/") && e.Name.EndsWith(".xml"));
            using var xml = worksheet.Open();
            var sheet = XDocument.Load(xml);
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            Assert.Empty(sheet.Descendants(ns + "f"));
            Assert.All(sheet.Descendants(ns + "row").Skip(1), row =>
            {
                Assert.Equal("inlineStr", (string?)row.Elements(ns + "c").ElementAt(2).Attribute("t"));
                Assert.Equal("n", (string?)row.Elements(ns + "c").ElementAt(3).Attribute("t"));
            });
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void Excel_preserves_the_exact_database_amount_when_it_exceeds_numeric_precision()
    {
        const decimal amount = 123456789012345.123m;
        var exports = TransactionExport.Excel([new TransactionAdvancedDto { Id = 1, Amount = amount }]);
        using var reader = ExcelReaderFactory.CreateOpenXmlReader(new MemoryStream(exports));
        Assert.True(reader.Read());
        Assert.True(reader.Read());
        Assert.Equal(amount.ToString(CultureInfo.InvariantCulture), reader.GetString(3));
    }

    [Fact]
    public async Task Contact_uses_fixed_headers_literal_fields_two_MIME_alternatives_and_a_route_budget()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new CapturingSmtp();
        using var factory = new CheckoutApiFactory(PostgresTestDatabase.ConnectionString($"unused_{Guid.NewGuid():N}"))
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(SmtpSettings));
                builder.ConfigureServices(services => services.AddSingleton<ISmtpTransport>(transport));
            });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var input = new { name = "اسم <b>literal</b>", email = "visitor@example.test", phoneNumber = "</td><td>",
            subject = "Question <h1>hello</h1>\r\nBcc: another@example.test", message = "First <em>literal</em>\r\nSecond & \"quoted\"" };
        for (var i = 0; i < 3; i++)
        {
            using var response = await client.PostAsJsonAsync("/api/Auth/contact", input, ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        using var throttled = await client.PostAsJsonAsync("/api/Auth/contact", input, ct);
        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
        Assert.Equal(3, transport.Messages.Count);

        var mail = transport.Messages[0];
        Assert.Equal("info@aljawhara.com", Assert.Single(mail.To.Mailboxes).Address);
        Assert.Empty(mail.Bcc);
        Assert.DoesNotContain(input.subject, mail.Subject);
        Assert.IsType<MultipartAlternative>(mail.Body);
        Assert.Contains("&lt;b&gt;literal&lt;/b&gt;", mail.HtmlBody);
        Assert.Contains("&lt;/td&gt;&lt;td&gt;", mail.HtmlBody);
        Assert.Contains("Question &lt;h1&gt;hello&lt;/h1&gt;", mail.HtmlBody);
        Assert.Contains("First &lt;em&gt;literal&lt;/em&gt;<br>Second &amp; &quot;quoted&quot;", mail.HtmlBody);
        Assert.DoesNotContain("<em>literal</em>", mail.HtmlBody);
        Assert.Contains(input.message.ReplaceLineEndings("\n"), mail.TextBody!.ReplaceLineEndings("\n"));
    }

    [Fact]
    public async Task Other_templates_encode_account_order_and_shipping_text_and_escape_URL_parameters()
    {
        var transport = new CapturingSmtp();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(SmtpSettings).Build();
        var service = new EmailService(configuration, NullLogger<EmailService>.Instance, transport);
        var user = new AppUsers { Id = "user&other=1", UserName = "<b>Customer</b>", Email = "user@example.test" };
        Assert.True(await service.SendEmailChangeConfirmationAsync(user, "new+tag@example.test", "token+'=&"));
        var account = transport.Messages[0];
        Assert.Contains("&lt;b&gt;Customer&lt;/b&gt;", account.HtmlBody);
        Assert.Contains("userId=user%26other%3D1&amp;token=token%2B%27%3D%26&amp;newEmail=new%2Btag%40example.test", account.HtmlBody);
        Assert.Contains("<b>Customer</b>", account.TextBody);
        Assert.True(await service.SendOrderNotificationToAdminAsync(new OrderDto
        {
            OrderNumber = "order-1", CustomerName = "<b>Customer</b>", CouponCode = "<i>code</i>", CouponDiscountAmount = 1m,
            Items = [new OrderItemDto { Name = "Tea & <b>coffee</b>", Quantity = 1, Price = 5 }],
            ShippingAddress = new ShippingAddressDto { FullName = "Name", Street = "<i>Street</i>", State = "<b>State</b>" }
        }, "admin@example.test", "Created"));
        var order = transport.Messages[1];
        Assert.IsType<MultipartAlternative>(order.Body);
        Assert.Contains("&lt;i&gt;Street&lt;/i&gt;", order.HtmlBody);
        Assert.Contains("&lt;b&gt;State&lt;/b&gt;", order.HtmlBody);
        Assert.Contains("Tea &amp; &lt;b&gt;coffee&lt;/b&gt;", order.HtmlBody);
        Assert.Contains("&lt;i&gt;code&lt;/i&gt;", order.HtmlBody);
        Assert.Contains("<i>Street</i>", order.TextBody);
    }

    private static readonly Dictionary<string, string?> SmtpSettings = new()
    {
        ["AppSettings:BaseUrl"] = "https://store.example.test",
        ["SmtpSettings:Host"] = "mail.example.test", ["SmtpSettings:Username"] = "test",
        ["SmtpSettings:Password"] = "test-configuration", ["SmtpSettings:FromEmail"] = "store@example.test"
    };

    private sealed class CapturingSmtp : ISmtpTransport
    {
        public List<MimeMessage> Messages { get; } = [];
        public async Task SendAsync(MimeMessage message, string host, int port, string username, string password)
        {
            using var wire = new MemoryStream();
            await message.WriteToAsync(wire);
            wire.Position = 0;
            Messages.Add(await MimeMessage.LoadAsync(wire));
        }
    }
}
