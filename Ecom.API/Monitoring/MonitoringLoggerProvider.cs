using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace Ecom.API.Monitoring;

[ProviderAlias("AdminMonitoring")]
public sealed class MonitoringLoggerProvider(MonitoringStore store, IHttpContextAccessor accessor) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new EventLogger(categoryName, store, accessor);
    public void Dispose() { }

    private sealed class EventLogger(string category, MonitoringStore store, IHttpContextAccessor accessor) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => level != LogLevel.None &&
            level >= (category.StartsWith("Ecom.", StringComparison.Ordinal) ? LogLevel.Information : LogLevel.Warning);

        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(level)) return;
            var fields = state as IEnumerable<KeyValuePair<string, object?>>;
            var properties = new Dictionary<string, string?>();
            var template = "Application event";
            foreach (var field in fields ?? [])
            {
                if (field.Key == "{OriginalFormat}") template = field.Value?.ToString() ?? template;
                else properties[field.Key] = category.StartsWith("Ecom.", StringComparison.Ordinal)
                    ? LogPrivacy.Value(field.Key, field.Value) : "[redacted]";
            }
            // Framework templates can include SQL or URI data; retain category/event ID only.
            var message = category.StartsWith("Ecom.", StringComparison.Ordinal)
                ? Regex.Replace(template, @"\{([^{}:,]+)(?:[^{}]*)\}", m =>
                    properties.GetValueOrDefault(m.Groups[1].Value) ?? "[redacted]")
                : $"Framework event {eventId.Id}. See category and exception type.";
            var context = accessor.HttpContext;
            store.Add(new MonitoringEvent(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow,
                level.ToString(), category, LogPrivacy.Text(message), context?.TraceIdentifier ?? Activity.Current?.TraceId.ToString(),
                LogPrivacy.Value("UserId", context?.User.FindFirstValue(ClaimTypes.NameIdentifier)),
                exception?.GetType().FullName, properties));
        }
    }
}

public static class LogPrivacy
{
    private static readonly HashSet<string> MoneyFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "ExpectedAmount", "ExpectedGrossAmount", "ExpectedDiscountAmount",
        "GatewayAmount", "GatewayTotal", "GatewaySubtotal", "GatewayFees"
    };
    // Deliberately allowlist metadata. New application fields are private by default.
    private static readonly HashSet<string> SafeFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "TraceId", "UserId", "OrderId", "PaymentAttemptId", "MessageId", "TransactionId",
        "StatusCode", "DurationMs", "Method", "Route", "Host", "Type", "EntityType", "Operation",
        "Count", "Rows", "Job", "Status", "Minutes", "Bytes", "Length", "ReasonCode", "FromStatus", "ToStatus", "EntityId"
    };

    public static string? Value(string name, object? value)
    {
        if (value == null) return null;
        // These fields contain numeric verification diagnostics, never response bodies.
        if (MoneyFields.Contains(name))
            return value is decimal amount ? amount.ToString(CultureInfo.InvariantCulture) : "[redacted]";
        if (!SafeFields.Contains(name)) return "[redacted]";
        return Text(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "");
    }

    public static string Text(string value)
    {
        value = Regex.Replace(value, @"(?i)Bearer\s+\S+|\beyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+", "[redacted]");
        value = Regex.Replace(value, @"[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}", "[redacted]");
        value = Regex.Replace(value, @"(?i)(password|secret|token|api[_-]?key|authorization)\s*[:=]\s*[^\s,;]+", "$1=[redacted]");
        value = Regex.Replace(value, @"https?://[^\s]+", m => Uri.TryCreate(m.Value, UriKind.Absolute, out var uri) ? uri.GetLeftPart(UriPartial.Authority) : "[redacted-url]");
        value = value.Replace('\r', ' ').Replace('\n', ' ');
        return value.Length > 2000 ? value[..2000] : value;
    }
}
