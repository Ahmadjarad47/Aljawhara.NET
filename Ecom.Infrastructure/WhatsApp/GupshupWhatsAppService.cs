using System.Net;
using System.Text.Json;
using Ecom.Domain.Interfaces;
using Microsoft.Extensions.Options;

namespace Ecom.Infrastructure.WhatsApp;

public sealed class GupshupWhatsAppService(HttpClient client, IOptions<GupshupOptions> options) : IGupshupWhatsAppService
{
    public async Task<WhatsAppSendResult> SendInvoiceAsync(string destination, string orderNumber, string mediaUrl,
        CancellationToken cancellationToken)
    {
        var o = options.Value;
        if (!o.Enabled || !o.IsValid() || WhatsAppPhone.Normalize(destination, o) != destination)
            return new(WhatsAppSendOutcome.Rejected, ErrorCode: "INVALID_CONFIGURATION_OR_PHONE");
        if (o.Environment == "Sandbox" && !o.SandboxAllowedDestinations.Contains(destination, StringComparer.Ordinal))
            return new(WhatsAppSendOutcome.Rejected, ErrorCode: "SANDBOX_DESTINATION_NOT_ALLOWED");
        var session = o.Environment == "Sandbox" && o.SandboxSessionMessages;
        var caption = $"مرحباً، تم استلام دفعتك بنجاح للطلب رقم {orderNumber} من الجوهرة بلس. مرفق فاتورتك بصيغة PDF. شكراً لتسوقك معنا.";
        var filename = "invoice-" + new string(orderNumber.Where(c => char.IsAsciiLetterOrDigit(c) || c == '-').Take(60).ToArray()) + ".pdf";
        var form = new Dictionary<string, string>
        {
            ["channel"] = "whatsapp", ["source"] = o.Source, ["destination"] = destination, ["src.name"] = o.AppName,
            ["message"] = session
                ? JsonSerializer.Serialize(new { type = "file", caption, url = mediaUrl, filename })
                : JsonSerializer.Serialize(new { type = "document", document = new { link = mediaUrl, filename } })
        };
        if (!session) form["template"] = JsonSerializer.Serialize(new { id = o.TemplateId, @params = new[] { orderNumber } });
        using var request = new HttpRequestMessage(HttpMethod.Post,
            session ? "https://api.gupshup.io/wa/api/v1/msg" : "https://api.gupshup.io/wa/api/v1/template/msg");
        request.Headers.Add("apikey", o.ApiKey);
        request.Content = new FormUrlEncodedContent(form);
        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            // Only an explicit rejection can be retried. 5xx and transport failures can hide acceptance.
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                return new(WhatsAppSendOutcome.RetryableRejection, ErrorCode: "PROVIDER_RATE_LIMIT");
            if ((int)response.StatusCode is >= 400 and < 500)
                return new(WhatsAppSendOutcome.Rejected, ErrorCode: $"PROVIDER_HTTP_{(int)response.StatusCode}");
            if (!response.IsSuccessStatusCode) return new(WhatsAppSendOutcome.Unknown, ErrorCode: "PROVIDER_UNKNOWN_HTTP");
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return new(WhatsAppSendOutcome.Unknown, ErrorCode: "PROVIDER_UNRECOGNIZED_RESPONSE");
            if (root.TryGetProperty("messageId", out var id) && id.ValueKind == JsonValueKind.String &&
                id.GetString() is { Length: > 0 and <= 200 } messageId &&
                root.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.String &&
                status.GetString() is "submitted" or "success")
                return new(WhatsAppSendOutcome.Accepted, messageId);
            return new(WhatsAppSendOutcome.Unknown, ErrorCode: "PROVIDER_UNRECOGNIZED_RESPONSE");
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
        {
            return new(WhatsAppSendOutcome.Unknown, ErrorCode: "PROVIDER_TRANSPORT_OR_RESPONSE_UNKNOWN");
        }
    }
}
