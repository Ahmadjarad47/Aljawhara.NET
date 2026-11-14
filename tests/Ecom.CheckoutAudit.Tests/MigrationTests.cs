using Ecom.Infrastructure.Data;
using Ecom.Domain.Entity;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Ecom.CheckoutAudit.Tests;

public class MigrationTests
{
    [Fact]
    public async Task Address_upgrade_keeps_existing_order_links_and_supports_multiple_addresses_per_customer()
    {
        var ct = TestContext.Current.CancellationToken;
        var connection = PostgresTestDatabase.ConnectionString($"ecom_address_upgrade_{Guid.NewGuid():N}");
        await using var db = new EcomDbContext(new DbContextOptionsBuilder<EcomDbContext>().UseNpgsql(connection).Options);
        var migrator = db.GetService<IMigrator>();
        const string previous = "20261114083000_P2CatalogIntegrity";
        const string target = "20261114120000_AllowMultipleCustomerShippingAddresses";
        try
        {
            await migrator.MigrateAsync(previous, ct);
            var user = new AppUsers { UserName = "Synthetic address owner" };
            var original = new ShippingAddress { FullName = "Existing recipient", AppUser = user };
            var order = new Order { OrderNumber = "ADDRESS-MIGRATION-FIXTURE", ShippingAddress = original };
            db.Orders.Add(order);
            await db.SaveChangesAsync(ct);

            await db.Database.MigrateAsync(ct);
            db.ShippingAddresses.Add(new ShippingAddress { FullName = "Second recipient", AppUserId = user.Id });
            await db.SaveChangesAsync(ct);
            Assert.Equal(2, await db.ShippingAddresses.CountAsync(a => a.AppUserId == user.Id, ct));
            Assert.Equal(original.Id, await db.Orders.Where(o => o.Id == order.Id)
                .Select(o => o.ShippingAddressId).SingleAsync(ct));
            Assert.Equal("Existing recipient", await db.ShippingAddresses.Where(a => a.Id == original.Id)
                .Select(a => a.FullName).SingleAsync(ct));

            var rollback = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(previous, ct));
            Assert.Equal(PostgresErrorCodes.UniqueViolation, rollback.SqlState);
            Assert.Contains(target, await db.Database.GetAppliedMigrationsAsync(ct));
            Assert.Equal(2, await db.ShippingAddresses.CountAsync(a => a.AppUserId == user.Id, ct));
        }
        finally { await db.Database.EnsureDeletedAsync(ct); }
    }

    [Fact]
    public async Task Failed_catalog_upgrade_rolls_back_schema_and_history_and_can_be_retried()
    {
        var ct = TestContext.Current.CancellationToken;
        var connection = PostgresTestDatabase.ConnectionString($"ecom_p2_retry_{Guid.NewGuid():N}");
        await using var db = new EcomDbContext(new DbContextOptionsBuilder<EcomDbContext>().UseNpgsql(connection).Options);
        const string previous = "20261112093003_P1AccountAndPaymentSecurity";
        const string target = "20261114083000_P2CatalogIntegrity";
        try
        {
            await db.GetService<IMigrator>().MigrateAsync(previous, ct);
            db.Categories.Add(new Category { Name = "Migration recovery fixture" });
            await db.SaveChangesAsync(ct);
            // A pre-existing index name causes PostgreSQL to fail after the migration's column adds.
            await db.Database.ExecuteSqlRawAsync("""
                CREATE INDEX "UX_Ratings_Product_Reviewer" ON "Ratings" ("Id");
                """, ct);
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.MigrateAsync(ct));
            Assert.Equal(PostgresErrorCodes.DuplicateTable, error.SqlState);
            Assert.DoesNotContain(target, await db.Database.GetAppliedMigrationsAsync(ct));
            Assert.Empty(await db.Database.SqlQueryRaw<string>("""
                SELECT column_name AS "Value" FROM information_schema.columns
                WHERE table_schema = 'public' AND
                    ((table_name = 'Products' AND column_name = 'ImageOwnerId') OR
                     (table_name = 'Ratings' AND column_name = 'ReviewerUserId'))
                """).ToListAsync(ct));
            Assert.Equal("Migration recovery fixture", await db.Categories.Select(c => c.Name).SingleAsync(ct));

            await db.Database.ExecuteSqlRawAsync("""DROP INDEX "UX_Ratings_Product_Reviewer";""", ct);
            await db.Database.MigrateAsync(ct);
            Assert.Contains(target, await db.Database.GetAppliedMigrationsAsync(ct));
            Assert.Equal("Migration recovery fixture", await db.Categories.Select(c => c.Name).SingleAsync(ct));
        }
        finally { await db.Database.EnsureDeletedAsync(ct); }
    }

    [Fact]
    public async Task P2_upgrade_preserves_legacy_images_and_ratings_and_adds_reviewer_uniqueness()
    {
        var ct = TestContext.Current.CancellationToken;
        var connection = PostgresTestDatabase.ConnectionString($"ecom_p2_upgrade_{Guid.NewGuid():N}");
        await using var db = new EcomDbContext(new DbContextOptionsBuilder<EcomDbContext>().UseNpgsql(connection).Options);
        try
        {
            await db.GetService<IMigrator>().MigrateAsync("20261112093003_P1AccountAndPaymentSecurity", ct);
            var category = new Category { Name = "Legacy category" };
            var product = new Product { Title = "Legacy product", subCategory = new SubCategory { Name = "Legacy", Category = category },
                Images = ["https://assets.example.test/products/legacy.png"] };
            var author = new AppUsers { Id = "legacy-author", UserName = "LegacyAuthor" };
            db.AddRange(category, product.subCategory, author);
            await db.SaveChangesAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Products" ("Id", "Title", "TitleAr", "Description", "DescriptionAr", "SubCategoryId",
                    "Images", "oldPrice", "newPrice", "TotalInStock", "IsInStock", "CreatedAt", "CreatedBy", "IsActive", "IsDeleted")
                VALUES (42, {product.Title}, '', '', '', {product.subCategory.Id}, {product.Images}, 1, 1, 1, true, now(), 'System', true, false);
                INSERT INTO "Ratings" ("Content", "RatingNumber", "ProductId", "CreatedAt", "CreatedBy", "IsDeleted", "IsActive")
                VALUES ('legacy rating 1', 3, 42, now(), {author.Id}, false, true),
                       ('legacy rating 2', 5, 42, now(), {author.Id}, false, true);
                """, ct);

            await db.Database.MigrateAsync(ct);
            var legacy = await db.Products.Include(p => p.Ratings).SingleAsync(p => p.Id == 42, ct);
            Assert.Equal(product.Images, legacy.Images);
            Assert.Null(legacy.ImageOwnerId);
            Assert.Equal(2, legacy.Ratings.Count);
            Assert.Equal(4d, legacy.Ratings.Average(r => r.RatingNumber));
            Assert.All(legacy.Ratings, r => { Assert.Null(r.ReviewerUserId); Assert.Equal(author.Id, r.CreatedBy); });

            db.Ratings.Add(new Rating { ProductId = 42, ReviewerUserId = author.Id, Content = "Verified", RatingNumber = 4 });
            await db.SaveChangesAsync(ct);
            db.Ratings.Add(new Rating { ProductId = 42, ReviewerUserId = author.Id, Content = "Duplicate", RatingNumber = 5 });
            var duplicate = Assert.IsType<PostgresException>((await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(ct))).InnerException);
            Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);
            Assert.Equal("UX_Ratings_Product_Reviewer", duplicate.ConstraintName);
        }
        finally { await db.Database.EnsureDeletedAsync(ct); }
    }

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
