using Ecom.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ecom.CheckoutAudit.Tests;

public class MigrationTests
{
    [Fact]
    public async Task Phase1_security_migration_applies_guest_fields_and_inventory_constraints()
    {
        var databaseName = $"EcomCheckoutMigration_{Guid.NewGuid():N}";
        var connectionString = PostgresTestDatabase.ConnectionString(databaseName);
        var options = new DbContextOptionsBuilder<EcomDbContext>().UseNpgsql(connectionString).Options;

        await using var context = new EcomDbContext(options);
        try
        {
            Assert.False(context.Database.HasPendingModelChanges());
            await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
            await context.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = """
                SELECT
                    (SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Orders' AND column_name = 'GuestAccessTokenHash') +
                    (SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Orders' AND column_name = 'GuestAccessExpiresAtUtc') +
                    (SELECT COUNT(*) FROM pg_constraint WHERE conname = 'CK_OrderItems_Quantity_Positive') +
                    (SELECT COUNT(*) FROM pg_constraint WHERE conname = 'CK_Products_TotalInStock_NonNegative')
                """;
            var count = Convert.ToInt32(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
            Assert.Equal(4, count);
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
            await context.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
        }
    }
}
