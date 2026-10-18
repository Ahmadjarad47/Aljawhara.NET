using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;

public static class IndexAudit
{
    public static async Task<List<string>> SourceAsync(SqlConnection source,SqlTransaction tx,IModel model)
    {
        var actual=new Dictionary<string,(string Columns,bool Unique)>();
        await using(var command=new SqlCommand("""
            SELECT t.name,i.name,STRING_AGG(CONVERT(nvarchar(max),c.name),',') WITHIN GROUP (ORDER BY ic.key_ordinal),i.is_unique
            FROM sys.tables t JOIN sys.indexes i ON i.object_id=t.object_id
            JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
            JOIN sys.columns c ON c.object_id=t.object_id AND c.column_id=ic.column_id
            WHERE t.schema_id=SCHEMA_ID('dbo') AND i.is_primary_key=0 AND ic.key_ordinal>0
            GROUP BY t.name,i.name,i.is_unique
            """,source,tx))
        await using(var reader=await command.ExecuteReaderAsync())
        while(await reader.ReadAsync())actual[reader.GetString(0)+"."+reader.GetString(1)]=(reader.GetString(2),reader.GetBoolean(3));
        var errors=new List<string>();
        foreach(var entity in model.GetEntityTypes())foreach(var index in entity.GetIndexes()) {
            var table=entity.GetTableName()!;var key=table+"."+index.GetDatabaseName();
            if(!actual.TryGetValue(key,out var found)) {
                var mapped=LegacySchema.NewTables.Contains(table)||index.Properties.All(p=>LegacySchema.NullableAdditions.Contains(table+"."+p.GetColumnName()));
                Console.WriteLine($"{(mapped ? "MAPPED NEW INDEX" : "ERROR: missing source index")} {key}");
                if(!mapped)errors.Add(key);
            } else if(found.Columns!=string.Join(',',index.Properties.Select(p=>p.GetColumnName()))||found.Unique!=index.IsUnique) {
                Console.WriteLine("ERROR: source index definition differs "+key);errors.Add(key);
            }
        }
        return errors;
    }
    public static async Task<List<string>> TargetAsync(NpgsqlConnection connection,NpgsqlTransaction tx,IModel model)
    {
        var actual=new Dictionary<string,(string Columns,bool Unique)>();
        await using(var command=new NpgsqlCommand("""
            SELECT t.relname,i.relname,string_agg(a.attname,',' ORDER BY k.ordinality),x.indisunique
            FROM pg_index x JOIN pg_class t ON t.oid=x.indrelid JOIN pg_class i ON i.oid=x.indexrelid
            JOIN pg_namespace n ON n.oid=t.relnamespace
            CROSS JOIN LATERAL unnest(x.indkey) WITH ORDINALITY k(attnum,ordinality)
            JOIN pg_attribute a ON a.attrelid=t.oid AND a.attnum=k.attnum
            WHERE n.nspname='public' AND k.ordinality<=x.indnkeyatts
            GROUP BY t.relname,i.relname,x.indisunique
            """,connection,tx))
        await using(var reader=await command.ExecuteReaderAsync())
        while(await reader.ReadAsync())actual[reader.GetString(0)+"."+reader.GetString(1)]=(reader.GetString(2),reader.GetBoolean(3));
        var errors=new List<string>();
        foreach(var entity in model.GetEntityTypes())foreach(var index in entity.GetIndexes()) {
            var key=entity.GetTableName()+"."+index.GetDatabaseName();
            if(!actual.TryGetValue(key,out var found)||found.Columns!=string.Join(',',index.Properties.Select(p=>p.GetColumnName()))||found.Unique!=index.IsUnique) {
                errors.Add(key);Console.WriteLine("ERROR: target index differs "+key);
            }
        }
        return errors;
    }
}
