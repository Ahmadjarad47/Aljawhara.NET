using System.ComponentModel.DataAnnotations;
using Ecom.API.Monitoring;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecom.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/monitoring")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminMonitoringController(MonitoringStore store, MonitoringState state) : ControllerBase
{
    [HttpGet("summary")]
    public IActionResult Summary() => Ok(state.Summary(store));

    [HttpGet("logs")]
    public IActionResult Logs([FromQuery] MonitoringLogQuery query)
    {
        var source = store.Snapshot();
        var filtered = source.Where(e =>
            (query.Level == null || e.Level.Equals(query.Level, StringComparison.OrdinalIgnoreCase)) &&
            (query.Category == null || e.Category.Contains(query.Category, StringComparison.OrdinalIgnoreCase)) &&
            (query.TraceId == null || e.TraceId == query.TraceId) &&
            (query.From == null || e.Timestamp >= query.From) && (query.To == null || e.Timestamp <= query.To) &&
            (query.Search == null || e.Message.Contains(query.Search, StringComparison.OrdinalIgnoreCase) ||
                e.Category.Contains(query.Search, StringComparison.OrdinalIgnoreCase) ||
                e.TraceId?.Contains(query.Search, StringComparison.OrdinalIgnoreCase) == true ||
                e.ExceptionType?.Contains(query.Search, StringComparison.OrdinalIgnoreCase) == true)).ToArray();
        return Ok(new { items = filtered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize),
            totalCount = filtered.Length, query.Page, query.PageSize, retainedCount = source.Length,
            recentEventLimit = store.Options.RecentEventLimit, oldestAvailableAt = source.LastOrDefault()?.Timestamp });
    }
}

public sealed class MonitoringLogQuery : IValidatableObject
{
    [RegularExpression("(?i)^(Information|Warning|Error|Critical)$")] public string? Level { get; set; }
    [StringLength(200)] public string? Category { get; set; }
    [StringLength(200)] public string? Search { get; set; }
    [StringLength(128)] public string? TraceId { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    [Range(1, 1000)] public int Page { get; set; } = 1;
    [Range(1, 200)] public int PageSize { get; set; } = 50;
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (From > To) yield return new ValidationResult("From must be before To.", [nameof(From), nameof(To)]);
    }
}
