using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;

public static class TargetSchemaAudit
{
    public static async Task CheckAsync(string connectionString,IModel model)
    {
        await using var connection=new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var tx=await connection.BeginTransactionAsync();
        await using(var readOnly=new NpgsqlCommand("SET TRANSACTION READ ONLY",connection,tx))await readOnly.ExecuteNonQueryAsync();
        var columns=new Dictionary<string,(string Type,bool Nullable)>();
        await using(var command=new NpgsqlCommand("""
            SELECT c.relname,a.attname,format_type(a.atttypid,a.atttypmod),NOT a.attnotnull
            FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace JOIN pg_attribute a ON a.attrelid=c.oid
            WHERE n.nspname='public' AND c.relkind='r' AND a.attnum>0 AND NOT a.attisdropped
            """,connection,tx))
        await using(var reader=await command.ExecuteReaderAsync())
            while(await reader.ReadAsync())columns[reader.GetString(0)+"."+reader.GetString(1)]=(reader.GetString(2),reader.GetBoolean(3));
        var errors=new List<string>();
        foreach(var entity in model.GetEntityTypes()) {
            var table=entity.GetTableName()!;var store=StoreObjectIdentifier.Table(table,null);
            foreach(var p in entity.GetProperties()) {
                var column=p.GetColumnName(store)!;if(column=="xmin")continue;
                var key=table+"."+column;
                if(!columns.TryGetValue(key,out var actual)) {errors.Add(key);Console.WriteLine("ERROR: target missing "+key);continue;}
                if(actual.Type!=p.GetColumnType()||actual.Nullable!=p.IsNullable) {
                    errors.Add(key);Console.WriteLine($"ERROR: target {key}: {actual.Type} nullable={actual.Nullable}, expected {p.GetColumnType()} nullable={p.IsNullable}");
                }
            }
        }
        var constraints=new HashSet<string>();
        await using(var command=new NpgsqlCommand("""
            SELECT c.conname,c.convalidated,pg_get_constraintdef(c.oid)
            FROM pg_constraint c JOIN pg_namespace n ON n.oid=c.connamespace WHERE n.nspname='public'
            """,connection,tx))
        await using(var reader=await command.ExecuteReaderAsync())
        while(await reader.ReadAsync()) {
            constraints.Add(reader.GetString(0));
            Console.WriteLine($"TARGET CONSTRAINT {reader.GetString(0)}: {reader.GetString(2)}");
            if(!reader.GetBoolean(1))errors.Add(reader.GetString(0));
        }
        foreach(var entity in model.GetEntityTypes()) {
            if(entity.FindPrimaryKey() is {} pk && !constraints.Contains(pk.GetName()!))errors.Add(pk.GetName()!);
            foreach(var fk in entity.GetForeignKeys())if(!constraints.Contains(fk.GetConstraintName()!))errors.Add(fk.GetConstraintName()!);
        }
        errors.AddRange(await IndexAudit.TargetAsync(connection,tx,model));
        await tx.RollbackAsync();
        if(errors.Count>0)throw new InvalidOperationException($"Preflight: Target schema differs from current model ({errors.Count} mismatches): {string.Join(", ",errors)}.");
        Console.WriteLine("Target column types, nullability, primary keys and foreign keys checked read-only.");
    }
}

