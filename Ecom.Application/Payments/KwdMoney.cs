namespace Ecom.Application.Payments;

public static class KwdMoney
{
    public const int Scale = 3;
    public static decimal Round(decimal value) => decimal.Round(value, Scale, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Compares monetary values at the precision actually settled for KWD. Sadad
    /// serializes some values through a binary floating-point pipeline (for example
    /// 354.66900000000004), so comparing the raw JSON decimal would reject the same
    /// three-decimal amount. A complete fils still changes the rounded value and is
    /// therefore rejected.
    /// </summary>
    public static bool EqualsAtSettlementPrecision(decimal left, decimal right) =>
        Round(left) == Round(right);
}
