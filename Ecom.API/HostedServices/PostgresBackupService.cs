using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Options;
using Npgsql;
using Ecom.API.Monitoring;

namespace Ecom.API.HostedServices;

public sealed class DatabaseBackupOptions
{
    public bool Enabled { get; set; } = true;
    public string Directory { get; set; } = "/var/backups/aljawhara";
    public int HourUtc { get; set; } = 0;
    public int RetentionCount { get; set; } = 3;
    public int TimeoutMinutes { get; set; } = 60;
    public string PgDumpPath { get; set; } = "/usr/bin/pg_dump";
    public string PgRestorePath { get; set; } = "/usr/bin/pg_restore";
}

// Runs only in Production. Completed files also record the last successful scheduled run,
// so restarts catch up a missed backup and failures never erase a previous backup.
public sealed class PostgresBackupService(
    IOptions<DatabaseBackupOptions> options,
    ILogger<PostgresBackupService> logger, MonitoringState? monitoring = null) : BackgroundService
{
    private readonly DatabaseBackupOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        monitoring?.RegisterJob("PostgreSQL backup scheduler", 1200);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                var scheduledDate = now.Hour >= _options.HourUtc ? now.Date : now.Date.AddDays(-1);
                await BackupOnceAsync(scheduledDate, stoppingToken);
                monitoring?.JobCompleted("PostgreSQL backup scheduler", true);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                monitoring?.JobCompleted("PostgreSQL backup scheduler", false);
                logger.LogError(ex, "PostgreSQL backup failed; retrying in 15 minutes. Previous backups are retained.");
                try { await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                continue;
            }

            try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    public async Task BackupOnceAsync(DateTime scheduledDate, CancellationToken cancellationToken)
    {
        if (OperatingSystem.IsLinux())
            System.IO.Directory.CreateDirectory(_options.Directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        else
            System.IO.Directory.CreateDirectory(_options.Directory);

        // File sharing locks coordinate multiple application processes on the same backup volume.
        await using var backupLock = new FileStream(Path.Combine(_options.Directory, ".backup.lock"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var file = Path.Combine(_options.Directory,
            $"aljawhara-{scheduledDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.dump");
        if (File.Exists(file))
        {
            DeleteOlderBackups(cancellationToken);
            return;
        }

        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Set ConnectionStrings__DefaultConnection for PostgreSQL backups.");
        var connection = new NpgsqlConnectionStringBuilder(connectionString);
        var temporaryFile = file + ".partial";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(_options.TimeoutMinutes));
        try
        {
            await RunToolAsync(_options.PgDumpPath,
                ["--format=custom", "--no-owner", "--no-acl", "--no-password", "--file", temporaryFile],
                connection, timeout.Token);
            if (!File.Exists(temporaryFile) || new FileInfo(temporaryFile).Length == 0)
                throw new IOException("pg_dump did not produce a backup.");

            // Check the archive directory before making the backup visible as completed.
            await RunToolAsync(_options.PgRestorePath, ["--list", temporaryFile], connection, timeout.Token);
            if (OperatingSystem.IsLinux())
                File.SetUnixFileMode(temporaryFile, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.Move(temporaryFile, file);
            logger.LogInformation("PostgreSQL backup completed: {File}, {Bytes} bytes.", file, new FileInfo(file).Length);
            DeleteOlderBackups(cancellationToken);
        }
        finally
        {
            if (File.Exists(temporaryFile)) File.Delete(temporaryFile);
        }
    }

    private void DeleteOlderBackups(CancellationToken cancellationToken)
    {
        // Keep only the newest dates in memory, even when the directory contains many backups.
        var newestDates = new PriorityQueue<DateTime, DateTime>();
        foreach (var (_, date) in EnumerateCompletedBackups())
        {
            cancellationToken.ThrowIfCancellationRequested();
            newestDates.Enqueue(date, date);
            if (newestDates.Count > _options.RetentionCount) newestDates.Dequeue();
        }

        if (newestDates.Count < _options.RetentionCount) return;
        var oldestRetainedDate = newestDates.Peek();
        foreach (var (path, date) in EnumerateCompletedBackups())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (date < oldestRetainedDate) File.Delete(path);
        }
    }

    private IEnumerable<(string Path, DateTime Date)> EnumerateCompletedBackups()
    {
        foreach (var path in System.IO.Directory.EnumerateFiles(_options.Directory, "aljawhara-*.dump"))
        {
            var date = Path.GetFileNameWithoutExtension(path)["aljawhara-".Length..];
            if (DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var backupDate))
                yield return (path, backupDate);
        }
    }

    private static async Task RunToolAsync(string executable, string[] arguments,
        NpgsqlConnectionStringBuilder connection, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        // libpq uses different connection-string syntax. Pass credentials via the child
        // environment so passwords never appear in process arguments or application logs.
        foreach (var key in start.Environment.Keys.Where(k => k.StartsWith("PG", StringComparison.Ordinal)).ToArray())
            start.Environment.Remove(key);
        start.Environment["PGHOST"] = connection.Host;
        start.Environment["PGPORT"] = connection.Port.ToString(CultureInfo.InvariantCulture);
        start.Environment["PGDATABASE"] = connection.Database;
        start.Environment["PGUSER"] = connection.Username;
        start.Environment["PGPASSWORD"] = connection.Password;
        start.Environment["PGCONNECT_TIMEOUT"] = "30";
        start.Environment["PGSSLMODE"] = connection.SslMode switch
        {
            SslMode.VerifyCA => "verify-ca",
            SslMode.VerifyFull => "verify-full",
            _ => connection.SslMode.ToString().ToLowerInvariant()
        };
        if (!string.IsNullOrWhiteSpace(connection.RootCertificate))
            start.Environment["PGSSLROOTCERT"] = connection.RootCertificate;

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start PostgreSQL backup tool.");
        // Use small, fixed-size buffers, and drain until exit (including after cancellation kills
        // the process). Always await both readers before disposing their process/pipe handles.
        const int outputBufferSize = 16 * 1024;
        var stdout = process.StandardOutput.BaseStream.CopyToAsync(Stream.Null, outputBufferSize);
        var stderr = process.StandardError.BaseStream.CopyToAsync(Stream.Null, outputBufferSize);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        finally
        {
            if (!process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) when (process.HasExited) { }
                await process.WaitForExitAsync(CancellationToken.None);
            }
            await Task.WhenAll(stdout, stderr);
        }
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"{Path.GetFileName(executable)} failed with exit code {process.ExitCode}. Check PostgreSQL permissions and client/server versions.");
    }
}
