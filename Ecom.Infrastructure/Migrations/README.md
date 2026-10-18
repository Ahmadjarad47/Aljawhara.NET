# Archived SQL Server migrations

These migrations are preserved for existing SQL Server schema upgrades and transfer
rehearsals with the previous application release. They are excluded from the
current PostgreSQL build.

The historical snapshot is named `EcomDbContextModelSnapshot.cs.archive` to keep
EF tooling from treating it as the active snapshot. Do not apply these migrations
to PostgreSQL. New migrations belong in `../PostgreSqlMigrations`.
