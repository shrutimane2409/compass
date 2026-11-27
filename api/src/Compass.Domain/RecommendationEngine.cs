namespace Compass.Domain;

public enum Tag
{
    Eligible, LikelyEligible, Reach, NotEligible, GoodFit,
    Affordable, Stretch, OverBudget, StaleData, Estimate, FeeYearUnconfirmed
}

public sealed record Recommendation(
    Course Course,
    EligibilityResult Eligibility,
    CostBreakdown Cost,
    Affordability Affordability,
    double FitScore,
    double Score,
    ScoreBreakdown Breakdown,
    IReadOnlyList<Tag> Tags,
    IReadOnlyList<string> Reasons);

/// <summary>Inputs from outside the domain: city costs, one-off costs, AI fit scores (0..1), the date, and the student's priority.</summary>
public sealed record RecommendationContext(
    IReadOnlyDictionary<string, decimal> MonthlyLivingByCity,
    decimal OneOffEur,
    IReadOnlyDictionary<int, double> FitScores,
    DateOnly Today,
    Priority Priority = Priority.Balanced);

/// <summary>
/// Combines deterministic eligibility and cost rules with an AI-provided fit score.
/// The AI can change the order of courses, never whether a student is eligible or what a course costs.
/// </summary>
public sealed class RecommendationEngine(EligibilityEvaluator eligibility, CostCalculator costs)
{
    public const double GoodFitThreshold = 0.6;
    public const double NeutralFit = 0.5;
    public const int StaleAfterDays = 180;

    public IReadOnlyList<Recommendation> Recommend(StudentProfile student, IEnumerable<Course> courses, RecommendationContext ctx)
    {
        var weights = RankingWeights.For(ctx.Priority);
        var intake = AcademicYear.UpcomingIntake(ctx.Today);
        return courses
            .Select(c => Build(student, c, ctx, weights, intake))
            .OrderBy(r => r.Eligibility.Status == EligibilityStatus.NotEligible)
            .ThenByDescending(r => r.Score)
            .ThenBy(r => r.Cost.Total)
            .ThenBy(r => r.Course.Id)
            .ToList();
    }

    private Recommendation Build(StudentProfile s, Course c, RecommendationContext ctx, RankingWeights weights, string intake)
    {
        var elig = eligibility.Evaluate(s, c);
        if (!ctx.MonthlyLivingByCity.TryGetValue(c.City, out var monthly))
            throw new InvalidOperationException($"No living-cost data for {c.City}.");
        var cost = costs.Calculate(c, monthly, ctx.OneOffEur);
        var afford = costs.Assess(cost, s.BudgetEur);
        var hasFit = ctx.FitScores.TryGetValue(c.Id, out var rawFit);
        var fit = hasFit ? Math.Clamp(rawFit, 0, 1) : NeutralFit;

        var tags = new List<Tag>();
        var reasons = new List<string>();

        tags.Add(elig.Status switch
        {
            EligibilityStatus.Eligible => Tag.Eligible,
            EligibilityStatus.LikelyEligible => Tag.LikelyEligible,
            EligibilityStatus.Borderline => Tag.Reach,
            _ => Tag.NotEligible
        });
        reasons.AddRange(elig.Checks.Select(x => x.Detail));

        if (hasFit && fit >= GoodFitThreshold)
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

        var feeYear = AcademicYear.StartYear(c.FeeAcademicYear);
        var intakeYear = AcademicYear.StartYear(intake)!.Value;
        if (feeYear is null)
        {
            tags.Add(Tag.FeeYearUnconfirmed);
            reasons.Add($"The page doesn't say which year the fee applies to. Confirm the {intake} fee with the university.");
        }
        else if (feeYear < intakeYear)
        {
            tags.Add(Tag.FeeYearUnconfirmed);
            reasons.Add($"The fee shown is for {c.FeeAcademicYear}. Fees for the {intake} intake may be higher.");
        }

        var ageDays = ctx.Today.DayNumber - c.VerifiedAt.DayNumber;
        if (ageDays > StaleAfterDays)
        {
            tags.Add(Tag.StaleData);
            reasons.Add($"Course details were last verified {ageDays} days ago. Check the official page.");
        }
        if (c.IsEstimate) tags.Add(Tag.Estimate);

        var headline = Scoring.HeadlineRank(c.Rankings);
        if (weights.Reputation > 0)
            reasons.Add(headline is null
                ? "The university isn't in the QS or THE world rankings."
                : $"Ranked {headline.RankText} in the {headline.Source} World University Rankings {headline.Edition}.");

        var breakdown = Scoring.Score(
            fit,
            Scoring.AffordabilityScore(afford, cost.Total, s.BudgetEur),
            Scoring.EligibilityScore(elig.Status),
            weights,
            Scoring.ReputationScore(c.Rankings));

        return new Recommendation(c, elig, cost, afford, fit, breakdown.Total, breakdown, tags, reasons);
    }
}
