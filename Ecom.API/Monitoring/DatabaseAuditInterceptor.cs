using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Ecom.API.Monitoring;

// Records entity identity, operation and enum status changes, never customer fields or SQL parameters.
// A successful SaveChanges inside an outer transaction can still be rolled back later.
public sealed class DatabaseAuditInterceptor(ILogger<DatabaseAuditInterceptor> logger) : SaveChangesInterceptor
{
    private (EntityEntry Entry, string Operation, string? FromStatus, string? ToStatus)[] _changes = [];
    private void Capture(DbContext? context)
    {
        _changes = context?.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => (e, e.State.ToString(), EnumStatus(e, true), EnumStatus(e, false))).ToArray() ?? [];
    }
    private void Record()
    {
        foreach (var change in _changes)
        {
            var key = change.Entry.Metadata.FindPrimaryKey();
            var value = key?.Properties.Count == 1 ? change.Entry.Property(key.Properties[0].Name).CurrentValue : null;
            // Identity IDs may be GUID strings; arbitrary string keys are not customer-safe metadata.
            var id = value is int or long or Guid ? value.ToString() :
                value is string s && Guid.TryParse(s, out var guid) ? guid.ToString() : null;
            logger.LogInformation("Database save: {Operation} {EntityType} {EntityId}; status {FromStatus} to {ToStatus}",
                change.Operation, change.Entry.Metadata.ClrType.Name, id, change.FromStatus, change.ToStatus);
        }
        _changes = [];
    }
    private static string? EnumStatus(EntityEntry entry, bool original)
    {
        var status = entry.Metadata.FindProperty("Status");
        if (status == null || !status.ClrType.IsEnum) return null;
        var property = entry.Property(status.Name);
        return (original && entry.State != EntityState.Added ? property.OriginalValue : property.CurrentValue)?.ToString();
    }
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    { Capture(eventData.Context); return result; }
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    { Capture(eventData.Context); return ValueTask.FromResult(result); }
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result) { Record(); return result; }
    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default) { Record(); return ValueTask.FromResult(result); }
    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    { _changes = []; logger.LogError(eventData.Exception, "Database save failed"); }
    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    { SaveChangesFailed(eventData); return Task.CompletedTask; }
}
