using System.Data;
using System.Text.Json;
using Ecom.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;

// This is deliberately a separate maintenance tool, never run by the API at startup.
if (!args.Contains("--source-times-are-utc"))
{
    Console.Error.WriteLine("Stop source writes and confirm SQL Server dates represent UTC. Then pass --source-times-are-utc. See docs/POSTGRESQL_UBUNTU.md.");
    return 1;
}

try
{
    await TransferAsync();
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

static async Task TransferAsync()
{
    var sourceString = Environment.GetEnvironmentVariable("SQLSERVER_SOURCE_CONNECTION")
        ?? throw new InvalidOperationException("Preflight: Set SQLSERVER_SOURCE_CONNECTION.");
    var targetString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
        ?? throw new InvalidOperationException("Preflight: Set ConnectionStrings__DefaultConnection.");
    var options = new DbContextOptionsBuilder<EcomDbContext>().UseNpgsql(targetString).Options;
    await using var context = new EcomDbContext(options);
    await context.Database.MigrateAsync(); // Schema only: never seed the empty target before transfer.
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

    await using var source = new SqlConnection(sourceString);
    await source.OpenAsync();
    await using var sourceTransaction = (SqlTransaction)await source.BeginTransactionAsync(IsolationLevel.Serializable);
    await using (var inventory = new SqlCommand("SELECT name FROM sys.tables WHERE schema_id = SCHEMA_ID('dbo') AND is_ms_shipped = 0 AND name <> '__EFMigrationsHistory'", source, sourceTransaction))
    await using (var reader = await inventory.ExecuteReaderAsync())
    {
        var sourceTables = new HashSet<string>();
        while (await reader.ReadAsync()) sourceTables.Add(reader.GetString(0));
        if (!sourceTables.SetEquals(modelTables.Keys))
            throw new InvalidOperationException("Preflight: Source tables differ from the application model. Review custom tables or missing SQL Server migrations before transferring.");
    }
    var sourceCounts = new Dictionary<string, long>();
    // Shared table locks live until the read-only source transaction ends. The old API
    // and payment workers MUST already be stopped to avoid long writer blocking.
    foreach (var table in ordered)
    {
        await using var count = new SqlCommand($"SELECT COUNT_BIG(*) FROM {SqlTable(table)} WITH (TABLOCK, HOLDLOCK)", source, sourceTransaction) { CommandTimeout = 300 };
        sourceCounts[table] = Convert.ToInt64(await count.ExecuteScalarAsync());
    }

    foreach (var table in ordered)
    {
        var storeObject = StoreObjectIdentifier.Table(table, null);
        var columns = modelTables[table].SelectMany(e => e.GetProperties())
            .Where(p => !p.IsConcurrencyToken || p.GetColumnName(storeObject) != "xmin")
            .Select(p => p.GetColumnName(storeObject)!).Distinct().ToList();
        var columnList = string.Join(", ", columns.Select(PgQuote));
        var expectedSourceColumns = columns.ToHashSet();
        if (modelTables[table].SelectMany(e => e.GetProperties()).Any(p => p.IsConcurrencyToken && p.GetColumnName(storeObject) == "xmin"))
            expectedSourceColumns.Add("RowVersion");
        await using (var inventory = new SqlCommand("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @table", source, sourceTransaction))
        {
            inventory.Parameters.AddWithValue("@table", table);
            await using var sourceSchema = await inventory.ExecuteReaderAsync();
            var sourceColumns = new HashSet<string>();
            while (await sourceSchema.ReadAsync()) sourceColumns.Add(sourceSchema.GetString(0));
            if (!sourceColumns.SetEquals(expectedSourceColumns))
                throw new InvalidOperationException($"Preflight: Source columns differ in {table}. Review custom columns or missing SQL Server migrations before transferring.");
        }
        var types = new List<string>();
        await using (var schemaCommand = new NpgsqlCommand($"SELECT {columnList} FROM {PgTable(table)} LIMIT 0", target, transaction))
        await using (var schema = await schemaCommand.ExecuteReaderAsync())
            for (var index = 0; index < columns.Count; index++) types.Add(schema.GetDataTypeName(index));

        await using var read = new SqlCommand($"SELECT {string.Join(", ", columns.Select(SqlQuote))} FROM {SqlTable(table)}", source, sourceTransaction) { CommandTimeout = 300 };
        await using var reader = await read.ExecuteReaderAsync(CommandBehavior.SequentialAccess);
        await using (var copy = await target.BeginBinaryImportAsync($"COPY {PgTable(table)} ({columnList}) FROM STDIN (FORMAT BINARY)"))
        {
            while (await reader.ReadAsync())
            {
                await copy.StartRowAsync();
                for (var index = 0; index < columns.Count; index++)
                {
                    if (await reader.IsDBNullAsync(index)) { await copy.WriteNullAsync(); continue; }
                    var value = reader.GetValue(index);
                    if (value is DateTime date && types[index] == "timestamp with time zone")
                        value = DateTime.SpecifyKind(date, DateTimeKind.Utc);
                    // SQL Server stores EF primitive collections as JSON; Npgsql uses
                    // native arrays. Preserve product image ordering and escaping.
                    if (value is string json && types[index] == "text[]")
                        value = JsonSerializer.Deserialize<string[]>(json)
                            ?? throw new InvalidOperationException($"Preflight: Invalid array in {table}.{columns[index]}.");
                    await copy.WriteAsync(value, types[index]);
                }
            }
            var copied = await copy.CompleteAsync();
            if ((long)copied != sourceCounts[table])
                throw new InvalidOperationException($"Preflight: Row-count mismatch in {table}.");
        }
        await reader.CloseAsync();
        await using var verify = new NpgsqlCommand($"SELECT COUNT(*) FROM {PgTable(table)}", target, transaction);
        if (Convert.ToInt64(await verify.ExecuteScalarAsync()) != sourceCounts[table])
            throw new InvalidOperationException($"Preflight: Target row-count mismatch in {table}.");
        Console.WriteLine($"{table}: {sourceCounts[table]} rows verified.");
    }

    // Explicit IDs don't advance PostgreSQL identity sequences. Keep at least the SQL
    // Server identity watermark, including IDs of deleted rows, to avoid reusing them.
    foreach (var table in ordered)
    {
        foreach (var property in modelTables[table].SelectMany(e => e.GetProperties())
            .Where(p => p.IsPrimaryKey() && p.ValueGenerated == ValueGenerated.OnAdd && p.ClrType == typeof(int)))
        {
            var column = property.GetColumnName(StoreObjectIdentifier.Table(table, null))!;
            await using var watermark = new SqlCommand("SELECT CONVERT(bigint, IDENT_CURRENT(@table))", source, sourceTransaction);
            watermark.Parameters.AddWithValue("@table", $"dbo.{SqlQuote(table)}");
            var sourceIdentity = await watermark.ExecuteScalarAsync();
            await using var reset = new NpgsqlCommand($"""
                SELECT setval(pg_get_serial_sequence(@table, @column),
                    GREATEST(COALESCE((SELECT MAX({PgQuote(column)}) FROM {PgTable(table)}), 1), @watermark), true)
                """, target, transaction);
            reset.Parameters.AddWithValue("table", PgTable(table));
            reset.Parameters.AddWithValue("column", column);
            reset.Parameters.AddWithValue("watermark", sourceIdentity is null or DBNull ? 1L : Convert.ToInt64(sourceIdentity));
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
    await transaction.CommitAsync();
    Console.WriteLine("Transfer committed. Source database is unchanged. Reconcile business totals and test checkout before opening production traffic.");
}

static string PgQuote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
static string SqlQuote(string value) => "[" + value.Replace("]", "]]") + "]";
static string PgTable(string table) => "public." + PgQuote(table);
static string SqlTable(string table) => "dbo." + SqlQuote(table);
