namespace Compass.Domain;

public enum Tag { Eligible, Reach, NotEligible, GoodFit, Affordable, Stretch, OverBudget, StaleData, Estimate }

public sealed record Recommendation(
    Course Course,
    EligibilityResult Eligibility,
    CostBreakdown Cost,
    Affordability Affordability,
    double FitScore,
    double Score,
    IReadOnlyList<Tag> Tags,
    IReadOnlyList<string> Reasons);

/// <summary>Inputs that come from outside the domain: city costs, one-off costs and AI fit scores (0..1).</summary>
public sealed record RecommendationContext(
    IReadOnlyDictionary<string, decimal> MonthlyLivingByCity,
    decimal OneOffEur,
    IReadOnlyDictionary<int, double> FitScores,
    DateOnly Today);

/// <summary>
/// Combines deterministic eligibility and cost rules with an AI-provided fit score.
/// The AI can change the order of courses, never whether a student is eligible or what a course costs.
/// </summary>
public sealed class RecommendationEngine(EligibilityEvaluator eligibility, CostCalculator costs)
{
    public const double GoodFitThreshold = 0.6;
    public const int StaleAfterDays = 180;

    public IReadOnlyList<Recommendation> Recommend(StudentProfile student, IEnumerable<Course> courses, RecommendationContext ctx)
    {
        return courses
            .Select(c => Build(student, c, ctx))
            .OrderBy(r => r.Eligibility.Status == EligibilityStatus.NotEligible)
            .ThenByDescending(r => r.Score)
            .ToList();
    }

    private Recommendation Build(StudentProfile s, Course c, RecommendationContext ctx)
    {
        var elig = eligibility.Evaluate(s, c);
        if (!ctx.MonthlyLivingByCity.TryGetValue(c.City, out var monthly))
            throw new InvalidOperationException($"No living-cost data for {c.City}.");
        var cost = costs.Calculate(c, monthly, ctx.OneOffEur);
        var afford = costs.Assess(cost, s.BudgetEur);
        var fit = Math.Clamp(ctx.FitScores.GetValueOrDefault(c.Id, 0.5), 0, 1);

        var tags = new List<Tag>();
        var reasons = new List<string>();

        tags.Add(elig.Status switch
        {
            EligibilityStatus.Eligible => Tag.Eligible,
            EligibilityStatus.Borderline => Tag.Reach,
            _ => Tag.NotEligible
        });
        reasons.AddRange(elig.Checks.Select(x => x.Detail));

        if (fit >= GoodFitThreshold)
        {
            tags.Add(Tag.GoodFit);
            reasons.Add($"Course content matches your interests (fit {fit:P0}).");
        }

        tags.Add(afford switch
        {
            Affordability.Affordable => Tag.Affordable,
            Affordability.Stretch => Tag.Stretch,
            _ => Tag.OverBudget
        });
        reasons.Add(afford switch
        {
            Affordability.Affordable => $"Estimated total €{cost.Total:N0} is within your €{s.BudgetEur:N0} budget.",
            Affordability.Stretch => $"Estimated total €{cost.Total:N0} is slightly above your €{s.BudgetEur:N0} budget.",
            _ => $"Estimated total €{cost.Total:N0} is well above your €{s.BudgetEur:N0} budget."
        });

        var ageDays = ctx.Today.DayNumber - c.VerifiedAt.DayNumber;
        if (ageDays > StaleAfterDays)
        {
            tags.Add(Tag.StaleData);
            reasons.Add($"Course details were last verified {ageDays} days ago. Check the official page.");
        }
        if (c.IsEstimate) tags.Add(Tag.Estimate);

        var eligScore = elig.Status switch { EligibilityStatus.Eligible => 1.0, EligibilityStatus.Borderline => 0.5, _ => 0.0 };
        var affordScore = afford switch { Affordability.Affordable => 1.0, Affordability.Stretch => 0.5, _ => 0.0 };
        var score = Math.Round(0.6 * fit + 0.25 * affordScore + 0.15 * eligScore, 4);

        return new Recommendation(c, elig, cost, afford, fit, score, tags, reasons);
    }
}
