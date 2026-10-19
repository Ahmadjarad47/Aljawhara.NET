# Admin monitoring

Open **`/admin/monitoring`** on the API host and sign in with an existing Admin account.
The page refreshes every 15 seconds while visible. It uses the existing login API and
keeps its JWT in memory; reloading the page requires signing in again. The public
HTML, CSS and JavaScript contain no monitoring data. Both data endpoints require
the Admin role and return `Cache-Control: no-store`.

## Coverage

- Every incoming request: HTTP method, route template, response status, elapsed time,
  authenticated user ID when available, and trace ID. Includes validation failures,
  denied access, rate limiting, missing routes, and caught controller error responses.
- Unhandled exceptions: generic 500 response and trace ID; exception type in logs.
  The `X-Trace-Id` response header links request and application events.
- Application `ILogger` events at Information and above, and framework warnings/errors.
  Existing authentication, OTP, account management, payment, email/backup worker logs
  are included. Debug/Trace events are excluded to keep storage bounded.
- Factory-created outbound HTTP calls, including Sadad: method, host, status and duration.
- Sadad `AMOUNT_FIELDS_MISMATCH` warnings include decimal-only `ExpectedAmount`,
  `ExpectedGrossAmount`, `ExpectedDiscountAmount`, `GatewayAmount`, `GatewayTotal`,
  `GatewaySubtotal`, and optional `GatewayFees`. These identify which comparison failed without capturing
  the provider response, tokens or customer details. A successful GetById HTTP 200
  does not by itself confirm payment or matching amounts.
- EF `SaveChanges` activity: entity type, numeric/GUID identity, operation and enum status transitions, with
  failure events. This records successful saves, not proof of an outer transaction
  commit; bulk `ExecuteUpdate`/`ExecuteDelete` and raw SQL bypass this interceptor.
- Database connectivity checked automatically every 30 seconds with a 10 second timeout.
  Health transitions are logged; the dashboard shows the last check time.
- Payment orchestration and Sadad verification job success, failure, and stale heartbeat.
  Production backup **scheduler** health records the last successful scheduling check;
  completed backup files also emit existing backup completion events.
- Last 60 minutes of request volume, server/client error counts, error rate, average
  latency, a histogram upper bound for p95 latency, process uptime and memory.

The page, its polling requests and `/health` are logged but excluded from traffic
metrics. Metrics and job state describe **one process** and reset on restart. This
is not an external uptime checker: another service must poll `/health` to detect
a stopped server. Email/SMS alerts and distributed aggregation are not included.

## Privacy and storage

The monitoring provider uses an allowlist of structured metadata fields. Unknown
fields are redacted. It does not capture request/response bodies, headers, query
strings, raw URL paths, SQL parameters, exception messages or stack traces.
Email addresses, JWTs and common secret assignments are additionally scrubbed.
Framework events retain the category, event ID and exception type, with their
property values redacted. Existing console providers retain their existing behavior;
this redaction policy applies to the admin monitoring provider and its files.

Events are queued without blocking requests, retained in a bounded recent-event
buffer, and appended to rotating JSON Lines files independently of PostgreSQL.
By default the dashboard searches the **latest 5,000 events**; older retained events
remain in disk files. Recent events are restored from those files after restart.
At most 100 files of approximately 10 MB each are retained for up to 14 days;
age and file count cleanup runs on writes. Rotation may exceed a size limit by
one event. Use a separate persistent directory for each app instance.

Queue overflow, file write failures and log-storage permission problems are shown
in the dashboard. A full disk does not fail customer requests. The queue is drained
on graceful shutdown within the host shutdown timeout; an abrupt crash can lose
queued events. These are operational logs, not an immutable compliance audit.

## Configuration

All settings are under `Monitoring` and support environment variable overrides:

| Environment variable | Default |
| --- | --- |
| `Monitoring__Directory` | `logs` relative to the API content root |
| `Monitoring__RetentionDays` | `14` |
| `Monitoring__MaxFiles` | `100` |
| `Monitoring__MaxFileBytes` | `10485760` |
| `Monitoring__RecentEventLimit` | `5000` |
| `Monitoring__QueueCapacity` | `10000` |
| `Monitoring__HealthIntervalSeconds` | `30` |
| `Monitoring__SlowRequestMilliseconds` | `2000` |

Use `/var/log/aljawhara` on the Ubuntu deployment. The supplied systemd drop-in
creates it with mode 0700, grants write access and sets `UMask=0077`. Apply the
updated environment file and drop-in, reload systemd and restart the service as
part of deployment. New files are created with mode 0600 on Linux. Keep this
directory outside the web root, on persistent storage with space monitoring.
No database migration or new package is required.

## Dashboard integration API

Send `Authorization: Bearer <admin JWT>`:

- `GET /api/admin/monitoring/summary`: current health, jobs, traffic metrics, 60
  minute series and log storage status.
- `GET /api/admin/monitoring/logs`: newest events first, with `items`, `totalCount`,
  `page`, `pageSize`, `retainedCount`, `recentEventLimit`, `oldestAvailableAt`.

Log filters: `level` (Information/Warning/Error/Critical), `category` (substring),
`search` (message/category/trace/exception type), `traceId` (exact), `from` and `to`
(ISO 8601), `page` (1–1000), `pageSize` (1–200; default 50). Invalid filters return
400. Selecting a trace ID on the page filters all events from that request.
Pages are a live view; newly arriving events can shift page boundaries.

## Validation

The `MonitoringTests` suite runs without PostgreSQL and covers role enforcement,
dashboard assets, request/error metrics, trace correlation, secret redaction,
filter validation, pagination, file rotation/restoration/cleanup, queue overflow
and storage failure. Run:

```powershell
dotnet test tests/Ecom.CheckoutAudit.Tests/Ecom.CheckoutAudit.Tests.csproj --filter FullyQualifiedName~MonitoringTests
```

`MonitoringDatabaseTests` additionally checks generated entity IDs, enum status
changes and database failures against PostgreSQL, while verifying that customer
fields stay out of the logs. It uses the existing `ECOM_TEST_POSTGRES` test-server
setting and creates and deletes its own uniquely named test database.
