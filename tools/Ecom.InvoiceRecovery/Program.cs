using Ecom.Application.WhatsApp;
using Ecom.Domain.Entity;
using Ecom.Domain.Interfaces;
using Ecom.Infrastructure.Data;
using Ecom.Infrastructure.WhatsApp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// Server maintenance tool: invoke through a root-controlled session, never a public endpoint.
// Intentionally scoped to this incident. It never calls a payment gateway or WhatsApp sender.
if (args.Length < 2 || args[0] is not ("verify" or "retry"))
    throw new ArgumentException("Usage: verify <API-publish-dir> | retry <API-publish-dir> <request-guid>");
var config = new ConfigurationBuilder().SetBasePath(Path.GetFullPath(args[1]))
    .AddJsonFile("appsettings.json", optional: false).AddEnvironmentVariables().Build();
var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? throw new InvalidOperationException("DATABASE_ENV_REQUIRED");
var o = new GupshupOptions();
config.GetSection("Gupshup").Bind(o);
if (o.Environment == "Sandbox") config.GetSection("Gupshup_sandbox").Bind(o);
o.PreparationRetryInvoiceIds = [6];
await using var db = new EcomDbContext(new DbContextOptionsBuilder<EcomDbContext>().UseNpgsql(connection,
    sql => sql.EnableRetryOnFailure()).Options);
var renderer = new InvoicePdfRenderer(Options.Create(o));
using var logs = LoggerFactory.Create(b => b.AddSimpleConsole());
if (args[0] == "retry")
{
    if (args.Length != 3 || !Guid.TryParse(args[2], out var requestId)) throw new ArgumentException("REQUEST_GUID_REQUIRED");
    var service = new WhatsAppInvoicePreparationRetry(db, renderer, Options.Create(o), logs.CreateLogger<WhatsAppInvoicePreparationRetry>());
    var result = await service.RetryAsync(6, 48, requestId, "Maintenance:SSH-root", CancellationToken.None);
    Console.WriteLine($"Invoice=6 Order=48 Success={result.Success} Code={result.Code} RequestId={requestId}");
    return result.Success ? 0 : 2;
}
var job = await db.WhatsAppInvoices.AsNoTracking().Include(j => j.PaymentAttempt).ThenInclude(a => a.Order).ThenInclude(o => o.Items)
    .Include(j => j.PaymentAttempt).ThenInclude(a => a.Order).ThenInclude(o => o.ShippingAddress)
    .SingleAsync(j => j.Id == 6 && j.PaymentAttempt.OrderId == 48);
var attempt = job.PaymentAttempt;
var order = attempt.Order;
var pdf = await renderer.RenderAsync(new InvoiceData(job.InvoiceNumber, order.OrderNumber, order.ShippingAddress.FullName,
    attempt.GatewayVerifiedAtUtc!.Value, order.Items.OrderBy(i => i.Id).Select(i => new InvoiceLine(i.Name, i.Quantity, i.Price)).ToArray(),
    order.Subtotal, order.CouponDiscountAmount ?? 0m, order.Shipping, order.Tax, order.Total), CancellationToken.None);
if (!pdf.AsSpan(0, 5).SequenceEqual("%PDF-"u8)) return 2;
Console.WriteLine($"PASS Invoice=6 Order=48 PdfBytes={pdf.Length} Rows={order.Items.Count} User={Environment.UserName}; no state changed");
return 0;
