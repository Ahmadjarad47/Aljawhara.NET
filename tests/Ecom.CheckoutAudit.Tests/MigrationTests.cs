using Ecom.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Ecom.CheckoutAudit.Tests;

public class MigrationTests
{
    [Fact]
    public async Task P1_upgrade_preserves_existing_account_and_coupon_but_retires_legacy_refresh_material()
    {
        var ct = TestContext.Current.CancellationToken;
        var connection = PostgresTestDatabase.ConnectionString($"ecom_p1_upgrade_{Guid.NewGuid():N}");
        await using var db = new EcomDbContext(new DbContextOptionsBuilder<EcomDbContext>().UseNpgsql(connection).Options);
        try
        {
            await db.GetService<IMigrator>().MigrateAsync("20261021133301_WhatsAppPreparationRetryAudit", ct);
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO "AspNetUsers" ("Id", "Email", "EmailConfirmed", "PasswordHash", "SecurityStamp",
                    "CreatedAt", "IsActive", "PhoneNumberConfirmed", "TwoFactorEnabled", "LockoutEnabled", "AccessFailedCount",
                    "RefreshToken", "RefreshTokenExpiresAtUtc")
                VALUES ('synthetic-upgrade', 'legacy@example.test', true, 'synthetic-password-hash', 'synthetic-stamp',
                    now(), true, false, false, true, 0, 'synthetic-legacy-token', now() + interval '1 day');
                INSERT INTO "Coupons" ("Code", "Description", "Type", "Value", "StartDate", "EndDate", "UsageLimit",
                    "UsedCount", "IsActive", "IsSingleUse", "AppUserId", "CreatedAt", "CreatedBy", "IsDeleted")
                VALUES ('LEGACY', 'Synthetic coupon', 1, 5, now() - interval '1 day', now() + interval '1 day', 8,
                    3, true, false, 'synthetic-upgrade', now(), 'fixture', false);
                """, ct);

            await db.Database.MigrateAsync(ct);
            var user = await db.Users.AsNoTracking().SingleAsync(ct);
            Assert.Equal("synthetic-password-hash", user.PasswordHash);
            Assert.Equal("synthetic-stamp", user.SecurityStamp);
            Assert.True(user.EmailConfirmed);
            var coupon = await db.Coupons.AsNoTracking().SingleAsync(ct);
            Assert.Equal("LEGACY", coupon.Code);
            Assert.Equal(user.Id, coupon.AppUserId);
            Assert.Equal(3, coupon.UsedCount);
            Assert.True(coupon.IsActive);
            Assert.False(coupon.IsPublic);
            Assert.Empty(await db.AuthSessions.ToListAsync(ct));
            Assert.Empty(await db.AccountChallenges.ToListAsync(ct));
            Assert.Empty(await db.Database.SqlQueryRaw<string>("""
                SELECT column_name AS "Value" FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = 'AspNetUsers'
                    AND column_name IN ('RefreshToken', 'RefreshTokenExpiresAtUtc')
                """).ToListAsync(ct));
        }
        finally { await db.Database.EnsureDeletedAsync(ct); }
    }

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
