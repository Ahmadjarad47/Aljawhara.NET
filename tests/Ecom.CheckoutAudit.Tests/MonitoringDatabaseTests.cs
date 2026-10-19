using System.Text.Json;
using Ecom.API.Monitoring;
using Ecom.Domain.Entity;
using Ecom.Domain.constant;
using Ecom.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ecom.CheckoutAudit.Tests;

public sealed class MonitoringDatabaseTests
{
    [Fact]
    public async Task Database_activity_records_generated_ids_and_status_changes_without_customer_fields()
    {
        var connection = PostgresTestDatabase.ConnectionString("ecom_monitoring_" + Guid.NewGuid().ToString("N"));
        var store = new MonitoringStore(Options.Create(new MonitoringOptions()), new MonitoringTestEnvironment());
        using var provider = new MonitoringLoggerProvider(store, new HttpContextAccessor());
        using var loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(provider));
        var interceptor = new DatabaseAuditInterceptor(loggerFactory.CreateLogger<DatabaseAuditInterceptor>());
        var options = new DbContextOptionsBuilder<EcomDbContext>().UseNpgsql(connection).AddInterceptors(interceptor).Options;
        await using var context = new EcomDbContext(options);
        try
        {
            await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
            var category = new Category { Name = "private-customer-value", NameAr = "private-arabic-value" };
            var message = new OutboxMessage { Type = "Test", DeduplicationKey = Guid.NewGuid().ToString(), Payload = "private-outbox-data" };
            context.AddRange(category, message);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            Assert.Contains(store.Snapshot(), e => e.Properties.GetValueOrDefault("EntityType") == nameof(Category)
                && e.Properties.GetValueOrDefault("EntityId") == category.Id.ToString());
            message.Status = OutboxStatus.Processed;
            context.SaveChanges();
            Assert.Contains(store.Snapshot(), e => e.Properties.GetValueOrDefault("FromStatus") == "Pending"
                && e.Properties.GetValueOrDefault("ToStatus") == "Processed");
            category.Name = null!;
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));
            Assert.Contains(store.Snapshot(), e => e.Level == "Error" && e.Message == "Database save failed");
            var serialized = JsonSerializer.Serialize(store.Snapshot());
            foreach (var privateValue in new[] { "private-customer-value", "private-arabic-value", "private-outbox-data" })
                Assert.DoesNotContain(privateValue, serialized);
        }
        finally { await context.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken); }
    }
}
