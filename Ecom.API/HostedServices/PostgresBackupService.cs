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
    public int RetentionDays { get; set; } = 14;
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
        if (File.Exists(file)) return;

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
            DeleteExpiredBackups(scheduledDate);
        }
        finally
        {
            if (File.Exists(temporaryFile)) File.Delete(temporaryFile);
        }
    }

    private void DeleteExpiredBackups(DateTime scheduledDate)
    {
        foreach (var file in System.IO.Directory.EnumerateFiles(_options.Directory, "aljawhara-*.dump"))
        {
            var date = Path.GetFileNameWithoutExtension(file)["aljawhara-".Length..];
            if (DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var backupDate) && backupDate <= scheduledDate.AddDays(-_options.RetentionDays))
                File.Delete(file);
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
        // Drain both streams to prevent pipe deadlocks without retaining database output in memory.
        var stdout = process.StandardOutput.BaseStream.CopyToAsync(Stream.Null, cancellationToken);
        var stderr = process.StandardError.BaseStream.CopyToAsync(Stream.Null, cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(stdout, stderr);
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"{Path.GetFileName(executable)} failed with exit code {process.ExitCode}. Check PostgreSQL permissions and client/server versions.");
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
        }
    }
}
