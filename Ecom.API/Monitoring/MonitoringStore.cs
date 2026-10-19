using System.Globalization;
using System.Text.Json;
using System.Threading.Channels;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace Ecom.API.Monitoring;

// Independent of PostgreSQL: failures remain visible while the database is unavailable.
public sealed class MonitoringStore
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly object _gate = new();
    private readonly Queue<MonitoringEvent> _recent = new();
    private readonly Channel<MonitoringEvent> _pending;
    private long _dropped;
    private string? _persistenceError;
    private string? _writePath;
    public MonitoringOptions Options { get; }
    public string DirectoryPath { get; }
    public long DroppedEvents => Interlocked.Read(ref _dropped);
    public string? PersistenceError => Volatile.Read(ref _persistenceError);
    public ChannelReader<MonitoringEvent> Reader => _pending.Reader;

    public MonitoringStore(IOptions<MonitoringOptions> options, IHostEnvironment environment)
    {
        Options = options.Value;
        DirectoryPath = Path.GetFullPath(Options.Directory, environment.ContentRootPath);
        _pending = Channel.CreateBounded<MonitoringEvent>(new BoundedChannelOptions(Options.QueueCapacity)
        {
            SingleReader = true, FullMode = BoundedChannelFullMode.Wait
        });
    }

    public void Add(MonitoringEvent entry)
    {
        lock (_gate)
        {
            _recent.Enqueue(entry);
            while (_recent.Count > Options.RecentEventLimit) _recent.Dequeue();
        }
        if (!_pending.Writer.TryWrite(entry)) Interlocked.Increment(ref _dropped);
    }

    public MonitoringEvent[] Snapshot()
    {
        lock (_gate) return _recent.Reverse().Where(e => e.Timestamp >= DateTimeOffset.UtcNow.AddDays(-Options.RetentionDays)).ToArray();
    }

    public void Complete() => _pending.Writer.TryComplete();
    public void SetPersistenceError(bool failed) => Volatile.Write(ref _persistenceError,
        failed ? "Log storage unavailable. Check disk space and directory permissions." : null);
    public void RecordDroppedEvent() => Interlocked.Increment(ref _dropped);

    public void RestoreRecent()
    {
        if (!System.IO.Directory.Exists(DirectoryPath)) return;
        var history = new List<MonitoringEvent>();
        foreach (var file in LogFiles().Reverse())
        {
            if (File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddDays(-Options.RetentionDays)) continue;
            // Each file is size bounded; only retain the tail in memory.
            foreach (var line in File.ReadLines(file).TakeLast(Options.RecentEventLimit))
            {
                try
                {
                    var entry = JsonSerializer.Deserialize<MonitoringEvent>(line, JsonOptions);
                    if (entry != null) history.Add(entry);
                }
                catch (JsonException) { /* Ignore an incomplete line after an interrupted write. */ }
            }
            if (history.Count >= Options.RecentEventLimit) break;
        }
        lock (_gate)
        {
            var merged = history.Concat(_recent).DistinctBy(e => e.Id)
                .OrderBy(e => e.Timestamp).TakeLast(Options.RecentEventLimit).ToArray();
            _recent.Clear();
            foreach (var entry in merged) _recent.Enqueue(entry);
        }
    }

    public string[] LogFiles() => System.IO.Directory.GetFiles(DirectoryPath, "events-*.jsonl")
        .Where(file => Regex.IsMatch(Path.GetFileName(file), @"^events-\d{8}-\d{6}\.jsonl$"))
        .Order(StringComparer.Ordinal).ToArray();

    public string GetWritePath()
    {
        if (OperatingSystem.IsLinux())
            System.IO.Directory.CreateDirectory(DirectoryPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        else System.IO.Directory.CreateDirectory(DirectoryPath);
        var prefix = $"events-{DateTime.UtcNow:yyyyMMdd}-";
        if (_writePath != null && Path.GetFileName(_writePath).StartsWith(prefix, StringComparison.Ordinal) &&
            File.Exists(_writePath) && new FileInfo(_writePath).Length < Options.MaxFileBytes) return _writePath;
        var last = LogFiles().LastOrDefault(f => Path.GetFileName(f).StartsWith(prefix, StringComparison.Ordinal));
        var part = last == null ? 0 : int.Parse(Path.GetFileNameWithoutExtension(last)[prefix.Length..], CultureInfo.InvariantCulture);
        if (last != null && new FileInfo(last).Length < Options.MaxFileBytes) return _writePath = last;
        return _writePath = Path.Combine(DirectoryPath, $"{prefix}{(last == null ? 0 : part + 1):D6}.jsonl");
    }

    public void Prune()
    {
        var files = LogFiles();
        var cutoff = DateTime.UtcNow.AddDays(-Options.RetentionDays);
        for (var i = 0; i < files.Length; i++)
            if (i < files.Length - Options.MaxFiles || File.GetLastWriteTimeUtc(files[i]) < cutoff)
                File.Delete(files[i]);
    }
}

public sealed class MonitoringPersistenceWorker(MonitoringStore store) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { store.RestoreRecent(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { store.SetPersistenceError(true); }

        // Read to completion on graceful shutdown; StopAsync completes the writer first.
        var nextPrune = DateTimeOffset.MinValue;
        string? currentFile = null;
        await foreach (var entry in store.Reader.ReadAllAsync())
        {
            try
            {
                var file = store.GetWritePath();
                var fileOptions = new FileStreamOptions { Mode = FileMode.Append, Access = FileAccess.Write,
                    Share = FileShare.Read, Options = FileOptions.Asynchronous };
                if (OperatingSystem.IsLinux()) fileOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                await using (var stream = new FileStream(file, fileOptions))
                await using (var writer = new StreamWriter(stream))
                    await writer.WriteLineAsync(JsonSerializer.Serialize(entry, MonitoringStore.JsonOptions));
                if (currentFile != file || DateTimeOffset.UtcNow >= nextPrune)
                {
                    store.Prune();
                    currentFile = file;
                    nextPrune = DateTimeOffset.UtcNow.AddMinutes(1);
                }
                store.SetPersistenceError(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Never throw into checkout, or recursively log a failing log sink.
                store.SetPersistenceError(true);
                store.RecordDroppedEvent();
            }
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        store.Complete();
        return base.StopAsync(cancellationToken);
    }
}
