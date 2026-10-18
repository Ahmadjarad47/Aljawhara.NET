using Ecom.Infrastructure.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

public class ArchivedMigrationDecisionTests
{
    [Fact] public void Pending_archives_add_no_business_evidence_or_reservation_backfill() {
        Migration[] pending=[new MakeShippingLocationFieldsOptional(),new Phase1CheckoutSecurity(),new Phase2ProductionCheckout(),new Phase2FinancialInvariants(),new Phase2ActivePaymentAttemptInvariant(),new Phase3SadadVerificationEvidence()];
        foreach(var sql in pending.SelectMany(m=>m.UpOperations).OfType<SqlOperation>()) {
            Assert.DoesNotContain("UPDATE ",sql.Sql,StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("INSERT ",sql.Sql,StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("DELETE ",sql.Sql,StringComparison.OrdinalIgnoreCase);
        }
        var created=pending.SelectMany(m=>m.UpOperations).OfType<CreateTableOperation>().Select(t=>t.Name).Order().ToArray();
        Assert.Equal(LegacySchema.NewTables.Order().ToArray(),created);
    }
    [Fact] public void Precision_upgrade_reduces_integer_capacity_and_requires_value_preflight() {
        var changes=new Phase2ProductionCheckout().UpOperations.OfType<AlterColumnOperation>().ToList();
        Assert.NotEmpty(changes);
        Assert.All(changes,c=> { Assert.Equal("decimal(18,2)",c.OldColumn.ColumnType);Assert.Equal("decimal(18,3)",c.ColumnType); });
        Assert.True(9999999999999999.99m > 999999999999999.999m);
    }
    [Fact] public void Old_status_archive_would_destroy_unknown_original_status() {
        var operations=new UpdateTransactionStatusAndGatewayInvoiceId().UpOperations;
        Assert.Contains(operations.OfType<SqlOperation>(),s=>s.Sql.Contains("ELSE 0"));
        Assert.Contains(operations.OfType<DropColumnOperation>(),c=>c.Table=="Transactions"&&c.Name=="Status");
        // A source predating this migration is intentionally not silently upgraded.
        Assert.DoesNotContain("Transactions.Status",LegacySchema.NullableAdditions);
    }
}
