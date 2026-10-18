namespace Ecom.Application.Payments;

public static class KwdMoney
{
    public const int Scale = 3;
    public static decimal Round(decimal value) => decimal.Round(value, Scale, MidpointRounding.AwayFromZero);
}
