namespace Ecom.API.Monitoring;

public sealed class MonitoringOptions
{
    public string Directory { get; set; } = "logs";
    public int RetentionDays { get; set; } = 3;
    public int MaxFiles { get; set; } = 100;
    public long MaxFileBytes { get; set; } = 10 * 1024 * 1024;
    public int RecentEventLimit { get; set; } = 5000;
    public int QueueCapacity { get; set; } = 10000;
    public int HealthIntervalSeconds { get; set; } = 30;
    public int SlowRequestMilliseconds { get; set; } = 2000;
}

public sealed record MonitoringEvent(
    string Id, DateTimeOffset Timestamp, string Level, string Category, string Message,
    string? TraceId, string? UserId, string? ExceptionType, IReadOnlyDictionary<string, string?> Properties);

public sealed record JobStatus(string Name, string Status, DateTimeOffset LastRun,
    DateTimeOffset? LastSuccess, double StaleAfterSeconds);
