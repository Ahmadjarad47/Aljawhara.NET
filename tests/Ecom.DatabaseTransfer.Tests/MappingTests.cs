using System.Security.Cryptography;
using Xunit;

public class MappingTests
{
    private static string Digest(params object?[] values) {
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var v in values) Reconciliation.Append(h,v);
        return Convert.ToHexString(h.GetHashAndReset());
    }
    [Fact] public void Decimal_scale_is_not_a_business_value_change() => Assert.Equal(Digest(12.30m),Digest(12.300m));
    [Fact] public void Arabic_and_password_hash_bytes_are_significant() {
        Assert.NotEqual(Digest("العربية", "AQAAAAEA"),Digest("العربية", "AQAAAAEB"));
        Assert.Equal(Digest("العربية", "AQAAAAEA"),Digest("العربية", "AQAAAAEA"));
    }
    [Fact] public void Null_empty_and_column_boundaries_are_distinct() {
        Assert.NotEqual(Digest((object?)null),Digest(""));
        Assert.NotEqual(Digest("ab","c"),Digest("a","bc"));
    }
    [Fact] public void Financial_changes_are_detected() => Assert.NotEqual(Digest(12.345m),Digest(12.346m));
    [Fact] public void Timestamp_ticks_survive_utc_labeling() {
        var t=new DateTime(2025,1,2,3,4,5,DateTimeKind.Unspecified).AddTicks(123);
        Assert.Equal(Digest(t),Digest(DateTime.SpecifyKind(t,DateTimeKind.Utc)));
    }
    [Fact] public void Mapping_cannot_invent_payment_evidence_or_reservations() {
        Assert.Equal(6,LegacySchema.NewTables.Count);
        Assert.Contains("PaymentAttempts",LegacySchema.NewTables);
        Assert.Contains("Transactions.GatewayVerifiedAtUtc",LegacySchema.NullableAdditions);
        Assert.DoesNotContain("Transactions.Status",LegacySchema.NullableAdditions);
        Assert.DoesNotContain("Products.TotalInStock",LegacySchema.NullableAdditions);
        Assert.DoesNotContain("Coupons.UsedCount",LegacySchema.NullableAdditions);
    }
}
