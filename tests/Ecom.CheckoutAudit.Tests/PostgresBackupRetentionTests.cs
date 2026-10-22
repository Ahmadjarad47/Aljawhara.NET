using System.Globalization;
using Ecom.API.HostedServices;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ecom.CheckoutAudit.Tests;

public sealed class PostgresBackupRetentionTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"ecom-backup-retention-{Guid.NewGuid():N}");

    public PostgresBackupRetentionTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task Existing_backup_keeps_three_newest_dates_and_ignores_unrelated_files()
    {
        // Creation order and modification times deliberately disagree with backup dates.
        var dates = new[] { "2026-10-22", "2026-03-01", "2026-10-18", "2025-01-01", "2026-09-02" };
        foreach (var date in dates) Write($"aljawhara-{date}.dump");
        File.SetLastWriteTimeUtc(Path.Combine(_directory, "aljawhara-2025-01-01.dump"), DateTime.UtcNow.AddDays(1));
        var ignored = new[] { "aljawhara-invalid.dump", "aljawhara-2026-02-30.dump", "aljawhara-2026-10-23.dump.partial", "other-2020-01-01.dump" };
        foreach (var name in ignored) Write(name);

        await CreateWorker().BackupOnceAsync(ParseDate("2026-10-22"), TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "aljawhara-2026-09-02.dump", "aljawhara-2026-10-18.dump", "aljawhara-2026-10-22.dump" },
            dates.Select(date => $"aljawhara-{date}.dump")
                .Where(name => File.Exists(Path.Combine(_directory, name))).OrderBy(name => name).ToArray());
        foreach (var name in ignored) Assert.True(File.Exists(Path.Combine(_directory, name)));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(5, 3)]
    public async Task Retention_respects_count_and_keeps_available_backups(int retentionCount, int expectedCount)
    {
        foreach (var date in new[] { "2026-01-01", "2026-05-01", "2026-10-22" })
            Write($"aljawhara-{date}.dump");

        await CreateWorker(retentionCount).BackupOnceAsync(ParseDate("2026-10-22"), TestContext.Current.CancellationToken);
        await CreateWorker(retentionCount).BackupOnceAsync(ParseDate("2026-10-22"), TestContext.Current.CancellationToken);

        Assert.Equal(expectedCount, Directory.GetFiles(_directory, "*.dump").Length);
        Assert.True(File.Exists(Path.Combine(_directory, "aljawhara-2026-10-22.dump")));
    }

    [Fact]
    public async Task Failed_backup_keeps_all_previous_archives_and_removes_partial_file()
    {
        var dates = new[] { "2026-01-01", "2026-05-01", "2026-10-18", "2026-10-21" };
        foreach (var date in dates) Write($"aljawhara-{date}.dump");
        Write("aljawhara-2026-10-22.dump.partial");
        var previousConnection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        try
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Host=localhost;Database=unused;Username=unused");
            await Assert.ThrowsAnyAsync<Exception>(() => CreateWorker().BackupOnceAsync(
                ParseDate("2026-10-22"), TestContext.Current.CancellationToken));

            Assert.Equal(4, Directory.GetFiles(_directory, "*.dump").Length);
            Assert.Empty(Directory.GetFiles(_directory, "*.partial"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", previousConnection);
        }
    }

    private PostgresBackupService CreateWorker(int retentionCount = 3) => new(Options.Create(new DatabaseBackupOptions
    {
        Directory = _directory,
        RetentionCount = retentionCount,
        PgDumpPath = Path.Combine(_directory, "missing-pg-dump")
    }), NullLogger<PostgresBackupService>.Instance);

    private void Write(string name) => File.WriteAllText(Path.Combine(_directory, name), "existing backup");
    private static DateTime ParseDate(string date) => DateTime.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
