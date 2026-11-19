namespace Compass.Domain;

public enum Affordability { Affordable, Stretch, OverBudget }

public sealed record CostBreakdown(decimal Tuition, decimal Living, decimal OneOff)
{
    public decimal Total => Tuition + Living + OneOff;
}

/// <summary>Real cost of a course: tuition + monthly living for the course duration + one-off costs.</summary>
public sealed class CostCalculator
{
    public const decimal StretchMargin = 0.10m;

    public CostBreakdown Calculate(Course c, decimal monthlyLivingEur, decimal oneOffEur)
    {
        if (monthlyLivingEur < 0) throw new ArgumentOutOfRangeException(nameof(monthlyLivingEur));
        if (oneOffEur < 0) throw new ArgumentOutOfRangeException(nameof(oneOffEur));
        return new CostBreakdown(c.FeeNonEuEur, monthlyLivingEur * c.DurationMonths, oneOffEur);
    }

    public Affordability Assess(CostBreakdown cost, decimal budgetEur) =>
        cost.Total <= budgetEur ? Affordability.Affordable
        : cost.Total <= budgetEur * (1 + StretchMargin) ? Affordability.Stretch
        : Affordability.OverBudget;
}
