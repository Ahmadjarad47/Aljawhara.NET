using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

public static class LegacyDataAudit
{
    public static async Task<List<string>> CheckAsync(SqlConnection source, SqlTransaction tx, IModel model, Dictionary<string,HashSet<string>> schema)
    {
        var errors=new List<string>();
        static string Q(string s)=>"["+s.Replace("]","]]")+"]";
        foreach(var entity in model.GetEntityTypes()) {
            var table=entity.GetTableName()!;
            if(!schema.TryGetValue(table,out var columns))continue;
            var store=StoreObjectIdentifier.Table(table,null);
            foreach(var p in entity.GetProperties()) {
                var column=p.GetColumnName(store)!;
                if(!columns.Contains(column))continue;
                var predicates=new List<string>();
                if(!p.IsNullable)predicates.Add(Q(column)+" IS NULL");
                if(p.ClrType==typeof(string) && p.GetMaxLength() is int length)
                    predicates.Add($"LEN({Q(column)}+N'#')-1>{length}");
                var clr=Nullable.GetUnderlyingType(p.ClrType)??p.ClrType;
                if(clr.IsEnum)predicates.Add($"{Q(column)} NOT IN ({string.Join(',',Enum.GetValues(clr).Cast<object>().Select(Convert.ToInt64))})");
                if(predicates.Count>0) {
                    await using var command=new SqlCommand($"SELECT COUNT_BIG(*) FROM dbo.{Q(table)} WHERE {string.Join(" OR ",predicates)}",source,tx);
                    var count=Convert.ToInt64(await command.ExecuteScalarAsync());
                    if(count>0) {errors.Add(table+"."+column);Console.WriteLine($"ERROR: {table}.{column}: {count} null/length/enum violations");}
                }
                if(p.GetColumnType()=="text[]") {
                    await using var jsonCommand=new SqlCommand($"SELECT {Q(column)} FROM dbo.{Q(table)} WHERE {Q(column)} IS NOT NULL",source,tx);
                    await using var jsonRows=await jsonCommand.ExecuteReaderAsync();
                    long invalid=0;
                    while(await jsonRows.ReadAsync()) {
                        try { if(System.Text.Json.JsonSerializer.Deserialize<string[]>(jsonRows.GetString(0)) is null)invalid++; }
                        catch(System.Text.Json.JsonException) {invalid++;}
                    }
                    if(invalid>0) {errors.Add(table+"."+column);Console.WriteLine($"ERROR: {table}.{column}: {invalid} invalid JSON arrays");}
                }
                if(clr==typeof(DateTime)) {
                    await using var command=new SqlCommand($"SELECT COUNT_BIG(*) FROM dbo.{Q(table)} WHERE DATEPART(NANOSECOND,{Q(column)})%1000<>0",source,tx);
                    Console.WriteLine($"TIMESTAMP ARCHIVE {table}.{column}: {await command.ExecuteScalarAsync()} exact tick records required");
                }
            }
        }
        foreach(var rule in new[]{("Products","TotalInStock < 0"),("OrderItems","Quantity <= 0"),("Coupons","UsedCount < 0 OR UsageLimit < 0 OR UsedCount > UsageLimit")}) {
            if(!schema.ContainsKey(rule.Item1))continue;
            await using var command=new SqlCommand($"SELECT COUNT_BIG(*) FROM dbo.{Q(rule.Item1)} WHERE {rule.Item2}",source,tx);
            var count=Convert.ToInt64(await command.ExecuteScalarAsync());
            if(count>0) {errors.Add(rule.Item1);Console.WriteLine($"ERROR: {rule.Item1}: {count} target check violations");}
        }
        return errors;
    }
}

