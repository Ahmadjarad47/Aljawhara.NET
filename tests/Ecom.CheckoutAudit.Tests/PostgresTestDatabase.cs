using Npgsql;

namespace Ecom.CheckoutAudit.Tests;

internal static class PostgresTestDatabase
{
    // Integration tests always create uniquely named databases, never the supplied database.
    public static string ConnectionString(string databaseName)
    {
        var configured = Environment.GetEnvironmentVariable("ECOM_TEST_POSTGRES")
            ?? throw new InvalidOperationException("Set ECOM_TEST_POSTGRES to a PostgreSQL test server connection string (role needs CREATEDB).");
        return new NpgsqlConnectionStringBuilder(configured) { Database = databaseName }.ConnectionString;
    }
}
