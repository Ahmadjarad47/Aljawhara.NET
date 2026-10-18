using Ecom.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ecom.CheckoutAudit.Tests;

public class MigrationTests
{
    [Fact]
    public async Task Phase1_security_migration_applies_guest_fields_and_inventory_constraints()
    {
        var databaseName = $"EcomCheckoutMigration_{Guid.NewGuid():N}";
        var connectionString = $"Server=(localdb)\\MSSQLLocalDB;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<EcomDbContext>().UseSqlServer(connectionString).Options;

        await using var context = new EcomDbContext(options);
        try
        {
            await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
            await context.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = """
                SELECT
                    (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('Orders') AND name = 'GuestAccessTokenHash') +
                    (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('Orders') AND name = 'GuestAccessExpiresAtUtc') +
                    (SELECT COUNT(*) FROM sys.check_constraints WHERE name = 'CK_OrderItems_Quantity_Positive') +
                    (SELECT COUNT(*) FROM sys.check_constraints WHERE name = 'CK_Products_TotalInStock_NonNegative')
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
