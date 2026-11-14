# Aljawhara.NET Backup Audit and Retention

Audit and deployment date: 2026-11-14  
Server: Ubuntu 22.04.5 LTS  
Production time zone used in this report: Asia/Damascus (UTC+03:00)  
Database server: PostgreSQL 18, cluster `18/main` on port 5432  
Monitoring: Zabbix 7.0.32

No database credentials, Telegram credentials, backup contents, or application secrets are included in this document.

## Executive result

- The existing backup creator is the Aljawhara.NET `PostgresBackupService` hosted inside `aljawhara.service`; it is not cron- or systemd-timer-based.
- The service successfully created the latest backup at **2026-11-14 03:00:44 Asia/Damascus** (`00:00:44 UTC`).
- Both existing production dumps passed custom-archive parsing and real isolated restores.
- The latest restore contained 40 application tables and 38 validated foreign keys, with zero unvalidated foreign keys.
- Production-safe automatic retention is active. Only actual restore-verified archives count toward the limit of three.
- Only two verified production sets currently exist, so no production backup was deleted. This is intentional and is alerted in Zabbix.
- Zabbix collects all requested backup metrics. Telegram problem and recovery messages were both delivered successfully in an end-to-end test.
- Aljawhara.NET was restarted once to load the retention safety setting and returned healthy in 7 seconds. PostgreSQL was not restarted.

## Existing backup discovery

### Active scheduled Aljawhara backup

| Property | Value |
| --- | --- |
| Creator | `Ecom.API.HostedServices.PostgresBackupService` inside `aljawhara.service` |
| Production database | `ecom_prod` |
| Active directory | `/var/backups/aljawhara` |
| Directory ownership/mode | `www-data:www-data`, `0700` |
| Archive ownership/mode | `www-data:www-data`, `0600` |
| Format | PostgreSQL custom format (`pg_dump --format=custom --no-owner --no-acl`) |
| Schedule | Daily at `00:00 UTC` / `03:00 Asia/Damascus` |
| Retry | Every 15 minutes after a backup failure |
| Incomplete naming | `*.dump.partial`; never counted as completed |
| Creation lock | `/var/backups/aljawhara/.backup.lock` |

The root crontab, user crontab spool, `/etc/cron.*`, and the systemd timer inventory contained no duplicate Aljawhara backup creator. The application journal recorded a successful completion for the 2026-11-14 archive. The archive timestamp and size match that journal event.

### Current production archives

| Archive | Timestamp UTC | Timestamp Asia/Damascus | Size | Result |
| --- | --- | --- | ---: | --- |
| `/var/backups/aljawhara/aljawhara-2026-11-13.dump` | 2026-11-13 13:27:41 | 2026-11-13 16:27:41 | 227,863 bytes | Catalog valid; isolated restore passed |
| `/var/backups/aljawhara/aljawhara-2026-11-14.dump` | 2026-11-14 00:00:44 | 2026-11-14 03:00:44 | 231,372 bytes | Catalog valid; isolated restore passed |

The directory is 463,331 bytes (`456K`) including its lock file. There were no `.partial` files at final verification.

### Other discovered backup-like locations

These locations were audited but are outside the production retention scope and were not modified:

| Location | Approximate size | Classification |
| --- | ---: | --- |
| `/root/ecom-backups` | 8.1M | Historical manual cutover/database artifacts; not scheduled |
| `/root/aljawhara-maintenance-20261024-20261023T213823Z` | 6.1M | Historical maintenance snapshot; not scheduled |
| `/var/backups/ecom_manual` | 4.0K | Manual legacy area; includes one zero-byte failed dump |
| `/var/opt/mssql/backup` | 12M | Historical SQL Server backup; unrelated to current PostgreSQL schedule |
| `/root/zabbix-deployment-backup-20261114T053849Z` | configuration snapshot | Zabbix deployment rollback material; excluded from Aljawhara retention |
| `/root/wg-backup`, `/root/wg-easy-backup` | small | Unrelated WireGuard backups |

The zero-byte legacy file is `/var/backups/ecom_manual/ecom_test_before_reset_20261019_185925.dump`. It was classified as failed/incomplete and preserved because it is outside the approved retention scope.

No scheduled Restic, Borg, Rclone, Duplicity, pgBackRest, WAL archive, or offsite Aljawhara database copy was found. No legal archive marker was found. Offsite and unrelated locations were not touched.

## Backup coverage and disaster-recovery gaps

The active set has an intentionally limited scope: **the `ecom_prod` PostgreSQL database only**.

Included:

- PostgreSQL application schema and data in `ecom_prod`.
- Table data, indexes, constraints, and the Entity Framework migrations history contained in the custom archive.

Not included in the active scheduled set:

- PostgreSQL roles, role passwords, tablespaces, and cluster-global settings (`pg_dumpall --globals-only` equivalent).
- PostgreSQL server configuration, including `postgresql.conf`, `pg_hba.conf`, and `pg_ident.conf`.
- Current application files under `/var/www/Aljawhara.NET/publish` (about 162M).
- Current private application configuration under `/etc/aljawhara` and the systemd unit/drop-ins.
- Nginx configuration, certificates, firewall state, or OS/package reconstruction instructions.
- Uploaded/product images. The active application uses an R2 bucket; no independent backup or object-versioning verification was available. Historical local MinIO data exists under `/root/minio-data` (about 6.8M) but is not part of the active backup.
- ASP.NET data-protection keys and other host-local runtime state.
- The Zabbix database and Zabbix configuration.
- A confirmed independent offsite copy of the PostgreSQL dump.

Therefore, the verified database archive is restorable for its intended database-only scope, but the current system is not yet a complete full-server disaster-recovery solution. The highest-priority remaining work is a private offsite copy, PostgreSQL globals/configuration, application configuration, and a separately verified image/object backup policy.

## Restore and integrity test

The latest archive was copied to a PostgreSQL-owned temporary location and restored with `pg_restore --exit-on-error --no-owner --no-acl` into a uniquely named disposable database. The production database was never used as a restore target.

Results for `/var/backups/aljawhara/aljawhara-2026-11-14.dump`:

| Check | Result |
| --- | --- |
| `pg_restore --list` | Passed |
| Full isolated restore | Passed, exit status 0 |
| Restored database size | 12 MB |
| Application tables | 40 |
| Foreign keys | 38 |
| Unvalidated foreign keys | 0 |
| Check constraints | 20 total, 20 validated |
| Restore-test completion | 2026-11-14 13:29:07 Asia/Damascus (`10:29:07 UTC`) |
| Disposable database removed | Yes |

Creating all foreign keys after data load succeeded, which actively proves that the restored rows satisfied the restored referential constraints. No disposable restore database or temporary dump remained at final verification.

Representative exact row counts matched production at the time of the test:

| Table | Restored | Production |
| --- | ---: | ---: |
| `AspNetUsers` | 6 | 6 |
| `Categories` | 16 | 16 |
| `OrderItems` | 6 | 6 |
| `Orders` | 6 | 6 |
| `PaymentAttempts` | 6 | 6 |
| `Products` | 584 | 584 |
| `Transactions` | 6 | 6 |
| `Visitors` | 1,296 | 1,296 |
| `__EFMigrationsHistory` | 8 | 8 |

Both existing production archives were independently restored and marked verified with a private checksum sidecar under `/var/lib/aljawhara-backup/verified`.

## Retention configuration

Effective policy: keep the newest **three restore-verified production archives**.

Installed controls:

- `/usr/local/sbin/aljawhara-backup-verify`
- `/usr/local/sbin/aljawhara-backup-metrics`
- `/usr/local/sbin/aljawhara-backup-metric`
- `/etc/systemd/system/aljawhara-backup-verify.service`
- `/etc/systemd/system/aljawhara-backup-verify.timer`
- `/etc/systemd/system/aljawhara-backup-metrics.service`
- `/etc/systemd/system/aljawhara-backup-metrics.timer`
- `/etc/zabbix/zabbix_agent2.d/aljawhara-backup.conf`
- `/etc/logrotate.d/aljawhara-backup`

The existing application remains the only backup creator. Its internal filename-only pruning is set to a large safety ceiling so it cannot delete an old good archive before the new archive has passed a real restore. The external verifier performs the actual retention decision.

Verifier schedule: every hour at minutes `05`, `20`, `35`, and `50` UTC (every 15 minutes, offset from the application backup). Metrics refresh every minute.

Safety properties:

1. Only exact `aljawhara-YYYY-MM-DD.dump` names are considered.
2. A dump must pass checksum validation, full isolated restore, important-table queries, and constraint validation before receiving a verified marker.
3. `.partial`, failed, unrelated, offsite, and unverified archives are never counted as valid sets.
4. If any candidate fails verification, pruning is skipped.
5. If fewer than three verified archives exist, all archives are preserved.
6. The application lock and a separate verifier lock prevent backup creation, restore verification, and pruning from running concurrently. A held-lock test produced a clean deferred result.
7. Before deletion, an exact dry-run manifest records the absolute path, byte size, and SHA-256 of every candidate.
8. At least three verified sets remain after any cleanup; the last restorable set can never be deleted by this policy.

Initial production dry run: `/var/log/aljawhara-backup/retention-initial-dry-run.manifest`

```text
mode=initial-dry-run-no-deletion
retention_count=3
verified_count=2
eligible_deletions=0
```

No production cleanup was performed because only two verified sets exist. A separate disposable test used four real PostgreSQL archives: the verifier restored all four, produced an exact manifest for the oldest, deleted only that disposable oldest archive, and left the newest three. The entire harness and its databases were then removed.

## Zabbix and Telegram monitoring

Zabbix server, agent, web frontend, Apache loopback frontend, and Nginx are active. The Zabbix frontend/API is bound through Apache on `127.0.0.1:8080`; the public application proxy was not replaced.

The `aljawhara-prod` host collects these one-minute numeric items:

- Last successful verified backup timestamp.
- Backup job failure flag.
- Missing scheduled backup flag (older than 27 hours).
- Restore/integrity verification failure flag.
- Verified retained-set count.
- Completed dump count.
- Backup-directory size.
- Backup filesystem free bytes.
- Last successful restore-test timestamp.
- Incomplete `.partial` count.
- Last application backup-completion timestamp.

Configured triggers include:

- Backup job failed — High.
- Scheduled backup missing — High.
- Restore verification failed — High.
- Fewer than three verified sets — Warning.
- Backup filesystem below 5 GiB free — High.
- Restore test older than 36 hours — High.
- Partial file persists for two hours — Average.

At final verification every backup item was supported and collecting. The only open backup problem was the expected warning that only two verified sets exist.

Telegram media, recipients, high/critical actions, warning escalation, and recovery operations are enabled. A controlled trapper test generated a High problem and then recovery:

| Test | Zabbix result | Time Asia/Damascus |
| --- | --- | --- |
| Problem notification | Alert status `sent`, no error | 2026-11-14 13:50:52 |
| Recovery notification | Alert status `sent`, no error | 2026-11-14 13:51:31 |

The test problem was closed and no test problem remained open. Tokens and recipient identifiers were not displayed or recorded here.

## Disk space

| Point | Root filesystem | Backup directory |
| --- | --- | --- |
| Audit start | approximately 38G available, 24% used | 456K |
| After retention dry run | 39,710,056,448 bytes available (reported as 37G), 24% used | 463,331 bytes (`456K`) |

There was no production backup deletion, so the backup directory size did not change. Any small root-filesystem change during the work came from Zabbix/monitoring state and logs, not retention cleanup.

## Final verification checklist

- Scheduled application backup: **working**, latest success 2026-11-14 03:00:44 Asia/Damascus.
- Latest archive catalog validation: **passed**.
- Actual isolated restore: **passed**.
- Important row counts: **matched production at test time**.
- Referential integrity: **passed**.
- Retention dry run: **passed, zero eligible production deletions**.
- Automatic retention: **enabled and tested in a disposable four-archive harness**.
- Concurrency protection: **enabled and lock-defer test passed**.
- Zabbix collection: **operational**.
- Backup failure/missing/integrity/count/size/disk/restore triggers: **configured**.
- Telegram problem delivery: **passed**.
- Telegram recovery delivery: **passed**.
- PostgreSQL restart: **not performed**.
- Production health after the one necessary application restart: **HTTP 200**.

## Required follow-up for full disaster recovery

1. Provision an independent private offsite destination and copy only completed, verified archives with separate credentials and retention.
2. Add encrypted backups of PostgreSQL globals and the minimum PostgreSQL configuration needed to rebuild the cluster.
3. Back up `/etc/aljawhara`, the systemd service/drop-ins, Nginx configuration, and the required ASP.NET data-protection/runtime keys to a protected configuration archive.
4. Confirm R2 object versioning or create an independent private image/object backup; do not assume the public delivery bucket is a backup.
5. After the next successful daily archive, confirm the verified-set warning moves from two to three. The first production deletion will not occur until a fourth restore-verified set exists, at which point the exact deletion manifest will be written before removal.
