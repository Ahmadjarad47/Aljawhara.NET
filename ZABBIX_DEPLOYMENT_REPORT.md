# Zabbix Deployment Report — Aljawhara.NET

Deployment and validation date: 2026-11-14 (Asia/Damascus)  
Production host: `72.61.187.165`  
Monitoring host name in Zabbix: `aljawhara-prod`

## Executive summary

Zabbix 7.0.32 LTS was installed from the official Zabbix repository and configured to monitor the existing Aljawhara.NET production host. The deployment uses the official Linux and PostgreSQL Agent 2 templates, the official PostgreSQL Agent 2 plugin, supported systemd and log-count items, Zabbix web scenarios, and the official Telegram webhook.

The deployment did not restart or reconfigure Aljawhara.NET, Nginx, or PostgreSQL. Their production PIDs and activation timestamps remained unchanged through final validation. No commerce database schema or application migration was modified.

The server is small (1 vCPU), but the present single-host scope is acceptable based on observed headroom and measured monitoring consumption. Do not add a significant number of hosts or high-frequency items to this installation without moving Zabbix to a separate monitoring host. Zabbix's published sizing examples are more conservative than this host's specification, so ongoing resource review is required.

Official references:

- [Zabbix lifecycle and release policy](https://www.zabbix.com/life_cycle_and_release_policy.html)
- [Zabbix 7.0 installation requirements](https://www.zabbix.com/documentation/7.0/en/manual/installation/requirements)
- [Official PostgreSQL integration](https://www.zabbix.com/integrations/postgresql)
- [Official Telegram integration](https://www.zabbix.com/integrations/telegram)

## Phase 1 findings

### Operating system and resources

| Item | Observed value |
|---|---|
| Ubuntu | 22.04.5 LTS (Jammy) |
| Kernel | 5.15.0-203-generic x86_64 |
| CPU | 1 vCPU, AMD EPYC 9354P virtual CPU |
| RAM | 3.8 GiB total; 3.1 GiB available before deployment |
| Swap | None |
| Root filesystem | 49 GiB total; 38 GiB free before deployment |
| Initial load | 0.00 / 0.00 / 0.00 |

### Existing production topology

- Aljawhara.NET runs as `aljawhara.service`, under `www-data`, from `/var/www/Aljawhara.NET/publish/Ecom.API.dll`.
- The application listens only on `127.0.0.1:5000` and is reverse-proxied by Nginx.
- Nginx 1.18.0 serves ports 80 and 443 publicly.
- PostgreSQL 18.6 listens only on `127.0.0.1:5432` and `::1:5432`.
- The production database is `ecom_prod`. Zabbix uses a separate `zabbix` database and did not alter `ecom_prod`.
- UFW is active with default-deny inbound policy. Publicly allowed services include SSH, HTTP/HTTPS, and the pre-existing WireGuard rule.
- No Zabbix, Prometheus, Grafana, Nagios, Icinga, Netdata, Telegraf, or Collectd installation existed at the initial audit.
- The host already required an operating-system restart and had pending Ubuntu updates. Neither the restart nor broad OS upgrades were performed.

### Log findings

- Nginx access/error logs are under `/var/log/nginx/` and use the standard combined access format.
- The application logs to systemd-journald with structured .NET/EF Core category headers.
- PostgreSQL logs to `/var/log/postgresql/postgresql-18-main.log`.
- A pre-existing application/database fault occurs approximately every two minutes: `ai_shopping.IndexCheckpoints` is missing. This produces PostgreSQL errors and EF Core failure/warning events. It was intentionally not fixed because doing so would require an application/schema decision outside this monitoring deployment.
- A reliable structured Sadad event exists for payment verification completion, including statuses such as `Invalid` and `RequiresReconciliation`. Monitoring counts these events without storing or sending the raw payment event.
- No reliable structured checkout-failure event was found in seven days of available journal data.

## Installed versions

| Component | Version |
|---|---|
| Zabbix Server PostgreSQL | 7.0.32 |
| Zabbix Agent 2 | 7.0.32 |
| Zabbix Agent 2 PostgreSQL plugin | 7.0.32 |
| Zabbix web frontend | 7.0.32 |
| Zabbix SQL scripts | 7.0.32 |
| Zabbix sender | 7.0.32 |
| Apache (dashboard only) | 2.4.52-1ubuntu4.27 |
| PHP | 8.1.2-1ubuntu2.28 |
| PostgreSQL | 18.6 |

Zabbix 7.0 LTS was selected because it is supported, mature, compatible with Ubuntu 22.04's PHP 8.1 stack, and supports the installed PostgreSQL release. The newly released Zabbix 8.0 frontend requires PHP 8.2 or later.

## Monitoring services and storage

| Service | Purpose | Final state |
|---|---|---|
| `zabbix-server.service` | Collection, trigger evaluation, actions | active, enabled |
| `zabbix-agent2.service` | Linux, systemd, log, and PostgreSQL metrics | active, enabled |
| `apache2.service` | Private Zabbix dashboard only | active, enabled |
| PostgreSQL database `zabbix` | Persistent monitoring storage | online, approximately 71 MiB at validation |

Security and isolation:

- Zabbix Server is bound to `127.0.0.1:10051`.
- Zabbix Agent 2 is bound to `127.0.0.1:10050`.
- The Zabbix dashboard is bound to `127.0.0.1:8080`.
- No Zabbix port was added to UFW and no Zabbix interface is publicly exposed.
- The existing Nginx configuration was not modified or reloaded.
- The monitoring database has its own `zabbix` login and database.
- PostgreSQL monitoring uses `zbx_monitor`, which inherits only the built-in `pg_monitor` role.
- The Zabbix administrator password was changed from the default to a generated random password.

Retention policy:

| Data | Retention |
|---|---|
| Numeric/text history | 14 days |
| Trends | 365 days |
| Trigger events | 90 days |
| Internal/discovery/autoregistration events | 7 days |
| Audit records | 90 days |
| Sessions | 30 days |

## Dashboard access

The dashboard is intentionally accessible only through an SSH tunnel.

From an administrator workstation:

```bash
ssh -L 8080:127.0.0.1:8080 root@72.61.187.165
```

Then open:

```text
http://127.0.0.1:8080/zabbix/
```

Login user: `Admin`

The generated password is not present in this report. It is stored only in `/root/.zabbix_admin_credentials` on the production server with mode `0600`. Retrieve it over the authorized SSH session and change it in the UI after first administrative use.

## Monitored items

### Official Linux template

`Linux by Zabbix agent` provides CPU utilization/load, memory and swap, filesystem discovery and exhaustion triggers, block-device I/O, network interfaces, processes, uptime, and agent health.

### Official PostgreSQL template

`PostgreSQL by Zabbix agent 2` and the official PostgreSQL plugin monitor connectivity, version/uptime, connections, transactions, locks, conflicts, deadlocks, checkpoints/background writer, replication state/lag, database sizes, database age, bloat signals, slow queries, and discovered databases.

At validation, 270 PostgreSQL items existed, 243 already had real values, and none were unsupported. The remaining items were master/dependent or slower-schedule items that had not yet produced a standalone last value.

Twelve nonessential inherited items were unsupported: optional Zabbix Server collectors that are intentionally not started (connector, IPMI, Java, SNMP trap, VMware, and report processes) plus the virtual NIC speed item, for which the hypervisor reports `-1`. These do not affect the requested Linux, PostgreSQL, service, log, HTTP, backup, or Telegram monitoring. They may be disabled at the template level later if a completely clean unsupported-item view is preferred; the unused collectors were not started merely to satisfy template self-checks.

### Service and availability monitoring

- `aljawhara.service` active state and restart counter.
- `nginx.service` active state.
- `postgresql@18-main.service` active state.
- Storefront web scenario: `https://aljawharaplus.com/`, expected HTTP 200.
- API health web scenario: `https://api.aljawharaplus.com/health`, expected HTTP 200.
- Zabbix Server internal health template.

### Privacy-safe log/event monitoring

- Application warning/error/exception event counts from journald.
- Nginx HTTP 5xx response counts from the access log.
- Nginx error/critical/alert/emergency counts from the error log.
- PostgreSQL ERROR/FATAL/PANIC counts.
- Repeated SSH authentication-failure counts.
- Structured Sadad payment-verification failures (`Invalid`, `RequiresReconciliation`, or `Failed`) as counts only.

Raw log lines, request paths, request bodies, customer details, payment identifiers, tokens, and credentials are not included in Telegram messages.

### Backup monitoring

The final server state includes reliable backup metrics refreshed by `aljawhara-backup-metrics.timer` and a daily verification timer. Zabbix receives:

- Last successful verified backup time.
- Job failure flag.
- Missing scheduled backup flag.
- Restore-verification failure flag.
- Verified retained-set count.
- Completed dump count.
- Backup directory size and filesystem free space.
- Last successful restore-test time.
- Incomplete partial-file count.

At validation there were two verified retained backup sets, below the configured target of three. Backup job, missing-schedule, and restore-test failure flags were all zero. No backup file or production data was deleted.

## Trigger policy

High/disaster examples:

- Application, Nginx, or PostgreSQL service not active.
- Storefront or API health scenario unavailable.
- Repeated application errors.
- Repeated HTTP 5xx responses.
- PostgreSQL ERROR/FATAL/PANIC event detected.
- Repeated structured payment-verification failures.
- Backup job, missing schedule, restore verification, low backup filesystem space, or stale restore test.

Warning/average examples:

- Single structured payment-verification failure, held for aggregation/suppression.
- Repeated Nginx error-log events.
- Repeated SSH authentication failures.
- Application restart.
- Backup retention below three verified sets.
- Official Linux template thresholds for CPU, memory, filesystem, I/O, and network conditions.

## Telegram configuration

- Integration: official Zabbix `Telegram` webhook media type.
- Bot: supplied dedicated bot, validated with Telegram `getMe` and `getChat`.
- Token storage: Zabbix secret global macro `{$TELEGRAM.BOT.TOKEN}`.
- TLS certificate validation: full.
- Webhook concurrency: one session.
- Retry policy: three attempts, ten-second interval.
- High/disaster action: immediate, one operation per event.
- Warning/average action: five-minute hold window; transient warnings that recover before the delayed step are suppressed.
- Duplicate suppression: Zabbix event state plus a single escalation step prevents repeated messages for repeated values while the same problem remains open.
- Recovery notifications: enabled for recipients that received the problem notification.
- Message templates exclude item values, operational data, raw logs, request content, and update comments.

The warning policy is a sustained-event hold/aggregation window, not a cross-event digest. Zabbix's official webhook does not combine unrelated problems into a single digest without additional tooling.

## Production validation

### End-to-end notification test

A synthetic trapper item and trigger named `Zabbix Telegram delivery test` were used. This did not stop or modify any production service.

| Test | Zabbix result |
|---|---|
| High-severity problem notification | sent successfully; zero retries; no error |
| Recovery notification | sent successfully; zero retries; no error |

The reusable test item remains available as `zabbix.telegram.delivery.test`. Send `1` to test a problem and `0` to test recovery.

### Real metrics

Validated real values included:

- Linux CPU utilization and available-memory percentage.
- Filesystem and network discovery.
- PostgreSQL ping = 1, database sizes, locks/transactions, and discovered databases.
- Aljawhara.NET, Nginx, and PostgreSQL systemd states = `active`.
- Aljawhara.NET restart counter = 0.
- Storefront and API health endpoints = HTTP 200.
- Privacy-safe log counters updating without unsupported-item errors.

### Existing production health

| Service | PID before | PID after | Restarted by deployment? |
|---|---:|---:|---|
| Aljawhara.NET | 21338 | 21338 | No |
| Nginx master | 669 | 669 | No |
| PostgreSQL main | 698 | 698 | No |

No failed systemd units were present at final validation.

### Monitoring resource consumption

| Component | MemoryCurrent at validation |
|---|---:|
| Zabbix Server | approximately 75 MiB |
| Zabbix Agent 2 | approximately 12 MiB |
| Private Apache dashboard | approximately 36 MiB |
| Total monitoring services | approximately 123 MiB |

Final host state:

- Approximately 2.8 GiB RAM available.
- Approximately 37 GiB root-filesystem space free.
- Load average approximately 0.13 / 0.22 / 0.13 during validation.
- Zabbix database approximately 71 MiB.
- Zabbix Server used approximately 81 CPU-seconds over roughly 4 hours 46 minutes, well under 1% of the single vCPU on average.

The resource result is acceptable for this scope. Move Zabbix to a separate host if sustained CPU load rises, available RAM falls below 1 GiB, disk growth exceeds expectations, or additional hosts/high-frequency checks are planned.

## Current findings requiring application/operations attention

1. `ai_shopping.IndexCheckpoints` is missing and produces a database/application error approximately every two minutes. Monitoring correctly detects this. Fixing it may require an application migration and was not attempted.
2. Only two verified backup sets are retained while the configured warning target is three. Current restore-verification and job-failure flags are healthy.
3. Ubuntu reports pending updates and a pre-existing restart requirement. Updates/reboot need a separate approved maintenance window.
4. Credentials were supplied through the deployment channel, and commented credential-like values exist in the application systemd unit. Rotate the root SSH password, Telegram bot token, and any exposed application client secrets; prefer key-only SSH. Update the Zabbix secret macro after rotating the Telegram token.

## Remaining monitoring gaps

- Checkout failures: no reliable structured checkout-failure event was present in the available logs. Add a structured event with a stable event name, outcome, non-customer correlation ID, and failure class before creating a checkout trigger.
- Payment reconciliation/discrepancy: monitoring currently detects only reliable Sadad verification statuses. It does not claim to detect amount discrepancies, settlement discrepancies, fraud, or data corruption.
- Nginx connection/request-rate internals: the official Nginx template requires `stub_status`. Enabling it would require an Nginx configuration change and reload, which were explicitly not authorized. Service state, HTTP availability, 5xx counts, and error logs are monitored without it.
- Application latency and endpoint-specific failure rates: add OpenTelemetry/APM or stable application metrics if these are required. No custom monitoring framework was introduced.
- Security incidents: repeated authentication failures are counted, but this is not an incident-detection or SIEM deployment.

## Maintenance

### Routine checks

```bash
systemctl status zabbix-server zabbix-agent2 apache2
ss -lntp | grep -E ':(8080|10050|10051)'
sudo -u postgres psql -d zabbix -c "select pg_size_pretty(pg_database_size('zabbix'));"
journalctl -u zabbix-server -u zabbix-agent2 -u apache2 --since today -p warning
```

Update only from the configured official Zabbix 7.0 repository, review release notes, back up the Zabbix database and `/etc/zabbix` first, and use an approved maintenance window for package upgrades.

### Repeat the Telegram test

```bash
zabbix_sender -z 127.0.0.1 -s aljawhara-prod \
  -k zabbix.telegram.delivery.test -o 1

zabbix_sender -z 127.0.0.1 -s aljawhara-prod \
  -k zabbix.telegram.delivery.test -o 0
```

Always return the test item to `0`.

### Credential maintenance

- Change the Zabbix `Admin` password in the UI and update the root-only credential record if operational policy requires it.
- Rotate the Telegram bot token in BotFather, then update `{$TELEGRAM.BOT.TOKEN}` as a secret macro in Zabbix.
- Never place the token or raw log content in action messages.

## Rollback

The pre-change configuration backup is:

```text
/root/zabbix-deployment-backup-20261114T053849Z
```

It includes the pre-change Nginx, PostgreSQL, rsyslog, application systemd, port, and firewall state plus pristine Zabbix/Apache configuration snapshots. The directory is root-only. Nginx was not changed, so no Nginx rollback is required.

Safe service rollback:

```bash
systemctl disable --now zabbix-server zabbix-agent2 apache2
```

Before removing persistent monitoring data, create a final Zabbix database backup:

```bash
sudo -u postgres pg_dump -Fc zabbix > /root/zabbix-final-backup.dump
```

Package removal and dropping the `zabbix`/`zbx_monitor` roles or `zabbix` database are destructive and should be performed only after verifying the dump and obtaining explicit approval. Never drop or modify `ecom_prod` during monitoring rollback. Preserve the independently managed Aljawhara backup timers, scripts, backup files, and metrics unless their owner separately authorizes removal.
