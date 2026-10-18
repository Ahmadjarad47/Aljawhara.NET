using System.Security.Cryptography;
using System.Data;
using System.Text.Json;
using Ecom.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;

// This is deliberately a separate maintenance tool, never run by the API at startup.
if (!args.Contains("--preflight") && !args.Contains("--source-times-are-utc"))
{
    Console.Error.WriteLine("Stop source writes and confirm SQL Server dates represent UTC. Then pass --source-times-are-utc. See docs/POSTGRESQL_UBUNTU.md.");
    return 1;
}

try
{
    await TransferAsync(args.Contains("--preflight"), args.Contains("--rollback-test"), args.Contains("--simulate-failure-before-commit"));
    return 0;
}
catch (Exception exception)
{
    // Connection strings and source row data must not be printed.
    Console.Error.WriteLine($"Transfer failed ({exception.GetType().Name}). Target data transaction was rolled back; source data was not modified.");
    if (exception is InvalidOperationException && exception.Message.StartsWith("Preflight:", StringComparison.Ordinal))
        Console.Error.WriteLine(exception.Message);
    if (exception is PostgresException postgres)
        Console.Error.WriteLine($"PostgreSQL code: {postgres.SqlState}; table: {postgres.TableName}; column: {postgres.ColumnName}; constraint: {postgres.ConstraintName}.");
    if (exception is SqlException sqlServer)
        Console.Error.WriteLine($"SQL Server error number: {sqlServer.Number}.");
    return 1;
}

static async Task TransferAsync(bool preflight, bool rollbackTest, bool simulateFailure)
{
    var sourceString = Environment.GetEnvironmentVariable("SQLSERVER_SOURCE_CONNECTION")
        ?? throw new InvalidOperationException("Preflight: Set SQLSERVER_SOURCE_CONNECTION.");
    var targetString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
        ?? (preflight ? "Host=localhost;Database=unused" : throw new InvalidOperationException("Preflight: Set ConnectionStrings__DefaultConnection."));
    var options = new DbContextOptionsBuilder<EcomDbContext>().UseNpgsql(targetString).Options;
    await using var context = new EcomDbContext(options);
    await using var source = new SqlConnection(sourceString);
    await source.OpenAsync();
    await using var sourceTransaction = (SqlTransaction)await source.BeginTransactionAsync(IsolationLevel.Serializable);
    var sourceSchema = await LegacySchema.InspectAsync(source, sourceTransaction, context.Model, preflight);
    if (Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") is not null)
        await TargetSchemaAudit.CheckAsync(targetString, context.Model);
    if (preflight) { await sourceTransaction.RollbackAsync(); Console.WriteLine("Read-only preflight completed."); return; }
    if (source.Database != "dbOfEcom_migration_source" || new NpgsqlConnectionStringBuilder(targetString).Database != "ecom_migration_test")
        throw new InvalidOperationException("Preflight: Import is restricted to dbOfEcom_migration_source -> ecom_migration_test. Production cutover requires a separately reviewed release.");
    // Target schema must be provisioned separately. No schema migrations run here.
    var modelTables = context.Model.GetEntityTypes().Where(e => e.GetTableName() != null)
        .GroupBy(e => e.GetTableName()!).ToDictionary(g => g.Key, g => g.ToList());
    var remaining = modelTables.Keys.ToHashSet();
    var ordered = new List<string>();
    while (remaining.Count > 0)
    {
        var ready = remaining.Where(table => modelTables[table].SelectMany(e => e.GetForeignKeys())
            .All(fk => fk.PrincipalEntityType.GetTableName() == table ||
                !remaining.Contains(fk.PrincipalEntityType.GetTableName()!))).Order().ToList();
        // Existing users and shipping addresses contain a nullable FK cycle.
        // Defer FK checks during COPY, then validate every FK before committing.
        if (ready.Count == 0) ready = remaining.Order().ToList();
        ordered.AddRange(ready);
        remaining.ExceptWith(ready);
    }

    await using var target = new NpgsqlConnection(targetString);
    await target.OpenAsync();
    await using var transaction = await target.BeginTransactionAsync();
    // Prevent concurrent transfer runs, seeders or API writes while checking/importing.
    await using (var command = new NpgsqlCommand($"LOCK TABLE {string.Join(", ", ordered.Select(PgTable))} IN ACCESS EXCLUSIVE MODE", target, transaction))
        await command.ExecuteNonQueryAsync();
    foreach (var table in ordered)
    {
        await using var command = new NpgsqlCommand($"SELECT COUNT(*) FROM {PgTable(table)}", target, transaction);
        if (Convert.ToInt64(await command.ExecuteScalarAsync()) != 0)
            throw new InvalidOperationException($"Preflight: Target table {table} is populated. Use a new empty PostgreSQL database; nothing will be overwritten.");
    }

    var constraints = new List<(string Table, string Name, bool Deferrable, bool Deferred)>();
    await using (var command = new NpgsqlCommand("""
        SELECT t.relname, c.conname, c.condeferrable, c.condeferred
        FROM pg_constraint c JOIN pg_class t ON t.oid = c.conrelid
        JOIN pg_namespace n ON n.oid = t.relnamespace
        WHERE n.nspname = 'public' AND c.contype = 'f'
        """, target, transaction))
    await using (var reader = await command.ExecuteReaderAsync())
        while (await reader.ReadAsync()) constraints.Add((reader.GetString(0), reader.GetString(1), reader.GetBoolean(2), reader.GetBoolean(3)));
    foreach (var constraint in constraints)
    {
        await using var command = new NpgsqlCommand($"ALTER TABLE {PgTable(constraint.Table)} ALTER CONSTRAINT {PgQuote(constraint.Name)} DEFERRABLE INITIALLY DEFERRED", target, transaction);
        await command.ExecuteNonQueryAsync();
    }
    await using (var defer = new NpgsqlCommand("SET CONSTRAINTS ALL DEFERRED", target, transaction))
        await defer.ExecuteNonQueryAsync();

    await using (var precisionTable = new NpgsqlCommand("""
        CREATE TABLE IF NOT EXISTS public."__LegacyTimestampPrecision" (
            "TableName" text NOT NULL, "RowKey" text NOT NULL, "ColumnName" text NOT NULL,
            "OriginalTicks" bigint NOT NULL, PRIMARY KEY ("TableName", "RowKey", "ColumnName"))
        """, target, transaction)) await precisionTable.ExecuteNonQueryAsync();
    await using (var precisionEmpty = new NpgsqlCommand("SELECT COUNT(*) FROM public.\"__LegacyTimestampPrecision\"", target, transaction))
        if (Convert.ToInt64(await precisionEmpty.ExecuteScalarAsync()) != 0)
            throw new InvalidOperationException("Preflight: Timestamp archive is populated.");
    var sourceCounts = new Dictionary<string, long>();
    // Shared table locks live until the read-only source transaction ends. The old API
    // and payment workers MUST already be stopped to avoid long writer blocking.
    foreach (var table in ordered.Where(sourceSchema.ContainsKey))
    {
        await using var count = new SqlCommand($"SELECT COUNT_BIG(*) FROM {SqlTable(table)} WITH (TABLOCK, HOLDLOCK)", source, sourceTransaction) { CommandTimeout = 300 };
        sourceCounts[table] = Convert.ToInt64(await count.ExecuteScalarAsync());
    }

    foreach (var table in ordered.Where(sourceSchema.ContainsKey))
    {
        var storeObject = StoreObjectIdentifier.Table(table, null);
        var columns = modelTables[table].SelectMany(e => e.GetProperties())
            .Where(p => !p.IsConcurrencyToken || p.GetColumnName(storeObject) != "xmin")
            .Select(p => p.GetColumnName(storeObject)!).Distinct().ToList();
        var columnList = string.Join(", ", columns.Select(PgQuote));
        var types = new List<string>();
        await using (var schemaCommand = new NpgsqlCommand($"SELECT {columnList} FROM {PgTable(table)} LIMIT 0", target, transaction))
        await using (var schema = await schemaCommand.ExecuteReaderAsync())
            for (var index = 0; index < columns.Count; index++) types.Add(schema.GetDataTypeName(index));

        var keys = modelTables[table][0].FindPrimaryKey()!.Properties.Select(p => p.GetColumnName(storeObject)!).ToList();
        var sourceOrder = string.Join(", ", keys.Select(SqlQuote));
        var targetOrder = string.Join(", ", keys.Select(PgQuote));
        var sourceRows = new List<string>();
        var timestampArchive = new List<(string Key, string Column, long Ticks)>();
        using var sourceHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using var read = new SqlCommand($"SELECT {string.Join(", ", columns.Select(c => sourceSchema[table].Contains(c) ? SqlQuote(c) : "NULL AS " + SqlQuote(c)))} FROM {SqlTable(table)} ORDER BY {sourceOrder}", source, sourceTransaction) { CommandTimeout = 300 };
        await using var reader = await read.ExecuteReaderAsync(CommandBehavior.SequentialAccess);
        await using (var copy = await target.BeginBinaryImportAsync($"COPY {PgTable(table)} ({columnList}) FROM STDIN (FORMAT BINARY)"))
        {
            while (await reader.ReadAsync())
            {
                await copy.StartRowAsync();
                var rowValues = new object?[columns.Count];
                var rowTimestamps = new List<(string Column, long Ticks)>();
                for (var index = 0; index < columns.Count; index++)
                {
                    if (await reader.IsDBNullAsync(index)) { Reconciliation.Append(sourceHash, null); await copy.WriteNullAsync(); continue; }
                    var value = reader.GetValue(index);
                    rowValues[index] = value;
                    if (value is DateTime date && types[index] == "timestamp with time zone")
                    {
                        if (date.Ticks % 10 != 0) rowTimestamps.Add((columns[index], date.Ticks));
                        value = new DateTime(date.Ticks - date.Ticks % 10, DateTimeKind.Utc);
                    }
                    // SQL Server stores EF primitive collections as JSON; Npgsql uses
                    // native arrays. Preserve product image ordering and escaping.
                    if (value is string json && types[index] == "text[]")
                        value = JsonSerializer.Deserialize<string[]>(json)
                            ?? throw new InvalidOperationException($"Preflight: Invalid array in {table}.{columns[index]}.");
                    Reconciliation.Append(sourceHash, value);
                    await copy.WriteAsync(value, types[index]);
                }
                var rowKey = JsonSerializer.Serialize(keys.Select(k => rowValues[columns.IndexOf(k)]).ToArray());
                foreach (var item in rowTimestamps) timestampArchive.Add((rowKey, item.Column, item.Ticks));
                sourceRows.Add(Convert.ToHexString(sourceHash.GetHashAndReset()));
            }
            var copied = await copy.CompleteAsync();
            if ((long)copied != sourceCounts[table])
                throw new InvalidOperationException($"Preflight: Row-count mismatch in {table}.");
        }
        await reader.CloseAsync();
        foreach (var original in timestampArchive) {
            await using var archive = new NpgsqlCommand("INSERT INTO public.\"__LegacyTimestampPrecision\" VALUES (@table,@key,@column,@ticks)", target, transaction);
            archive.Parameters.AddWithValue("table", table);
            archive.Parameters.AddWithValue("key", original.Key);
            archive.Parameters.AddWithValue("column", original.Column);
            archive.Parameters.AddWithValue("ticks", original.Ticks);
            await archive.ExecuteNonQueryAsync();
        }
        Console.WriteLine($"{table}: {timestampArchive.Count} original submicrosecond timestamps archived.");
        var totals = modelTables[table].SelectMany(e => e.GetProperties())
            .Where(p => (Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType) == typeof(decimal) ||
                (table == "Products" && p.Name == "TotalInStock") || (table == "Coupons" && p.Name == "UsedCount"))
            .Select(p => p.GetColumnName(storeObject)!).Where(c => sourceSchema[table].Contains(c)).Distinct();
        foreach (var amountColumn in totals) {
            await using var sqlTotal = new SqlCommand($"SELECT COALESCE(SUM(CONVERT(decimal(38,3),{SqlQuote(amountColumn)})),0) FROM {SqlTable(table)}", source, sourceTransaction);
            var expected = Convert.ToDecimal(await sqlTotal.ExecuteScalarAsync());
            await using var pgTotal = new NpgsqlCommand($"SELECT COALESCE(SUM({PgQuote(amountColumn)}),0) FROM {PgTable(table)}", target, transaction);
            if (expected != Convert.ToDecimal(await pgTotal.ExecuteScalarAsync()))
                throw new InvalidOperationException($"Preflight: Total mismatch {table}.{amountColumn}.");
            Console.WriteLine($"TOTAL {table}.{amountColumn}: {expected.ToString(System.Globalization.CultureInfo.InvariantCulture)} verified.");
        }
        await using var verify = new NpgsqlCommand($"SELECT COUNT(*) FROM {PgTable(table)}", target, transaction);
        if (Convert.ToInt64(await verify.ExecuteScalarAsync()) != sourceCounts[table])
            throw new InvalidOperationException($"Preflight: Target row-count mismatch in {table}.");
        using var targetHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var targetRows = new List<string>();
        await using (var reconcile = new NpgsqlCommand($"SELECT {columnList} FROM {PgTable(table)}", target, transaction))
        await using (var rows = await reconcile.ExecuteReaderAsync())
            while (await rows.ReadAsync()) {
                for (var index = 0; index < columns.Count; index++) Reconciliation.Append(targetHash, rows.GetValue(index));
                targetRows.Add(Convert.ToHexString(targetHash.GetHashAndReset()));
            }
        sourceRows.Sort(StringComparer.Ordinal);
        targetRows.Sort(StringComparer.Ordinal);
        if (!sourceRows.SequenceEqual(targetRows))
            throw new InvalidOperationException($"Preflight: Full-row reconciliation failed in {table}.");
        Console.WriteLine($"{table}: {sourceCounts[table]} rows verified.");
    }

    // Explicit IDs don't advance PostgreSQL identity sequences. Keep at least the SQL
    // Server identity watermark, including IDs of deleted rows, to avoid reusing them.
    foreach (var table in ordered.Where(sourceSchema.ContainsKey))
    {
        foreach (var property in modelTables[table].SelectMany(e => e.GetProperties())
            .Where(p => p.IsPrimaryKey() && p.ValueGenerated == ValueGenerated.OnAdd && p.ClrType == typeof(int)))
        {
            var column = property.GetColumnName(StoreObjectIdentifier.Table(table, null))!;
            await using var watermark = new SqlCommand("SELECT CONVERT(bigint, IDENT_CURRENT(@table))", source, sourceTransaction);
            watermark.Parameters.AddWithValue("@table", $"dbo.{SqlQuote(table)}");
            var sourceIdentity = await watermark.ExecuteScalarAsync();
            await using var maximum = new NpgsqlCommand($"SELECT COALESCE(MAX({PgQuote(column)}),0)::bigint FROM {PgTable(table)}", target, transaction);
            var next = checked(Math.Max(Convert.ToInt64(await maximum.ExecuteScalarAsync()), sourceIdentity is null or DBNull ? 0L : Convert.ToInt64(sourceIdentity)) + 1);
            await using var sequence = new NpgsqlCommand("SELECT pg_get_serial_sequence(@table,@column)", target, transaction);
            sequence.Parameters.AddWithValue("table", PgTable(table));
            sequence.Parameters.AddWithValue("column", column);
            var sequenceName = (string?)await sequence.ExecuteScalarAsync() ?? throw new InvalidOperationException("Preflight: Missing identity sequence.");
            await using var reset = new NpgsqlCommand($"ALTER SEQUENCE {sequenceName} RESTART WITH {next}", target, transaction);
            await reset.ExecuteNonQueryAsync();
        }
    }

    // Raise today's counter above legacy ORD-yyyyMMdd-NNNN identifiers as well.
    await using (var counters = new NpgsqlCommand("""
        INSERT INTO "OrderNumberCounters" ("DateKey", "NextValue")
        SELECT substring("OrderNumber" from 5 for 8)::integer,
               max(substring("OrderNumber" from 14)::integer)
        FROM "Orders" WHERE "OrderNumber" ~ '^ORD-[0-9]{8}-[0-9]{1,9}$'
        GROUP BY substring("OrderNumber" from 5 for 8)::integer
        ON CONFLICT ("DateKey") DO UPDATE
        SET "NextValue" = GREATEST("OrderNumberCounters"."NextValue", EXCLUDED."NextValue")
        """, target, transaction))
        await counters.ExecuteNonQueryAsync();
    await using (var validate = new NpgsqlCommand("SET CONSTRAINTS ALL IMMEDIATE", target, transaction))
        await validate.ExecuteNonQueryAsync();
    foreach (var constraint in constraints)
    {
        var original = constraint.Deferrable
            ? "DEFERRABLE INITIALLY " + (constraint.Deferred ? "DEFERRED" : "IMMEDIATE")
            : "NOT DEFERRABLE INITIALLY IMMEDIATE";
        await using var command = new NpgsqlCommand($"ALTER TABLE {PgTable(constraint.Table)} ALTER CONSTRAINT {PgQuote(constraint.Name)} {original}", target, transaction);
        await command.ExecuteNonQueryAsync();
    }
    await sourceTransaction.RollbackAsync(); // Source is read-only.
    if (simulateFailure) throw new InvalidOperationException("Preflight: Injected failure before commit; verify target rollback.");
    if (rollbackTest) {
        await transaction.RollbackAsync();
        Console.WriteLine("Rehearsal passed: all imported rows, constraints, timestamp archive and identity changes rolled back.");
        return;
    }
    await transaction.CommitAsync();
    Console.WriteLine("Transfer committed. Source database is unchanged. Reconcile business totals and test checkout before opening production traffic.");
}

static string PgQuote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
static string SqlQuote(string value) => "[" + value.Replace("]", "]]") + "]";
static string PgTable(string table) => "public." + PgQuote(table);
static string SqlTable(string table) => "dbo." + SqlQuote(table);
