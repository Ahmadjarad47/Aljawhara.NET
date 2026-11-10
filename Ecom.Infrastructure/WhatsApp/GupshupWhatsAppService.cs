using System.Net;
using System.Text.Json;
using Ecom.Domain.Interfaces;
using Microsoft.Extensions.Options;

namespace Ecom.Infrastructure.WhatsApp;

public sealed class GupshupWhatsAppService(HttpClient client, IOptions<GupshupOptions> options) : IGupshupWhatsAppService
{
    public Task<WhatsAppSendResult> SendInvoiceAsync(string destination, string orderNumber, string mediaUrl,
        CancellationToken cancellationToken) =>
        Task.FromResult(new WhatsAppSendResult(WhatsAppSendOutcome.Rejected, ErrorCode: "INVOICE_DELIVERY_DISABLED"));

    public async Task<WhatsAppSendResult> SendPhoneVerificationCodeAsync(string destination, string code,
        CancellationToken cancellationToken)
    {
        var o = options.Value;
        if (!o.Enabled || !o.IsValid() || WhatsAppPhone.Normalize(destination, o) != destination ||
            code.Length != 6 || !code.All(char.IsAsciiDigit))
            return new(WhatsAppSendOutcome.Rejected, ErrorCode: "INVALID_CONFIGURATION_OR_PHONE");
        if (o.Environment == "Sandbox" && !WhatsAppPhone.IsSandboxAllowed(destination, o))
            return new(WhatsAppSendOutcome.Rejected, ErrorCode: "SANDBOX_DESTINATION_NOT_ALLOWED");

        var session = o.Environment == "Sandbox" && o.SandboxSessionMessages;
        if (!session && string.IsNullOrWhiteSpace(o.PhoneVerificationTemplateId))
            return new(WhatsAppSendOutcome.Rejected, ErrorCode: "PHONE_VERIFICATION_TEMPLATE_NOT_CONFIGURED");
        var form = new Dictionary<string, string>
        {
            ["channel"] = "whatsapp", ["source"] = o.Source, ["destination"] = WhatsAppPhone.ToProvider(destination, o)!, ["src.name"] = o.AppName
        };
        if (session)
            form["message"] = JsonSerializer.Serialize(new { type = "text", text = $"رمز التحقق من رقم واتساب للجوهرة بلس هو: {code}" });
        else
            form["template"] = JsonSerializer.Serialize(new { id = o.PhoneVerificationTemplateId, @params = new[] { code } });
        using var request = new HttpRequestMessage(HttpMethod.Post,
            session ? "https://api.gupshup.io/wa/api/v1/msg" : "https://api.gupshup.io/wa/api/v1/template/msg");
        request.Headers.Add("apikey", o.ApiKey);
        request.Content = new FormUrlEncodedContent(form);
        return await SendAsync(request, cancellationToken);
    }

    private async Task<WhatsAppSendResult> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
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
