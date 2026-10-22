using System.Text.RegularExpressions;
using PhoneNumbers;

namespace Ecom.Infrastructure.WhatsApp;

public sealed class GupshupOptions
{
    public bool Enabled { get; set; }
    public string ApiKey { get; set; } = "";
    public string AppName { get; set; } = "whatsappaljawhara";
    public string Source { get; set; } = "";
    public string TemplateId { get; set; } = "";
    public string PhoneVerificationTemplateId { get; set; } = "";
    public string Environment { get; set; } = "Sandbox";
    public string CountryCode { get; set; } = "965";
    // Legacy configuration retained for compatibility; validation uses international metadata.
    public string NationalNumberPattern { get; set; } = "[569][0-9]{7}";
    public string PublicBaseUrl { get; set; } = "";
    public string WebhookSecret { get; set; } = "";
    public string[] SandboxAllowedDestinations { get; set; } = [];
    // Sandbox session documents require the tester to open the 24-hour conversation first.
    public bool SandboxSessionMessages { get; set; } = true;
    public int MaxAttempts { get; set; } = 5;
    public int MediaLifetimeMinutes { get; set; } = 30;
    public int PdfRetentionDays { get; set; } = 7;
    // Optional installed browser channel; null uses Playwright's bundled Chromium.
    public string? PdfBrowserChannel { get; set; }
    // Empty by default. This incident enables only invoice 6; no bulk recovery.
    public int[] PreparationRetryInvoiceIds { get; set; } = [];

    public bool IsValid()
    {
        if (!Enabled) return true;
        if (PdfBrowserChannel is not (null or "chrome" or "msedge" or "chromium")) return false;
        if (Environment is not ("Sandbox" or "Production") || string.IsNullOrWhiteSpace(ApiKey) ||
            string.IsNullOrWhiteSpace(AppName) || AppName.Length > 100 || !Regex.IsMatch(Source, "^[1-9][0-9]{7,14}$") ||
            !Regex.IsMatch(CountryCode, "^[1-9][0-9]{0,2}$") || WebhookSecret.Length is < 32 or > 512 ||
            !Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out var url) || url.Scheme != "https" ||
            !string.IsNullOrEmpty(url.Query) || !string.IsNullOrEmpty(url.Fragment) || !string.IsNullOrEmpty(url.UserInfo) ||
            MaxAttempts is < 1 or > 10 || MediaLifetimeMinutes is < 5 or > 60 || PdfRetentionDays is < 1 or > 30)
            return false;
        if (Environment == "Production" && (Source == "917834811114" || string.IsNullOrWhiteSpace(TemplateId))) return false;
        if (Environment == "Sandbox" && (SandboxAllowedDestinations.Length == 0 ||
            (!SandboxSessionMessages && string.IsNullOrWhiteSpace(TemplateId)))) return false;
        return true;
    }
}

public static class WhatsAppPhone
{
    private static readonly PhoneNumberUtil Phones = PhoneNumberUtil.GetInstance();

    // CountryCode is only the default for national input. Explicit + / 00 always wins.
    public static string? Normalize(string? value, GupshupOptions options, string? country = null)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 40) return null;
        var text = value.Trim();
        if (text.Any(c => !(char.IsAsciiDigit(c) || c is '+' or ' ' or '-' or '(' or ')'))) return null;
        text = text.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");
        if (text.StartsWith("00", StringComparison.Ordinal)) text = "+" + text[2..];
        if (!Regex.IsMatch(text, @"^\+?[0-9]+$")) return null;
        var region = country?.ToUpperInvariant();
        if (region == null)
        {
            if (!int.TryParse(options.CountryCode, out var code)) return null;
            region = Phones.GetRegionCodeForCountryCode(code);
        }
        if (!Phones.GetSupportedRegions().Contains(region)) return null;
        try
        {
            var callingCode = Phones.GetCountryCodeForRegion(region).ToString();
            if (!text.StartsWith('+') && text.StartsWith(callingCode, StringComparison.Ordinal))
            {
                var explicitPhone = Phones.Parse("+" + text, null);
                if (Phones.IsValidNumber(explicitPhone) && explicitPhone.CountryCode.ToString() == callingCode)
                    return Phones.Format(explicitPhone, PhoneNumberFormat.E164);
            }
            var phone = Phones.Parse(text, region);
            if (!Phones.IsValidNumber(phone) || phone.HasExtension ||
                (!text.StartsWith('+') && !Phones.IsValidNumberForRegion(phone, region))) return null;
            return Phones.Format(phone, PhoneNumberFormat.E164);
        }
        catch (NumberParseException) { return null; }
    }

    public static string? ToProvider(string? value, GupshupOptions options) => Normalize(value, options)?[1..];

    public static bool IsSandboxAllowed(string destination, GupshupOptions options) =>
        options.SandboxAllowedDestinations.Any(allowed =>
            Normalize(allowed.StartsWith('+') || allowed.StartsWith("00") ? allowed : "+" + allowed, options) == destination);
}
