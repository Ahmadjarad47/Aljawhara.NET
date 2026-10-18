using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Data;

// Only reviewed additions may be absent. Never infer defaults for arbitrary columns.
public static class LegacySchema
{
    public static readonly HashSet<string> NewTables = new(StringComparer.Ordinal) {
        "PaymentAttempts", "InventoryReservations", "CouponReservations", "OutboxMessages", "PaymentTransitionAudits", "OrderNumberCounters" };
    public static readonly HashSet<string> NullableAdditions = new(StringComparer.Ordinal) {
        "Transactions.GatewayVerifiedAtUtc", "Transactions.GatewayInvoiceId", "Categories.Image",
        "Orders.GuestAccessExpiresAtUtc", "Orders.GuestAccessRevokedAtUtc", "Orders.GuestAccessTokenHash",
        "Orders.CheckoutIdempotencyKeyHash", "Orders.CheckoutRequestHash", "Orders.GuestTokenDerivationNonce", "Orders.ReservationExpiresAtUtc" };
    public static async Task<Dictionary<string, HashSet<string>>> InspectAsync(SqlConnection source, SqlTransaction tx,
        IModel model, bool reportOnly)
    {
        var schema = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        await using (var command = new SqlCommand("SELECT TABLE_NAME,COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='dbo' AND TABLE_NAME <> '__EFMigrationsHistory' ORDER BY TABLE_NAME,ORDINAL_POSITION", source, tx))
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync()) {
                if (!schema.TryGetValue(reader.GetString(0), out var columns)) schema[reader.GetString(0)] = columns = new(StringComparer.Ordinal);
                columns.Add(reader.GetString(1));
            }
        var errors = await CatalogAudit.ReadAsync(source, tx, model);
        var tables = model.GetEntityTypes().Where(e => e.GetTableName() != null).GroupBy(e => e.GetTableName()!);
        foreach (var table in tables) {
            if (!schema.TryGetValue(table.Key, out var actual)) {
                Console.WriteLine($"{(NewTables.Contains(table.Key) ? "MAPPED EMPTY" : "ERROR")}: missing table {table.Key}");
                if (!NewTables.Contains(table.Key)) errors.Add(table.Key);
                continue;
            }
            var store = StoreObjectIdentifier.Table(table.Key, null);
            var expected = table.SelectMany(e => e.GetProperties()).Where(p => p.GetColumnName(store) != "xmin").Select(p => p.GetColumnName(store)!).ToHashSet();
            foreach (var missing in expected.Except(actual)) {
                var mapped = NullableAdditions.Contains(table.Key + "." + missing);
                Console.WriteLine($"{(mapped ? "MAPPED NULL" : "ERROR")}: missing column {table.Key}.{missing}");
                if (!mapped) errors.Add(table.Key + "." + missing);
            }
            foreach (var extra in actual.Except(expected)) {
                Console.WriteLine($"{(extra == "RowVersion" ? "MAPPED xmin" : "ERROR")}: source column {table.Key}.{extra}");
                if (extra != "RowVersion") errors.Add(table.Key + "." + extra);
            }
            foreach (var property in table.SelectMany(e => e.GetProperties()).Where(p => (Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType) == typeof(decimal))) {
                var column = property.GetColumnName(store)!;
                if (!actual.Contains(column)) continue;
                var q = "[" + column.Replace("]", "]]") + "]";
                var t = "[" + table.Key.Replace("]", "]]") + "]";
                await using var check = new SqlCommand($"SELECT COUNT_BIG(*) FROM dbo.{t} WHERE {q} IS NOT NULL AND (TRY_CONVERT(decimal(18,3),{q}) IS NULL OR {q} <> TRY_CONVERT(decimal(18,3),{q}))", source, tx);
                if (Convert.ToInt64(await check.ExecuteScalarAsync()) > 0) {
                    errors.Add(table.Key + "." + column); Console.WriteLine($"ERROR: precision loss {table.Key}.{column}");
                }
            }
        }
        var names = tables.Select(g => g.Key).ToHashSet();
        foreach (var extra in schema.Keys.Except(names)) { errors.Add(extra); Console.WriteLine($"ERROR: unmapped table {extra}"); }
        await using (var history = new SqlCommand("SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId", source, tx))
        await using (var reader = await history.ExecuteReaderAsync())
            while (await reader.ReadAsync()) Console.WriteLine("Source migration: " + reader.GetString(0));
        errors.AddRange(await LegacyDataAudit.CheckAsync(source, tx, model, schema));
        if (errors.Count > 0) throw new InvalidOperationException($"Preflight: {errors.Count} unsupported schema/data differences; no import permitted.");
        return schema;
    }
}


