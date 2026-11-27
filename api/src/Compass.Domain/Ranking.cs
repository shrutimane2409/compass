namespace Compass.Domain;

/// <summary>What the student says matters most. Changes the weights, never eligibility or cost.</summary>
public enum Priority { Balanced, BestFit, LowestCost, Reputation }

/// <summary>
/// Reputation is 0 unless the student picks the Reputation priority: rankings are shown to everyone,
/// but only steer the order when the student says they matter.
/// </summary>
public sealed record RankingWeights(double Fit, double Affordability, double Eligibility, double Reputation = 0)
{
    public static RankingWeights For(Priority p) => p switch
    {
        Priority.BestFit => new(0.70, 0.15, 0.15),
        Priority.LowestCost => new(0.25, 0.60, 0.15),
        Priority.Reputation => new(0.30, 0.25, 0.15, 0.30),
        _ => new(0.50, 0.30, 0.20),
    };
}

/// <summary>Each component is 0..1. Total = weighted sum. Returned so the UI can show why a course ranks where it does.</summary>
public sealed record ScoreBreakdown(double Fit, double Affordability, double Eligibility, double Total, RankingWeights Weights, double Reputation = 0);

public static class Scoring
{
    /// <summary>Fraction of the budget left over at which a course earns the full affordability score.</summary>
    public const decimal FullHeadroom = 0.25m;

    public static double EligibilityScore(EligibilityStatus s) => s switch
    {
        EligibilityStatus.Eligible => 1.0,
        EligibilityStatus.LikelyEligible => 0.75,
        EligibilityStatus.Borderline => 0.4,
        _ => 0.0,
    };

    /// <summary>Affordable courses score 0.7..1 by how much budget they leave; a stretch scores 0.35; over budget 0.</summary>
    public static double AffordabilityScore(Affordability a, decimal total, decimal budget)
    {
        if (a == Affordability.Stretch) return 0.35;
        if (a != Affordability.Affordable || budget <= 0) return 0.0;
        var headroom = Math.Clamp((budget - total) / (budget * FullHeadroom), 0m, 1m);
        return 0.7 + 0.3 * (double)headroom;
    }

    /// <summary>The source used for the reputation score: QS (the edition most students quote), else THE.</summary>
    public static UniversityRank? HeadlineRank(IReadOnlyList<UniversityRank> ranks) =>
        ranks.Where(r => r.Source == "QS").OrderByDescending(r => r.Edition).FirstOrDefault()
        ?? ranks.Where(r => r.Source == "THE").OrderByDescending(r => r.Edition).FirstOrDefault();

    /// <summary>Coarse bands, because a few places in a league table are noise: top 100 = 1, unranked = 0.1.</summary>
    public static double ReputationScore(IReadOnlyList<UniversityRank> ranks) => HeadlineRank(ranks)?.RankLow switch
    {
        null => 0.1,
        <= 100 => 1.0,
        <= 200 => 0.85,
        <= 300 => 0.7,
        <= 400 => 0.6,
        <= 600 => 0.45,
        <= 800 => 0.3,
        _ => 0.15,
    };

    public static ScoreBreakdown Score(double fit, double afford, double elig, RankingWeights w, double reputation = 0) =>
        new(Math.Round(fit, 4), Math.Round(afford, 4), Math.Round(elig, 4),
            Math.Round(w.Fit * fit + w.Affordability * afford + w.Eligibility * elig + w.Reputation * reputation, 4), w,
            Math.Round(reputation, 4));
}

public static class AcademicYear
{
    /// <summary>The next September intake a student applying today would target, e.g. "2027/28" in November 2026.</summary>
    public static string UpcomingIntake(DateOnly today)
    {
        var year = today.Month >= 9 ? today.Year + 1 : today.Year;
        return $"{year}/{(year + 1) % 100:D2}";
    }

    /// <summary>First year of an academic-year label ("2025/26" → 2025), or null if it can't be read.</summary>
    public static int? StartYear(string? label) =>
        label is { Length: >= 4 } && int.TryParse(label.AsSpan(0, 4), out var y) ? y : null;
}
