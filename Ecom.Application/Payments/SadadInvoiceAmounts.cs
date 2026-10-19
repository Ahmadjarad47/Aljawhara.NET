namespace Ecom.Application.Payments;

internal static class SadadInvoiceAmounts
{
    // Sadad may report subtotal as merchant proceeds after gateway fees.
    // The customer's amount and total must still match the full local charge.
    public static bool MatchesExpected(decimal expected, decimal amount, decimal total, decimal subtotal, decimal fees) =>
        expected > 0m && amount == expected && total == expected &&
        subtotal >= 0m && fees >= 0m && fees <= total && subtotal == total - fees &&
        HasKwdScale(expected) && HasKwdScale(amount) && HasKwdScale(total) &&
        HasKwdScale(subtotal) && HasKwdScale(fees);

    public static bool HasKwdScale(decimal value) =>
        decimal.Round(value, KwdMoney.Scale, MidpointRounding.AwayFromZero) == value;
}
