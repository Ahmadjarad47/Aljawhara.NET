using System.Diagnostics;

namespace Ecom.API.Monitoring;

public sealed class MonitoringState
{
    private readonly object _gate = new();
    private readonly SortedDictionary<long, MinuteBucket> _minutes = new();
    private readonly Dictionary<string, JobStatus> _jobs = new();
    public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
    private object _health = new { status = "Unknown", checkedAt = (DateTimeOffset?)null, checks = Array.Empty<object>() };
    private static readonly double[] Bounds = [10, 25, 50, 100, 250, 500, 1000, 2000, 5000, 10000, 30000, double.PositiveInfinity];

    public void RecordRequest(int status, double elapsedMilliseconds)
    {
        lock (_gate)
        {
            var minute = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60;
            if (!_minutes.TryGetValue(minute, out var bucket)) _minutes[minute] = bucket = new();
            bucket.Requests++;
            bucket.Duration += elapsedMilliseconds;
            if (status >= 500) bucket.Errors++;
            else if (status >= 400) bucket.ClientErrors++;
            bucket.Latencies[Array.FindIndex(Bounds, bound => elapsedMilliseconds <= bound)]++;
            foreach (var key in _minutes.Keys.TakeWhile(key => key < minute - 59).ToArray()) _minutes.Remove(key);
        }
    }

    public void SetHealth(object health) { lock (_gate) _health = health; }
    public void RegisterJob(string name, double staleAfterSeconds)
    {
        lock (_gate) _jobs.TryAdd(name, new(name, "Starting", DateTimeOffset.UtcNow, null, staleAfterSeconds));
    }
    public void JobCompleted(string name, bool success)
    {
        lock (_gate)
        {
            if (!_jobs.TryGetValue(name, out var previous)) return;
            var now = DateTimeOffset.UtcNow;
            _jobs[name] = previous with { Status = success ? "Healthy" : "Failed", LastRun = now,
                LastSuccess = success ? now : previous.LastSuccess };
        }
    }

    public object Summary(MonitoringStore store)
    {
        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            var currentMinute = now.ToUnixTimeSeconds() / 60;
            var series = Enumerable.Range(0, 60).Select(i =>
            {
                var minute = currentMinute - 59 + i;
                var bucket = _minutes.GetValueOrDefault(minute) ?? new MinuteBucket();
                return new { timestamp = DateTimeOffset.FromUnixTimeSeconds(minute * 60), requests = bucket.Requests,
                    errors = bucket.Errors, clientErrors = bucket.ClientErrors,
                    averageMs = bucket.Requests == 0 ? 0 : Math.Round(bucket.Duration / bucket.Requests, 1) };
            }).ToArray();
            var count = series.Sum(x => x.requests);
            var errors = series.Sum(x => x.errors);
            var active = _minutes.Where(p => p.Key >= currentMinute - 59).Select(p => p.Value).ToArray();
            var cumulative = 0L;
            double? p95 = null;
            for (var i = 0; i < Bounds.Length && count > 0; i++)
            {
                cumulative += active.Sum(b => b.Latencies[i]);
                if (cumulative >= Math.Ceiling(count * .95)) { p95 = double.IsFinite(Bounds[i]) ? Bounds[i] : null; break; }
            }
            using var process = Process.GetCurrentProcess();
            return new
            {
                timestamp = now, startedAt = StartedAt, uptimeSeconds = (now - StartedAt).TotalSeconds,
                windowMinutes = 60, requests = count, serverErrors = errors, clientErrors = series.Sum(x => x.clientErrors),
                errorRatePercent = count == 0 ? 0 : Math.Round(100d * errors / count, 2),
                averageMs = count == 0 ? 0 : Math.Round(active.Sum(b => b.Duration) / count, 1),
                p95UpperBoundMs = p95, workingSetMb = Math.Round(process.WorkingSet64 / 1048576d, 1),
                health = _health, series,
                jobs = _jobs.Values.Select(j => j with { Status = (now - j.LastRun).TotalSeconds > j.StaleAfterSeconds ? "Stale" : j.Status }).ToArray(),
                logging = new { droppedEvents = store.DroppedEvents, persistenceError = store.PersistenceError,
                    recentEventLimit = store.Options.RecentEventLimit, retentionDays = store.Options.RetentionDays }
            };
        }
    }

    private sealed class MinuteBucket
    {
        public long Requests, Errors, ClientErrors;
        public double Duration;
        public long[] Latencies = new long[Bounds.Length];
    }
}
