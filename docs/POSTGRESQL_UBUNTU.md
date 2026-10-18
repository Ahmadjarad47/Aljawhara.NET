# PostgreSQL cutover and daily backups on Ubuntu / Hostinger VPS

The API and EF tooling read `ConnectionStrings__DefaultConnection` directly from
the process environment. JSON and User Secrets cannot supply the database connection.
The application now uses PostgreSQL exclusively. SQL Server migrations are retained
in `Ecom.Infrastructure/Migrations` as an excluded historical archive; the active
baseline is `Ecom.Infrastructure/PostgreSqlMigrations`.
The archived SQL Server snapshot has a `.cs.archive` suffix so EF tooling cannot
mistake it for the active PostgreSQL snapshot.

**Do not deploy this change to the running production service before transferring
your data.** The existing deployment workflow restarts the API on pushes to master.
No production server is changed by these repository edits.

## 1. Prepare Ubuntu

Install the .NET 10 ASP.NET runtime using Microsoft's Ubuntu instructions for your
Ubuntu release. Install PostgreSQL and its matching command-line tools:

```bash
sudo apt update
sudo apt install postgresql postgresql-client
sudo systemctl enable --now postgresql
sudo -u postgres psql
```

In psql, create a dedicated login and database. `\password` prompts for a password
without putting it in shell history. This role owns only the application database,
not the PostgreSQL server:

```sql
CREATE ROLE aljawhara LOGIN;
\password aljawhara
CREATE DATABASE aljawhara OWNER aljawhara;
\q
```

Use `127.0.0.1:5432` for a database on the same VPS and keep port 5432 closed to the
public internet. The sample disables TLS only for this loopback connection. For a
remote database use `SSL Mode=VerifyFull;Root Certificate=/path/to/ca.crt` and the
matching DNS hostname. Check `pg_dump --version` against `SELECT version()`; use
client tools of the same PostgreSQL major version as the server (an older pg_dump
cannot dump a newer server).

## 2. Preserve the current SQL Server database

Before maintenance, take a verified full SQL Server backup using your existing
SQL Server backup process and keep a copy away from the VPS. Back up the old
application release and environment configuration too. Keep SQL Server available
for rollback; this transfer tool never changes its data.

First rehearse the transfer against a restored copy of production. The source must
be at the schema represented by the archived SQL Server migrations, including the
Phase3 verification migration. Apply any missing migrations with the old SQL Server
release, after taking its backup. The transfer intentionally fails on missing
tables/columns, invalid constraints, or a populated PostgreSQL target.

Confirm the existing SQL Server datetime values represent UTC. Existing code uses
UTC, and the transfer preserves these clock values as UTC timestamps. If any legacy
data represents a different timezone, correct that conversion in a rehearsed import
before passing `--source-times-are-utc`.

## 3. Transfer all existing data during maintenance

Stop **every** API instance, checkout/payment worker and any other source writer;
keep public traffic in maintenance mode. Pause automatic deployments for the
cutover. For the known service name:

```bash
sudo systemctl stop aljawhara.service
```

Build the separate transfer tool locally with the .NET 10 SDK and copy its output
to a private maintenance directory on the VPS:

```bash
dotnet publish tools/Ecom.DatabaseTransfer -c Release -o artifacts/database-transfer
```

The tool uses two environment variables:

- `SQLSERVER_SOURCE_CONNECTION`: the existing SQL Server connection string, with a
  login that can SELECT all application tables and read identity metadata.
- `ConnectionStrings__DefaultConnection`: the new, empty PostgreSQL database.

Load these values securely in the maintenance shell. This example prompts without
putting passwords in shell history (Bash):

```bash
read -r -s -p 'SQL Server connection string: ' SQLSERVER_SOURCE_CONNECTION; echo
export SQLSERVER_SOURCE_CONNECTION
read -r -s -p 'PostgreSQL connection string: ' ConnectionStrings__DefaultConnection; echo
export ConnectionStrings__DefaultConnection
dotnet /path/to/private/database-transfer/Ecom.DatabaseTransfer.dll --source-times-are-utc
unset SQLSERVER_SOURCE_CONNECTION ConnectionStrings__DefaultConnection
```

Do not start the API to create the PostgreSQL database schema: the tool applies the
PostgreSQL baseline itself, **without seeding**. The tool then:

1. Locks and checks that every target application table is empty.
2. Holds read-only SQL Server table locks to prevent source changes during import.
3. Streams all application tables using binary COPY, preserving
   IDs, users, password hashes, orders, payments, inventory, reservations and audits.
   Foreign keys are temporarily deferred inside the target transaction to handle
   the existing user/address relationship cycle, then checked and restored before
   commit. Custom source tables/columns cause the tool to stop for review.
4. Verifies each table's row count, advances identity sequences, and reconciles
   daily order counters. PostgreSQL creates new concurrency tokens (`xmin`).
5. Commits target data as one transaction. A failure rolls back target data;
   the empty schema may remain and can be reused for a corrected retry.

Compare row counts, stock totals, coupon usage, order totals, payment statuses,
Arabic text and representative timestamps against the source. Test existing user
login, guest-order access and a sandbox checkout before admitting live traffic.
Row-count verification does not replace this business reconciliation.
Also check text-search and coupon-code case variants: PostgreSQL's default text
comparisons are case-sensitive, whereas a SQL Server installation may have used
a case-insensitive collation.

Keep **the same JWT and guest token derivation keys** so existing tokens/checkouts
continue working. Keep source payment workers stopped after cutover to avoid
duplicate payment processing. Delete the maintenance credentials from its shell.

## 4. Configure the production service

Copy `deploy/ubuntu/aljawhara.env.example` to `/etc/aljawhara/aljawhara.env` and
replace placeholders with your real values. Merge existing production secrets;
do not replace them with new random values. Keep this file outside Git and publish
directories:

```bash
sudo install -d -m 700 /etc/aljawhara
sudo install -m 600 deploy/ubuntu/aljawhara.env.example /etc/aljawhara/aljawhara.env
sudoedit /etc/aljawhara/aljawhara.env
sudo systemctl show aljawhara.service -p User -p Group
```

Create the backup directory owned by the service's actual account. Example for
`www-data` (substitute the user/group reported above):

```bash
sudo install -d -o www-data -g www-data -m 700 /var/backups/aljawhara
sudo install -d /etc/systemd/system/aljawhara.service.d
sudo install -m 644 deploy/ubuntu/environment.conf /etc/systemd/system/aljawhara.service.d/environment.conf
sudo systemctl daemon-reload
```

Deploy the new published API only after the transfer succeeds, then:

```bash
sudo systemctl restart aljawhara.service
sudo systemctl status aljawhara.service
sudo journalctl -u aljawhara.service --since '10 minutes ago'
```

The existing startup migration/seeding still runs. On a transferred database,
existing records remain in place. Check the private health endpoint and logs before
reopening traffic. Do not expose backups through Nginx or the API static files.

## 5. Daily backup behavior

In `Production`, backups are enabled by default; Development and Testing do not
start the backup worker. The defaults are:

| Environment setting | Default |
| --- | --- |
| `DatabaseBackup__Enabled` | `true` |
| `DatabaseBackup__Directory` | `/var/backups/aljawhara` |
| `DatabaseBackup__HourUtc` | `0` (03:00 Damascus) |
| `DatabaseBackup__RetentionDays` | `14` |
| `DatabaseBackup__TimeoutMinutes` | `60` |
| `DatabaseBackup__PgDumpPath` | `/usr/bin/pg_dump` |
| `DatabaseBackup__PgRestorePath` | `/usr/bin/pg_restore` |

The worker uses the **same environment connection string as the API** and runs
`pg_dump --format=custom` without a shell. Credentials go into the child process
environment, never command arguments. Backups are private files named
`aljawhara-YYYY-MM-DD.dump`. A `.partial` file becomes a completed backup only after
pg_dump succeeds and pg_restore can read its archive directory. This check is not
a full restore test.

On startup, the worker backs up the latest scheduled day if its completed archive
is missing. Failed runs retry after 15 minutes, with errors in the service journal.
Old completed backups are removed only after a new one succeeds. Multiple API
processes on the same backup volume coordinate through a file lock. Separate VPSs
need separate backup directories/coordination. The application must be running;
after downtime it takes one current backup, not historical snapshots of missed days.

Alert on `PostgreSQL backup failed` log entries and on a completed backup older
than 26 hours. Monitor disk space. Keep this directory outside the release folder
so deployments do not remove backups. PostgreSQL roles/global server settings and
R2 images are not part of the database dump; keep their configuration separately.

**A same-VPS backup does not protect against losing the VPS.** Copy completed
archives to a private off-server backup bucket or backup host, with separate
credentials and retention. Never use the public product-image bucket for database
backups. Off-server storage must be configured with your storage destination;
this change does not invent or provision a cloud bucket.

## 6. Verify a restore before relying on backups

Create a separate disposable PostgreSQL database owned by the application role.
Use a libpq password file with mode 600 (or an interactive password prompt), not
a password in command arguments. Example:

```bash
sudo -u postgres createdb --owner=aljawhara aljawhara_restore_check
pg_restore --host=127.0.0.1 --username=aljawhara --dbname=aljawhara_restore_check \
  --no-owner --no-acl --exit-on-error --single-transaction \
  /var/backups/aljawhara/aljawhara-YYYY-MM-DD.dump
```

Verify counts and representative orders/users on this database, ideally with a
separate sandbox application instance and payment/email workers disabled. Never
test by restoring over the live database. Repeat restore drills regularly.

Before live traffic is opened, rollback can point the old application release at
the unchanged SQL Server database. After new PostgreSQL writes/payments begin,
rollback requires reconciling those new writes; simply switching back loses them.

## Local verification

Integration tests use `ECOM_TEST_POSTGRES` pointing at a disposable PostgreSQL
server. Its role needs CREATEDB; tests create unique databases and remove them.
`ECOM_TEST_PG_BIN` optionally supplies a directory containing pg_dump/pg_restore.
The backup restore test actually dumps and restores a separate database.

```powershell
$env:ECOM_TEST_POSTGRES = 'Host=localhost;Database=postgres;Username=test_admin;Password=...'
$env:ECOM_TEST_PG_BIN = 'C:/Program Files/PostgreSQL/18/bin'
dotnet test tests/Ecom.CheckoutAudit.Tests
```

References: [Npgsql concurrency](https://www.npgsql.org/efcore/modeling/concurrency.html),
[Npgsql timestamps](https://www.npgsql.org/doc/types/datetime.html),
[PostgreSQL pg_dump](https://www.postgresql.org/docs/current/app-pgdump.html),
[pg_restore](https://www.postgresql.org/docs/current/app-pgrestore.html).
