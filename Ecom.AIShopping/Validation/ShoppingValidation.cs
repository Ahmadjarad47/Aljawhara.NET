using Ecom.AIShopping.Contracts;

namespace Ecom.AIShopping.Validation;

public sealed class ShoppingException(string code, int status = 400) : Exception(code)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}

public sealed class ShoppingLimits
{
    public int MaxMessageLength { get; set; } = 2000;
    public int MaxTurns { get; set; } = 30;
    public int MaxNeeds { get; set; } = 15;
    public int MaxPackages { get; set; } = 100;
    public int HistoryTurns { get; set; } = 6;
    public int ConversationMinutes { get; set; } = 120;
}

public static class ShoppingValidation
{
    public static void Plan(ShoppingPlan plan, ShoppingLimits limits)
    {
        if (plan is null || plan.People is < 1 or > 200 || plan.Budget is < 0 or > 10000 ||
            plan.Needs is null || plan.Needs.Length > limits.MaxNeeds || plan.ExcludedNeeds is null ||
            plan.ExcludedNeeds.Length > limits.MaxNeeds || !Text(plan.Occasion, 100) ||
            plan.Clarification?.Length > 300 || plan.ExcludedNeeds.Any(x => !Text(x, 80)) ||
            plan.Needs.Select(x => x?.Key).Distinct(StringComparer.Ordinal).Count() != plan.Needs.Length)
            throw new ShoppingException("INVALID_PLAN", 502);
        foreach (var need in plan.Needs)
        {
            if (need is null || !Text(need.Key, 80) || !Text(need.Label, 100) || !Text(need.Query, 160) ||
                need.Material?.Length > 80 || need.UnitsPerPerson is < 0 or > 10 ||
                need.FixedUnits is < 0 or > 1000 || need.SelectedProductId is <= 0 ||
                (need.PackageBased && need.UnitsPerPerson != 0) ||
                (need.UnitsPerPerson == 0 && need.FixedUnits == 0))
                throw new ShoppingException("INVALID_PLAN", 502);
        }
    }

    public static bool Text(string? text, int max) => !string.IsNullOrWhiteSpace(text) && text.Length <= max;
    public static decimal Money(decimal value) => decimal.Round(value, 3, MidpointRounding.AwayFromZero);
    public static int Packages(int units, int packageSize) => units <= 0 || packageSize <= 0
        ? throw new ShoppingException("INVALID_QUANTITY")
        : checked((units + packageSize - 1) / packageSize);
}
