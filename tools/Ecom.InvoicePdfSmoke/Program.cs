using System.Security.Cryptography;
using Ecom.Domain.Interfaces;
using Ecom.Infrastructure.WhatsApp;
using Microsoft.Extensions.Options;

if (args.FirstOrDefault() == "install") return Microsoft.Playwright.Program.Main(args);
var output = args.FirstOrDefault() ?? "/tmp/invoice-pdf-smoke";
Directory.CreateDirectory(output);
Console.WriteLine($"User={Environment.UserName}; OS={System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
Console.WriteLine($"PLAYWRIGHT_BROWSERS_PATH={Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH")}");
var renderer = new InvoicePdfRenderer(Options.Create(new GupshupOptions()));
foreach (var count in new[] { 0, 3, 60 })
{
    var lines = Enumerable.Range(1, count).Select(n => new InvoiceLine(
        $"LINE-{n:D3} منتج الجوهرة بلس رقم {n} - عبوة عناية مميزة باسم عربي طويل للتحقق من التفاف النص وتقسيم الصفحات", n % 3 + 1, 12.345m)).ToArray();
    var subtotal = lines.Sum(l => l.Price * l.Quantity);
    var data = new InvoiceData("INV-LINUX-SYNTHETIC", "ORDER-SYNTHETIC", "عميل اختبار اصطناعي <script>throw 1</script>",
        new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc), lines, subtotal, 1m, 2m, 0m, subtotal + 1m);
    var start = System.Diagnostics.Stopwatch.StartNew();
    var pdf = await renderer.RenderAsync(data, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(60));
    if (pdf.Length < 10000 || !pdf.AsSpan(0, 5).SequenceEqual("%PDF-"u8)) throw new InvalidOperationException("INVALID_PDF");
    await File.WriteAllBytesAsync(Path.Combine(output, $"invoice-{count}.pdf"), pdf);
    Console.WriteLine($"PASS rows={count} bytes={pdf.Length} elapsedMs={start.ElapsedMilliseconds} sha256={Convert.ToHexString(SHA256.HashData(pdf))}");
}
return 0;
