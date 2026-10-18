using System.Diagnostics;
using Ecom.API.HostedServices;
using Ecom.Domain.Entity;
using Ecom.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Ecom.CheckoutAudit.Tests;

public sealed class PostgresProductionTests
{
    [Fact]
    public async Task Xmin_rejects_a_stale_payment_worker_update()
    {
        var connection = PostgresTestDatabase.ConnectionString($"ecom_xmin_{Guid.NewGuid():N}");
        var options = new DbContextOptionsBuilder<EcomDbContext>().UseNpgsql(connection).Options;
        await using var first = new EcomDbContext(options);
        await using var second = new EcomDbContext(options);
        try
        {
            await first.Database.MigrateAsync(TestContext.Current.CancellationToken);
            first.OutboxMessages.Add(new OutboxMessage { Type = "test", DeduplicationKey = "test", Payload = "{}" });
            await first.SaveChangesAsync(TestContext.Current.CancellationToken);
            var original = await first.OutboxMessages.SingleAsync(TestContext.Current.CancellationToken);
            var stale = await second.OutboxMessages.SingleAsync(TestContext.Current.CancellationToken);
            original.AttemptCount++;
            await first.SaveChangesAsync(TestContext.Current.CancellationToken);
            stale.AttemptCount++;
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync(TestContext.Current.CancellationToken));
        }
        finally { await first.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken); }
    }

    [Fact]
    public async Task Backup_round_trip_preserves_data_and_failure_keeps_previous_archive()
    {
        var connection = PostgresTestDatabase.ConnectionString($"ecom_backup_{Guid.NewGuid():N}");
        var restoredConnection = new NpgsqlConnectionStringBuilder(connection) { Database = $"ecom_restore_{Guid.NewGuid():N}" }.ConnectionString;
        var options = new DbContextOptionsBuilder<EcomDbContext>().UseNpgsql(connection).Options;
        var restoreOptions = new DbContextOptionsBuilder<EcomDbContext>().UseNpgsql(restoredConnection).Options;
        var directory = Path.Combine(Path.GetTempPath(), $"ecom-backup-{Guid.NewGuid():N}");
        var binaryDirectory = Environment.GetEnvironmentVariable("ECOM_TEST_PG_BIN") ?? "/usr/bin";
        var suffix = OperatingSystem.IsWindows() ? ".exe" : "";
        var settings = new DatabaseBackupOptions
        {
            Directory = directory,
            RetentionDays = 2,
            PgDumpPath = Path.Combine(binaryDirectory, "pg_dump" + suffix),
            PgRestorePath = Path.Combine(binaryDirectory, "pg_restore" + suffix)
        };
        var previousEnvironment = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        await using var context = new EcomDbContext(options);
        await using var restored = new EcomDbContext(restoreOptions);
        try
        {
            await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
            context.Categories.Add(new Category { Name = "Backup العربية", NameAr = "اختبار" });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", connection);
            var worker = new PostgresBackupService(Options.Create(settings), NullLogger<PostgresBackupService>.Instance);
            await worker.BackupOnceAsync(DateTime.UtcNow.Date, TestContext.Current.CancellationToken);
            var archive = Assert.Single(Directory.GetFiles(directory, "*.dump"));
            var lastWritten = File.GetLastWriteTimeUtc(archive);
            await worker.BackupOnceAsync(DateTime.UtcNow.Date, TestContext.Current.CancellationToken);
            Assert.Equal(lastWritten, File.GetLastWriteTimeUtc(archive));

            // Keep an expired archive on failure, then prune it only after a successful run.
            var oldArchive = Path.Combine(directory, $"aljawhara-{DateTime.UtcNow.AddDays(-5):yyyy-MM-dd}.dump");
            File.Copy(archive, oldArchive);
            var dumpExecutable = settings.PgDumpPath;
            settings.PgDumpPath = Path.Combine(directory, "missing-pg-dump");
            await Assert.ThrowsAnyAsync<Exception>(() => worker.BackupOnceAsync(DateTime.UtcNow.AddDays(1).Date, TestContext.Current.CancellationToken));
            Assert.True(File.Exists(oldArchive));
            Assert.True(File.Exists(archive));
            Assert.Empty(Directory.GetFiles(directory, "*.partial"));
            settings.PgDumpPath = dumpExecutable;
            await worker.BackupOnceAsync(DateTime.UtcNow.AddDays(1).Date, TestContext.Current.CancellationToken);
            Assert.False(File.Exists(oldArchive));

            // Create only the database, then restore the actual dump (schema + data).
            await restored.GetService<Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator>()
                .CreateAsync(TestContext.Current.CancellationToken);
            var target = new NpgsqlConnectionStringBuilder(restoredConnection);
            var start = new ProcessStartInfo(settings.PgRestorePath) { UseShellExecute = false, CreateNoWindow = true };
            foreach (var argument in new[] { "--host", target.Host!, "--port", target.Port.ToString(), "--username", target.Username!, "--dbname", target.Database!, "--no-owner", "--no-acl", "--exit-on-error", "--single-transaction", archive })
                start.ArgumentList.Add(argument);
            start.Environment["PGPASSWORD"] = target.Password;
            start.Environment["PGSSLMODE"] = "disable";
            using var process = Process.Start(start)!;
            await process.WaitForExitAsync(TestContext.Current.CancellationToken);
            Assert.Equal(0, process.ExitCode);
            Assert.Equal("Backup العربية", (await restored.Categories.SingleAsync(TestContext.Current.CancellationToken)).Name);
            Assert.Empty(await restored.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", previousEnvironment);
            await context.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
            await restored.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
