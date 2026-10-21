using Ecom.Domain.Entity;
using Ecom.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Ecom.CheckoutAudit.Tests;

public sealed class WhatsAppPhoneMigrationTests
{
    [Fact]
    public async Task Upgrade_preserves_existing_consent_and_never_grants_missing_consent()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = $"WhatsAppMigration_{Guid.NewGuid():N}";
        await using var db = new EcomDbContext(new DbContextOptionsBuilder<EcomDbContext>()
            .UseNpgsql(PostgresTestDatabase.ConnectionString(database)).Options);
        try
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261021113339_WhatsAppCustomerConsent", ct);
            var grantedAt = DateTime.UtcNow;
            var owner = new AppUsers { Id = "migration-owner", UserName = "migration-owner",
                PhoneNumber = "96550000000", PhoneNumberConfirmed = true, WhatsAppNotificationsEnabled = true,
                WhatsAppConsentPhone = "96550000000", WhatsAppConsentAtUtc = grantedAt };
            var withConsent = new Order { OrderNumber = "MIGRATION-CONSENT", AppUser = owner,
                ShippingAddress = new ShippingAddress { FullName = "Owner", Phone = "50000000", Street = "Street" },
                WhatsAppConsentPhone = "96550000000", WhatsAppConsentAtUtc = grantedAt, WhatsAppConsentSource = "Checkout" };
            var withoutConsent = new Order { OrderNumber = "MIGRATION-NO-CONSENT",
                ShippingAddress = new ShippingAddress { FullName = "Guest", Phone = "50000000", Street = "Street" } };
            db.AddRange(withConsent, withoutConsent);
            db.WhatsAppConsentAudits.Add(new WhatsAppConsentAudit { AppUser = owner, Order = withConsent,
                Phone = "96550000000", Source = "Checkout", Action = WhatsAppConsentAction.Granted, OccurredAtUtc = grantedAt });
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
            await migrator.MigrateAsync(cancellationToken: ct);
            var orders = await db.Orders.AsNoTracking().ToListAsync(ct);
            Assert.Equal("+96550000000", orders.Single(o => o.Id == withConsent.Id).WhatsAppConsentPhone);
            var absent = orders.Single(o => o.Id == withoutConsent.Id);
            Assert.Null(absent.WhatsAppConsentPhone);
            Assert.Null(absent.WhatsAppConsentAtUtc);
            Assert.Null(absent.WhatsAppConsentSource);
            Assert.Equal("+96550000000", await db.Users.Select(u => u.WhatsAppConsentPhone).SingleAsync(ct));
            Assert.Equal("+96550000000", await db.WhatsAppConsentAudits.Select(a => a.Phone).SingleAsync(ct));
            Assert.Single(await db.WhatsAppConsentAudits.ToListAsync(ct));
            // Rollback removes the marker before narrowing columns; upgrade is repeatable.
            await migrator.MigrateAsync("20261021113339_WhatsAppCustomerConsent", ct);
            await migrator.MigrateAsync(cancellationToken: ct);
            Assert.Equal("+96550000000", await db.WhatsAppConsentAudits.Select(a => a.Phone).SingleAsync(ct));
        }
        finally { await db.Database.EnsureDeletedAsync(ct); }
    }
}
