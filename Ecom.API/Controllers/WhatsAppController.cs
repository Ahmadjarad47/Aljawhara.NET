using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ecom.Application.WhatsApp;
using Ecom.Domain.Entity;
using Ecom.Infrastructure.Data;
using Ecom.Infrastructure.WhatsApp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ecom.API.Controllers;

[ApiController]
[Route("api/whatsapp")]
public sealed class WhatsAppController(EcomDbContext db, WhatsAppInvoiceProcessor processor,
    IOptions<GupshupOptions> options) : ControllerBase
{
    [HttpGet("invoices/{id:int}/document")]
    [AllowAnonymous]
    public async Task<IActionResult> Document(int id, [FromQuery] string? token, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store, private";
        Response.Headers.Pragma = "no-cache";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        if (token is not { Length: 64 } || !token.All(char.IsAsciiHexDigit)) return NotFound();
        var hash = WhatsAppInvoiceProcessor.HashToken(token);
        var pdf = await db.WhatsAppInvoices.AsNoTracking()
            .Where(j => j.Id == id && j.MediaTokenHash == hash && j.MediaExpiresAtUtc > DateTime.UtcNow)
            .Select(j => j.Pdf).SingleOrDefaultAsync(ct);
        return pdf == null ? NotFound() : File(pdf, "application/pdf", $"invoice-{id}.pdf");
    }

    // Self-serve callbacks do not document a signature contract. A per-app, high-entropy
    // HTTPS callback secret is mandatory, supplied in the dashboard callback URL.
    [HttpPost("webhook")]
    [AllowAnonymous]
    [RequestSizeLimit(32768)]
    public async Task<IActionResult> Webhook([FromQuery] string? token, [FromBody] JsonElement body, CancellationToken ct)
    {
        var o = options.Value;
        if (o.WebhookSecret.Length < 32 || token == null || token.Length > 512 ||
            !CryptographicOperations.FixedTimeEquals(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)),
                SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(o.WebhookSecret)))) return Unauthorized();
        static string? S(JsonElement e, string name) => e.ValueKind == JsonValueKind.Object &&
            e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
        if (S(body, "app") != o.AppName) return BadRequest();
        if (S(body, "type") != "message-event") return Ok();
        if (!body.TryGetProperty("payload", out var payload)) return BadRequest();
        var type = S(payload, "type");
        if (type is not ("enqueued" or "sent" or "delivered" or "read" or "failed")) return Ok();
        var id = S(payload, "gsId") ?? (type is "enqueued" or "failed" ? S(payload, "id") : null);
        var destination = S(payload, "destination");
        if (id is not { Length: > 0 and <= 200 } || destination == null ||
            !Regex.IsMatch(destination, "^[1-9][0-9]{7,14}$") ||
            !body.TryGetProperty("timestamp", out var timestamp) || timestamp.ValueKind != JsonValueKind.Number ||
            !timestamp.TryGetInt64(out var milliseconds) ||
            milliseconds < 946684800000 || milliseconds > DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeMilliseconds()) return BadRequest();
        var occurred = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime;
        var whatsappId = S(payload, "gsId") != null ? S(payload, "id") : null;
        if (whatsappId?.Length > 200) return BadRequest();
        var key = WhatsAppInvoiceProcessor.HashToken(JsonSerializer.Serialize(new { o.AppName, id, destination, type, milliseconds }));
        var now = DateTime.UtcNow;
        // ON CONFLICT makes repeat callbacks a success and avoids aborting a PostgreSQL transaction.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "WhatsAppDeliveryEvents"
            ("EventKey", "AppName", "ProviderMessageId", "Destination", "WhatsAppMessageId", "Type", "OccurredAtUtc", "CreatedAt", "CreatedBy", "IsDeleted", "IsActive")
            VALUES ({key}, {o.AppName}, {id}, {destination}, {whatsappId}, {type}, {occurred}, {now}, 'GupshupWebhook', FALSE, TRUE)
            ON CONFLICT ("EventKey") DO NOTHING
            """, ct);
        await processor.ApplyEventsAsync(id, ct);
        return Ok();
    }

    [HttpGet("admin/invoices")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Status(CancellationToken ct) => Ok(await db.WhatsAppInvoices.AsNoTracking()
        .OrderByDescending(j => j.Id).Take(100).Select(j => new
        {
            j.Id, j.PaymentAttemptId, j.InvoiceNumber, j.Status, j.AttemptCount, j.ProviderMessageId,
            j.LastErrorCode, j.NextAttemptAtUtc, j.LastEventAtUtc
        }).ToListAsync(ct));

    public sealed class ReconcileRequest
    {
        [Required, MinLength(10), MaxLength(1000)] public string Evidence { get; set; } = "";
        [MaxLength(200)] public string? ProviderMessageId { get; set; }
        public bool ConfirmedNotAccepted { get; set; }
    }

    [HttpPost("admin/invoices/{id:int}/reconcile")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Reconcile(int id, ReconcileRequest request, CancellationToken ct)
    {
        var hasId = !string.IsNullOrWhiteSpace(request.ProviderMessageId);
        if (hasId == request.ConfirmedNotAccepted) return BadRequest("Supply the accepted provider message ID OR confirm non-acceptance using provider records.");
        var query = db.WhatsAppInvoices.Where(j => j.Id == id && j.Status == WhatsAppInvoiceStatus.RequiresReconciliation);
        if (request.ConfirmedNotAccepted) query = query.Where(j => j.AttemptCount < options.Value.MaxAttempts);
        if (hasId && await db.WhatsAppInvoices.AnyAsync(j => j.ProviderMessageId == request.ProviderMessageId, ct))
            return Conflict("This provider message is already assigned.");
        var evidence = $"Admin:{User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value}; {request.Evidence}";
        evidence = evidence[..Math.Min(evidence.Length, 1000)];
        var updated = await query.ExecuteUpdateAsync(s => s
            .SetProperty(j => j.Status, hasId ? WhatsAppInvoiceStatus.Accepted : WhatsAppInvoiceStatus.Pending)
            .SetProperty(j => j.ProviderMessageId, hasId ? request.ProviderMessageId : null)
            .SetProperty(j => j.ReconciliationEvidence, evidence).SetProperty(j => j.LastErrorCode, (string?)null)
            .SetProperty(j => j.LeaseOwner, (string?)null).SetProperty(j => j.LeaseUntilUtc, (DateTime?)null)
            .SetProperty(j => j.NextAttemptAtUtc, DateTime.UtcNow), ct);
        if (updated == 0) return Conflict("Only unresolved sends with remaining retry attempts can be reconciled.");
        if (hasId) await processor.ApplyEventsAsync(request.ProviderMessageId!, ct);
        return Ok();
    }
}
