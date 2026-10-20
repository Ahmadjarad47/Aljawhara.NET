using System.Globalization;
using System.Net;
using System.Text;
using Ecom.Domain.Interfaces;
using Microsoft.Playwright;
using Microsoft.Extensions.Options;

namespace Ecom.Infrastructure.WhatsApp;

// Chromium provides Arabic shaping, bidi layout, embedded fonts and paginated, searchable PDF.
// All data is HTML-escaped and the renderer never makes outbound resource requests.
public sealed class InvoicePdfRenderer(IOptions<GupshupOptions> options) : IInvoicePdfRenderer
{
    public async Task<byte[]> RenderAsync(InvoiceData invoice, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true, Channel = options.Value.PdfBrowserChannel });
        await using var context = await browser.NewContextAsync(new() { JavaScriptEnabled = false, Offline = true });
        var page = await context.NewPageAsync();
        // Slightly narrower than A4's printable area so measured line wrapping is conservative.
        await page.SetViewportSizeAsync(680, 1100);
        await page.EmulateMediaAsync(new() { Media = Media.Print });
        page.SetDefaultTimeout(30000);
        await page.SetContentAsync(BuildHtml(invoice), new() { WaitUntil = WaitUntilState.Load });
        await page.EvaluateAsync("async () => { await document.fonts.ready; }");
        // Separate tables avoid Chromium clipping repeated table headers at some page boundaries.
        // Pagination uses the real, font-loaded row heights, including wrapped Arabic product names.
        await page.EvaluateAsync("""
            invoiceNumber => {
                const intro = document.getElementById('invoice-intro');
                const ending = document.getElementById('invoice-ending');
                const table = document.querySelector('table');
                const rows = Array.from(table.tBodies[0].rows);
                document.body.replaceChildren();
                const limit = 980;
                const newPage = first => {
                    const sheet = document.createElement('section');
                    sheet.className = 'invoice-page';
                    document.body.append(sheet);
                    if (first) sheet.append(intro);
                    return sheet;
                };
                const newTable = sheet => {
                    const next = table.cloneNode(false);
                    next.append(table.tHead.cloneNode(true));
                    const body = document.createElement('tbody');
                    next.append(body);
                    sheet.append(next);
                    return body;
                };
                let sheet = newPage(true);
                let body = newTable(sheet);
                for (const row of rows) {
                    body.append(row);
                    if (sheet.getBoundingClientRect().height > limit && body.rows.length > 1) {
                        row.remove();
                        sheet = newPage(false);
                        body = newTable(sheet);
                        body.append(row);
                    }
                }
                sheet.append(ending);
                if (sheet.getBoundingClientRect().height > limit) {
                    ending.remove();
                    const finalSheet = newPage(false);
                    const finalBody = newTable(finalSheet);
                    // Keep a couple of product lines with totals instead of an orphan totals page.
                    Array.from(body.rows).slice(-2).forEach(row => finalBody.append(row));
                    if (!body.rows.length && !sheet.contains(intro)) sheet.remove();
                    finalSheet.append(ending);
                }
            }
            """, invoice.InvoiceNumber);
        cancellationToken.ThrowIfCancellationRequested();
        return await page.PdfAsync(new()
        {
            Format = "A4", PrintBackground = true, PreferCSSPageSize = true,
            DisplayHeaderFooter = true,
            HeaderTemplate = $"<div style='font:9px Arial;width:100%;text-align:center;color:#6b7280'>{WebUtility.HtmlEncode(invoice.InvoiceNumber)}</div>",
            FooterTemplate = "<div style='font:10px Arial;width:100%;text-align:center;color:#6b7280'><span class='pageNumber'></span> / <span class='totalPages'></span> &nbsp; | &nbsp; ALJAWHARA PLUS</div>",
            Margin = new() { Top = "16mm", Bottom = "18mm", Left = "14mm", Right = "14mm" }
        });
    }

    public static string BuildHtml(InvoiceData i)
    {
        static string H(string s) => WebUtility.HtmlEncode(s);
        static string M(decimal n) => decimal.Round(n, 3, MidpointRounding.AwayFromZero).ToString("F3", CultureInfo.InvariantCulture);
        var font = Path.Combine(AppContext.BaseDirectory, "InvoiceAssets", "NotoSansArabic.ttf");
        if (!File.Exists(font)) throw new InvalidOperationException("INVOICE_ARABIC_FONT_NOT_INSTALLED");
        var fontData = Convert.ToBase64String(File.ReadAllBytes(font));
        var logoData = Convert.ToBase64String(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "InvoiceAssets", "logo.png")));
        var rows = new StringBuilder();
        foreach (var line in i.Lines)
            rows.Append($"<tr><td>{H(line.Name)}</td><td class='number'>{line.Quantity}</td><td class='number'>{M(line.Price)}</td><td class='number'>{M(line.Price * line.Quantity)}</td></tr>");
        var paidAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(i.PaidAtUtc, DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuwait"));
        return $$"""
            <!DOCTYPE html><html lang="ar" dir="rtl"><head><meta charset="utf-8"><style>
            @font-face{font-family:Invoice;src:url(data:font/ttf;base64,{{fontData}}) format('truetype');font-weight:100 900}
            @page{size:A4}*{box-sizing:border-box}body{font-family:Invoice,Arial,sans-serif;font-size:12px;line-height:1.8;color:#182a35;margin:0}
            .invoice-page{display:flow-root;break-after:page}.invoice-page:last-child{break-after:auto}
            .brand{display:flex;align-items:center;gap:16px;border-bottom:3px solid #ba954b;padding-bottom:18px;margin-bottom:24px}
            .brand h1{margin:0;font-size:27px;color:#143a39}.en{font-family:Arial,sans-serif;letter-spacing:2px;font-size:10px;color:#92713c}
            h2{font-size:22px;margin:10px 0}.paid{color:#16734d;background:#e8f5ec;padding:6px 16px;border-radius:6px;display:inline-block;font-size:13px}
            .meta{display:grid;grid-template-columns:1fr 1fr;gap:12px 24px;background:#f5f7f7;border-radius:8px;padding:18px;margin:24px 0}
            .label{font-size:10px;color:#61737b}.value{font-size:13px;overflow-wrap:anywhere}.latin{direction:ltr;unicode-bidi:isolate;display:inline-block;font-family:Arial,sans-serif}
            table{border-collapse:separate;border-spacing:0;width:100%;table-layout:fixed;margin:20px 0}thead{display:table-row-group;break-inside:avoid;break-after:avoid}th{background:#173f3e;color:white;font-weight:600}
            th,td{padding:12px 10px;text-align:right;border-bottom:1px solid #e4e9e9;overflow-wrap:anywhere}th:first-child{width:48%}tr{break-inside:avoid}tbody tr:nth-child(even){background:#f8faf9}
            .number{direction:ltr;text-align:center;unicode-bidi:isolate;font-variant-numeric:tabular-nums}.summary{width:54%;margin-right:auto;break-inside:avoid}
            .sumrow{display:flex;justify-content:space-between;padding:7px 10px;gap:15px}.total{background:#173f3e;color:white;padding:15px 12px;font-weight:700;font-size:17px;margin-top:9px;border-radius:5px}
            .thanks{border-top:1px solid #ded8ca;margin-top:32px;padding-top:16px;text-align:center;color:#61737b;font-size:11px;break-inside:avoid}
            </style></head><body>
            <section id="invoice-intro"><header class="brand"><img src="data:image/png;base64,{{logoData}}" width="100" height="65" style="object-fit:contain" alt="شعار الجوهرة"><div><h1>الجوهرة بلس</h1><div class="en">ALJAWHARA PLUS</div></div></header>
            <h2>فاتورة الطلب</h2><span class="paid">مدفوع بنجاح</span>
            <section class="meta"><div><div class="label">رقم الفاتورة</div><div class="value latin">{{H(i.InvoiceNumber)}}</div></div><div><div class="label">رقم الطلب</div><div class="value latin">{{H(i.OrderNumber)}}</div></div><div><div class="label">اسم الزبون</div><div class="value">{{H(i.CustomerName)}}</div></div><div><div class="label">تاريخ تأكيد الدفع - توقيت الكويت</div><div class="value latin">{{paidAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}}</div></div></section></section>
            <table><thead><tr><th>المنتج</th><th class="number">الكمية</th><th class="number">سعر الوحدة<br>KWD</th><th class="number">الإجمالي<br>KWD</th></tr></thead><tbody>{{rows}}</tbody></table>
            <section id="invoice-ending"><section class="summary"><div class="sumrow"><span>المجموع الفرعي</span><span class="latin">{{M(i.Subtotal)}}</span></div><div class="sumrow"><span>الخصم</span><span class="latin">{{M(i.Discount)}}</span></div><div class="sumrow"><span>رسوم التوصيل</span><span class="latin">{{M(i.Shipping)}}</span></div><div class="sumrow"><span>الضريبة</span><span class="latin">{{M(i.Tax)}}</span></div><div class="sumrow total"><span>المبلغ النهائي</span><span class="latin">{{M(i.Total)}} KWD</span></div></section>
            <footer class="thanks">شكراً لتسوقك من الجوهرة بلس<br>تم إنشاء هذه الفاتورة بعد تأكيد الدفع من سجلات الطلب.</footer></section>
            </body></html>
            """;
    }
}
