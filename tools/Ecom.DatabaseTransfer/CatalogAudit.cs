using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

public static class CatalogAudit
{
    public static async Task<List<string>> ReadAsync(SqlConnection source, SqlTransaction tx, IModel model)
    {
        var errors = new List<string>();
        var entities = model.GetEntityTypes().Where(e=>e.GetTableName()!=null).GroupBy(e=>e.GetTableName()!).ToDictionary(g=>g.Key,g=>g.ToList());
        await using(var command = new SqlCommand("""
            SELECT t.name,c.name,ty.name,c.max_length,c.precision,c.scale,c.is_nullable,c.is_identity
            FROM sys.tables t JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id
            WHERE t.schema_id=SCHEMA_ID('dbo') AND t.name<>'__EFMigrationsHistory' ORDER BY t.name,c.column_id
            """,source,tx))
        await using(var reader=await command.ExecuteReaderAsync())
        while(await reader.ReadAsync()) {
            var table=reader.GetString(0); var column=reader.GetString(1); var type=reader.GetString(2);
            if(!entities.TryGetValue(table,out var group))continue;
            var store=StoreObjectIdentifier.Table(table,null);
            var property=group.SelectMany(e=>e.GetProperties()).FirstOrDefault(p=>p.GetColumnName(store)==column);
            Console.WriteLine($"COLUMN {table}.{column}: SQL {type} length={reader.GetInt16(3)} precision={reader.GetByte(4)} scale={reader.GetByte(5)} nullable={reader.GetBoolean(6)} identity={reader.GetBoolean(7)} -> {property?.GetColumnType() ?? "unmapped"}");
            if(property==null)continue;
            var clr=Nullable.GetUnderlyingType(property.ClrType)??property.ClrType;
            if(clr.IsEnum)clr=Enum.GetUnderlyingType(clr);
            var valid=clr==typeof(string) ? type is "nvarchar" or "varchar" or "nchar" or "char" or "text" or "ntext" :
                clr==typeof(double) ? (type=="float" || (type is "decimal" or "numeric" && property.GetColumnType()!.StartsWith("numeric"))) : clr==typeof(float) ? type=="real" :
                clr==typeof(int) ? type=="int" : clr==typeof(long) ? type=="bigint" : clr==typeof(bool) ? type=="bit" :
                clr==typeof(decimal) ? type is "decimal" or "numeric" or "money" or "smallmoney" :
                clr==typeof(DateTime) ? type is "datetime2" or "datetime" or "smalldatetime" :
                clr==typeof(DateTimeOffset) ? type=="datetimeoffset" :
                clr==typeof(byte[]) ? type is "varbinary" or "binary" or "timestamp" :
                property.GetColumnType()=="text[]" && type=="nvarchar";
            if(!valid) { errors.Add(table+"."+column);Console.WriteLine($"ERROR: unsupported source type for {table}.{column}"); }
        }
        var actualFks=new HashSet<string>();
        await using(var command=new SqlCommand("""
            SELECT t.name,c.name,rt.name,rc.name,fk.is_disabled,fk.is_not_trusted
            FROM sys.foreign_keys fk JOIN sys.foreign_key_columns f ON f.constraint_object_id=fk.object_id
            JOIN sys.tables t ON t.object_id=f.parent_object_id JOIN sys.columns c ON c.object_id=t.object_id AND c.column_id=f.parent_column_id
            JOIN sys.tables rt ON rt.object_id=f.referenced_object_id JOIN sys.columns rc ON rc.object_id=rt.object_id AND rc.column_id=f.referenced_column_id
            WHERE t.schema_id=SCHEMA_ID('dbo')
            """,source,tx))
        await using(var reader=await command.ExecuteReaderAsync())
        while(await reader.ReadAsync()) {
            var signature=$"{reader.GetString(0)}.{reader.GetString(1)}->{reader.GetString(2)}.{reader.GetString(3)}";
            actualFks.Add(signature); Console.WriteLine("SOURCE FK "+signature);
            if(reader.GetBoolean(4)||reader.GetBoolean(5)) {errors.Add(signature);Console.WriteLine("ERROR: disabled/untrusted FK "+signature);}
        }
        foreach(var group in entities)
        foreach(var fk in group.Value.SelectMany(e=>e.GetForeignKeys())) {
            var principal=fk.PrincipalEntityType.GetTableName()!;
            for(var i=0;i<fk.Properties.Count;i++) {
                var signature=$"{group.Key}.{fk.Properties[i].GetColumnName()}->{principal}.{fk.PrincipalKey.Properties[i].GetColumnName()}";
                if(!actualFks.Contains(signature)) {
                    var mapped=LegacySchema.NewTables.Contains(group.Key)||LegacySchema.NewTables.Contains(principal);
                    Console.WriteLine($"{(mapped ? "MAPPED NEW FK" : "ERROR: missing source FK")} {signature}");
                    if(!mapped)errors.Add(signature);
                }
            }
        }
        errors.AddRange(await IndexAudit.SourceAsync(source,tx,model));
        return errors;
    }
}



