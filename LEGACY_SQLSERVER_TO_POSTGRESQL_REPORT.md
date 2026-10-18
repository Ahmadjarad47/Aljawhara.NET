# Legacy SQL Server to PostgreSQL report

Date: 2026-10-18. **VPS TEST transfer executed and reconciled. No production migration performed.**

## Scope and decision

Improved the existing `tools/Ecom.DatabaseTransfer` executable. SQL Server commands issued by it are SELECT statements in a serializable transaction, rolled back at completion. No EF migrations, UPDATE, INSERT, DELETE, restore, drop, or database creation are executed on SQL Server. Import is deliberately restricted to `dbOfEcom_migration_source` -> `ecom_migration_test`. The production databases `dbOfEcom` and `ecom_prod` were not modified. Existing working tree was clean at inspection; only task files were changed.

**Decision: direct, explicit transformation of the six-migration restored source.** Applying pending archives to another disposable restore is a viable alternative, but adds source DDL without supplying missing payment/reservation business history. The pending archives only relax shipping nullability, introduce nullable security fields and empty new tables, narrow decimal integer capacity, and add constraints/indexes. They do not backfill verified payments or reservations. Direct transformation gives equivalent business preservation without changing even the restored source.

The older status migration is ALREADY applied in this source. Its `ELSE 0` maps unknown string statuses to Pending and drops the original column. It must not be used as an automatic repair for an earlier/unrecognized source. This tool rejects an incompatible status type; it does not reinterpret unknown statuses. Tests inspect actual archived migration operations, including the pending no-backfill behavior, unsafe old status fallback, and decimal range reduction. **The pending archives were not applied to a second disposable database during this session**; validation used their operations and the actual unmodified restored source.

## Complete observed source history

1. `20251126130951_intital`: original application/Identity schema.
2. `20251126152743_intital2`: Arabic carousel fields.
3. `20251126154433_intital3`: carousel product URL.
4. `20251202205433_update shipping address`: six optional Arabic address fields.
5. `20260212113603_UpdateTransactionStatusAndGatewayInvoiceId`: integer transaction status and gateway invoice ID.
6. `20260303220508_intital category`: category image.

Unapplied archives inspected: `20261017161057_MakeShippingLocationFieldsOptional`, `20261018054031_Phase1CheckoutSecurity`, `20261018060740_Phase2ProductionCheckout`, `20261018061240_Phase2FinancialInvariants`, `20261018062035_Phase2ActivePaymentAttemptInvariant`, `20261018063147_Phase3SadadVerificationEvidence`. The archived SQL Server snapshot remains excluded from the production build. PostgreSQL uses its existing InitialPostgreSql baseline.

## Explicit mapping

All existing tables map `dbo.<name>` -> `public."<name>"`; all existing columns map by exact name, without applying EF query filters (including deleted/inactive records). The [field-by-field column mapping](docs/legacy-transfer-evidence/SCHEMA_MAPPING.md) lists every existing column. The SQL type/precision/nullability/identity -> PostgreSQL type inventory and FK definitions are recorded in [VPS transfer evidence](docs/legacy-transfer-evidence/vps-transfer.txt). No source business columns are discarded. Unmapped tables/columns block import.

| Existing table | Rows copied | Rule |
|---|---:|---|
| AspNetRoles | 2 | Preserve IDs, names, normalization, concurrency stamp |
| AspNetRoleClaims | 0 | Preserve claim IDs/role relationships |
| AspNetUsers | 3 | Preserve IDs, hashes, security/concurrency stamps, refresh tokens, flags |
| AspNetUserClaims | 0 | Preserve all values and relationships |
| AspNetUserLogins | 0 | Preserve composite keys and provider values |
| AspNetUserRoles | 1 | Preserve user/role links |
| AspNetUserTokens | 0 | Preserve composite keys and token values |
| Carousels | 5 | Preserve Arabic/English text, price, URLs |
| Categories | 16 | Preserve IDs, Arabic text, image |
| SubCategories | 36 | Preserve IDs/category FK |
| Products | 584 | Preserve prices and current stock, without stock movements |
| ProductDetails | 14 | Preserve labels/values/product FK |
| ProductVariants | 42 | Preserve variants/product FK |
| ProductVariantValues | 57 | Preserve values/prices/variant FK |
| Ratings | 2899 | Preserve numeric(2,1) and product FK |
| HealthPings | 10 | Preserve all records |
| Visitors | 1296 | Preserve all records |
| Coupons | 15 | Preserve UsedCount, limits, amounts, validity; no usage increments |
| ShippingAddresses | 68 | Preserve user/order links, Arabic address fields and nulls |
| Orders | 102 | Preserve IDs, number, statuses, totals, links and history |
| OrderItems | 134 | Preserve IDs, order/product links, quantities and prices |
| Transactions | 51 | Preserve IDs, integer status, invoice IDs, amounts and dates |

Provider transformations:

- SQL Server JSON primitive image collections -> native PostgreSQL text arrays, preserving ordering and escaping. Invalid arrays block preflight.
- Legacy decimal(18,2) financial columns -> numeric(18,3). Every populated decimal column is tested with TRY_CONVERT and equality: no overflow and no rounding allowed. decimal(18,3) has only 15 integer digits, versus 16 previously; increasing scale is NOT a blanket safe widening. No double/float conversion is used for financial amounts.
- SQL Server rowversion, when present, -> PostgreSQL-generated xmin. Version tokens are provider concurrency metadata, not historical business values.
- SQL datetime2 -> timestamp with time zone under the explicit UTC interpretation flag. PostgreSQL supports microseconds; SQL Server supports 100 ns. Business columns contain truncated microseconds. Every nonrepresentable original is retained losslessly in `public."__LegacyTimestampPrecision"` with table name, JSON primary-key tuple, column name and original .NET ticks. 5,106 originals were archived. Recover the original with `new DateTime(OriginalTicks, DateTimeKind.Utc)`. Include this table in backups and any rollback/export. Exact 100 ns values are NOT available through ordinary business timestamp columns alone.

Missing current columns explicitly initialized NULL: `Orders.GuestAccessExpiresAtUtc`, `GuestAccessRevokedAtUtc`, `GuestAccessTokenHash`, `CheckoutIdempotencyKeyHash`, `CheckoutRequestHash`, `GuestTokenDerivationNonce`, `ReservationExpiresAtUtc`; `Transactions.GatewayVerifiedAtUtc`. The compatibility allowlist also permits absent `Categories.Image` and `Transactions.GatewayInvoiceId` when nullable; neither is absent in this restored source. No existing value is replaced with a default.

| New table | Initialization rule |
|---|---|
| PaymentAttempts | Empty: invoice/status alone is not dispatch, idempotency or verification evidence |
| InventoryReservations | Empty: historical stock is already the source of truth; no stock deduction or reservation replay |
| CouponReservations | Empty: preserve coupon UsedCount; no usage replay |
| OutboxMessages | Empty: do not redispatch historic payments or notifications |
| PaymentTransitionAudits | Empty: do not manufacture historical transitions |
| OrderNumberCounters | 36 date rows derived only from matching existing ORD-yyyyMMdd-NNNN numbers, taking the largest suffix per date |

Paid stays Paid, Pending stays Pending. Observed transactions: 36 Pending, 15 Paid on both sides. No gateway-verified timestamp, amount, evidence ID, audit or new payment attempt was invented. Legacy pending payments need a separate operator reconciliation workflow before any retry; existing checkout code requires an active inventory reservation for payment retry and therefore will not automatically retry these legacy orders. Guest links are not fabricated. No external gateway calls were made.

Authentication: all Identity tables copied directly, including hashes/stamps/token fields. Tests verify the current Identity hasher accepts V2/V3 hashes. No passwords are reset. Actual-user password login was not tested because plaintext passwords were not supplied; imported complete-row fingerprints include the original authentication fields.

## Safety and validation

Preflight inventories all source tables/columns, source types/FKs and migration history; checks financial representability, nulls, string lengths, enums, JSON arrays, stock/quantity/coupon invariants, and reports required timestamp archives. It checks configured target column types/nullability, PK/FK names, validated constraint state and model indexes read-only. Known missing new tables/nullable fields are explicitly reported. A source-only preflight can omit the target environment variable. All detectable unsupported differences are collected rather than stopping at the first missing table. It is not a general equivalence prover for arbitrary custom SQL expressions, collations, triggers or filtered-index predicates; custom schemas still require review of the printed catalog.

Import requires all 28 target application tables empty under exclusive locks; reruns against populated targets fail without overwrite. Source shared locks hold a consistent serializable view. Stop source writers for planned transfer to avoid blocking; current test source is a restored copy. Use a SELECT-only source login in deployment. ApplicationIntent=ReadOnly is an intent hint, not a server-enforced permission boundary; the session used the supplied SQL login and executed only SELECT commands.

COPY, timestamp archive DDL/data, counter initialization, FK deferral/restoration and sequence changes share ONE PostgreSQL transaction. ALTER SEQUENCE RESTART replaces nontransactional setval so sequence watermarks roll back too. Explicit IDs preserve source identity watermarks (including deleted IDs). Deferred FK cycles are fully checked before commit. No schema migrations or seeding run in the importer; target schema provisioning is separate.

Per table, counts and sorted SHA-256 complete-row fingerprints compare all copied fields (including IDs, FK values, Arabic text, authentication values, statuses and transformed timestamp representations). Hashes are not printed. Decimal sums, inventory stock and coupon usage totals are independently compared inside the transaction. Row fingerprints are held in memory; plan capacity for larger future datasets. PostgreSQL constraints provide final FK/check/unique enforcement.

## Executed tests and VPS results

- Release build and self-contained Ubuntu linux-x64 publish: passed.
- 10 local tests: passed (archived decision, exact financial fingerprinting, Arabic/hash sensitivity, null/boundary handling, timestamp interpretation, mapping restrictions, Identity V2/V3 compatibility).
- Actual restored SQL Server six-migration/22-table source -> PostgreSQL **18.6** on the VPS: preflight passed.
- Full import followed by rollback: passed.
- Deliberate failure immediately before commit: rolled back.
- Repeat full rollback rehearsal after failure: passed. Harness snapshots verify table counts/schema visibility and every existing sequence's last_value/is_called before and after each run.
- Actual test import: committed; complete-row fingerprints, counts, totals, all FK checks passed.
- Second import into populated test target: correctly refused; no records overwritten.
- Initial rehearsal exposed datetime2 submicrosecond loss and rolled back. Timestamp archive was implemented and subsequent rehearsals passed.

[VPS rehearsal evidence](docs/legacy-transfer-evidence/vps-rehearsals.txt) records the automated real-database runs. No database was dropped, recreated, cleared or truncated for these tests. These tests exercise actual legacy data rather than a newly generated approximation. Pending archived DDL was not executed on a second clone, and exhaustive synthetic malformed-schema fixtures have not been implemented.

[Final schema/index preflight](docs/legacy-transfer-evidence/vps-final-preflight.txt), [populated-target refusal](docs/legacy-transfer-evidence/vps-populated-target-rejection.txt), and [independent postcommit output](docs/legacy-transfer-evidence/ecom-legacy-postcommit.txt) are retained separately. The final added index audit passed read-only against the committed test database.

Independent postcommit checks:

| Check | Result |
|---|---:|
| Orders / sum Total | 102 / 789.870 |
| Transactions / sum Amount | 51 / 506.520 |
| Products / sum TotalInStock | 584 / 29717 |
| Coupons / sum UsedCount | 15 / 0 |
| OrderItems / sum Price | 134 / 543.970 |
| Unvalidated public constraints | 0 |
| New payment/reservation/outbox/audit records | 0 |
| Exact timestamp archive records | 5106 |
| Order number counter dates | 36 |
| Every identity sequence above current max ID | true (22 sequences checked) |

Order totals and payment totals intentionally are not required to equal each other: existing unpaid/other historical business states are preserved.

## Exact build and test commands

From repository root on a .NET 10 SDK host:

```powershell
dotnet test tests/Ecom.DatabaseTransfer.Tests -c Release
dotnet publish tools/Ecom.DatabaseTransfer -c Release -r linux-x64 --self-contained true -o artifacts/database-transfer/linux-x64
tar -czf artifacts/database-transfer/linux-x64.tar.gz -C artifacts/database-transfer/linux-x64 .
scp artifacts/database-transfer/linux-x64.tar.gz root@72.61.187.165:/tmp/ecom-legacy-transfer.tar.gz
scp tests/Ecom.DatabaseTransfer.Tests/rehearse.py root@72.61.187.165:/tmp/rehearse.py
```

On the VPS (existing prepared, empty test target only; current target is now populated, so do NOT repeat import/rehearsals against it):

```bash
install -d -m 755 /tmp/ecom-legacy-review
tar -xzf /tmp/ecom-legacy-transfer.tar.gz -C /tmp/ecom-legacy-review
chmod 755 /tmp/ecom-legacy-review/Ecom.DatabaseTransfer
set +o history
read -rsp 'SQL Server password: ' SQL_PASSWORD; printf '\n'
export SQLSERVER_SOURCE_CONNECTION="Server=localhost;Database=dbOfEcom_migration_source;User Id=sa;Password=${SQL_PASSWORD};TrustServerCertificate=true;ApplicationIntent=ReadOnly"
unset SQL_PASSWORD
export ConnectionStrings__DefaultConnection='Host=/var/run/postgresql;Database=ecom_migration_test;Username=postgres'
# Run through postgres OS account for PostgreSQL peer authentication:
runuser -m -u postgres -- /tmp/ecom-legacy-review/Ecom.DatabaseTransfer --preflight > /tmp/ecom-preflight.txt 2>&1
# Only before the first committed test import, and after confirming UTC semantics:
runuser -m -u postgres -- python3 /tmp/rehearse.py /tmp/ecom-legacy-review/Ecom.DatabaseTransfer > /tmp/ecom-rehearsals.txt 2>&1
runuser -m -u postgres -- /tmp/ecom-legacy-review/Ecom.DatabaseTransfer --source-times-are-utc > /tmp/ecom-transfer.txt 2>&1
unset SQLSERVER_SOURCE_CONNECTION SQLCMDPASSWORD
```

Use the actual SELECT-only username instead of sa when provisioned. Do not put credentials in committed files, command arguments or logs. Inspect process exit codes; a populated test target is intentionally refused. Source-only preflight does not require the UTC assertion and does not connect to a target if its variable is absent. Exit code 0 indicates successful preflight/rehearsal/import; 1 indicates rejection or rollback. Retain logs securely.

Postcommit read-only validation:

```bash
sudo -u postgres psql -X -d ecom_migration_test <<'SQL'
SELECT count(*),sum("Total") FROM "Orders";
SELECT "Status",count(*),sum("Amount") FROM "Transactions" GROUP BY "Status" ORDER BY "Status";
SELECT count(*),sum("TotalInStock") FROM "Products";
SELECT count(*),sum("UsedCount") FROM "Coupons";
SELECT count(*) FROM "__LegacyTimestampPrecision";
SELECT * FROM "OrderNumberCounters" ORDER BY "DateKey";
SELECT c.conname FROM pg_constraint c JOIN pg_namespace n ON n.oid=c.connamespace
WHERE n.nspname='public' AND NOT c.convalidated;
SELECT format('SELECT %L,last_value > (SELECT COALESCE(max(%I),0) FROM public.%I) FROM public.%I;',
 t.relname,a.attname,t.relname,s.relname)
FROM pg_class s JOIN pg_depend d ON d.objid=s.oid JOIN pg_class t ON t.oid=d.refobjid
JOIN pg_attribute a ON a.attrelid=t.oid AND a.attnum=d.refobjsubid
WHERE s.relkind='S' AND d.deptype IN ('a','i') ORDER BY t.relname \gexec
SQL
```

## Production cutover remains outstanding

**No claim of production migration success.** This release intentionally refuses production import names. A production cutover requires a separately reviewed release enabling the exact production destination, a fresh backup/restore rehearsal, confirmed historical timezone semantics, source write freeze, gateway reconciliation of legacy Pending transactions, current-user login smoke test, and application checkout/payment smoke tests against the reconciled test database. No production DB should be dropped/recreated and no historical data should be deleted to make checks pass. Keep the original SQL backup, target backup and timestamp precision archive. Failed imports roll back automatically; a successful test import is deliberately retained for review and is not automatically deleted.
