namespace Compass.Domain;

public enum EligibilityStatus { Eligible, Borderline, NotEligible }

/// <summary>Passed is null when the course page does not state the requirement or the student has not provided the value yet.</summary>
public sealed record Check(string Name, bool? Passed, string Detail);

public sealed record EligibilityResult(EligibilityStatus Status, IReadOnlyList<Check> Checks)
{
    public bool HasUnknowns => Checks.Any(c => c.Passed is null);
}

/// <summary>Deterministic eligibility rules. No AI is involved in this decision.</summary>
public sealed class EligibilityEvaluator
{
    public const decimal BorderlineMarginPercent = 3m;

    public EligibilityResult Evaluate(StudentProfile s, Course c)
    {
        var checks = new List<Check>();
        var borderline = false;

        // Grade
        if (c.MinGradePercent is null)
            checks.Add(new("Grade", null, "Minimum grade not stated on the course page."));
        else if (s.GradePercent >= c.MinGradePercent)
            checks.Add(new("Grade", true, $"{s.GradePercent}% meets the {c.MinGradePercent}% minimum."));
        else if (s.GradePercent >= c.MinGradePercent - BorderlineMarginPercent)
        {
            borderline = true;
            checks.Add(new("Grade", true, $"{s.GradePercent}% is just below the {c.MinGradePercent}% minimum. Possible, not certain."));
        }
        else
            checks.Add(new("Grade", false, $"{s.GradePercent}% is below the {c.MinGradePercent}% minimum."));

        // Background
        if (c.AcceptedBackgrounds.Count == 0)
            checks.Add(new("Background", null, "Accepted degree backgrounds not stated."));
        else if (c.AcceptedBackgrounds.Contains(s.Background, StringComparer.OrdinalIgnoreCase))
            checks.Add(new("Background", true, $"A {s.Background} degree is accepted."));
        else
            checks.Add(new("Background", false, $"A {s.Background} degree is not in the accepted list."));

        // English
        checks.Add(EvaluateEnglish(s.English, c));

        // Work experience
        if (c.MinWorkYears <= 0)
            checks.Add(new("Work experience", true, "No work experience required."));
        else if (s.WorkYears >= c.MinWorkYears)
            checks.Add(new("Work experience", true, $"{s.WorkYears} years meets the {c.MinWorkYears}-year requirement."));
        else
            checks.Add(new("Work experience", false, $"Needs {c.MinWorkYears} years; you have {s.WorkYears}."));

        var status = checks.Any(x => x.Passed == false) ? EligibilityStatus.NotEligible
                   : borderline ? EligibilityStatus.Borderline
                   : EligibilityStatus.Eligible;

        return new EligibilityResult(status, checks);
    }

    private const decimal NewToeflScaleMax = 6m;

    private static Check EvaluateEnglish(EnglishScore? score, Course c)
    {
        var required = RequirementText(c);
        if (required is null)
            return new("English", null, "English requirement not stated.");
        if (score is null)
            return new("English", null, $"No test taken yet. Required: {required}.");

        // TOEFL iBT moved to a 1-6 scale on 21 January 2026. A score of 6 or less is on the new scale.
        var newToefl = score.Test == EnglishTest.Toefl && score.Score <= NewToeflScaleMax;
        var label = newToefl ? "TOEFL (1-6 scale)" : score.Test.ToString();
        decimal? min = score.Test switch
        {
            EnglishTest.Ielts => c.MinIelts,
            EnglishTest.Pte => c.MinPte,
            EnglishTest.Toefl => newToefl ? c.MinToeflNew : c.MinToefl,
            _ => null
        };
        if (min is null)
            return new("English", null, $"{label} minimum not stated. Required: {required}.");

        return score.Score >= min
            ? new("English", true, $"{label} {score.Score} meets the {min} minimum.")
            : new("English", false, $"{label} {score.Score} is below the {min} minimum.");
    }

    private static string? RequirementText(Course c)
    {
        var parts = new List<string>();
        if (c.MinIelts is not null) parts.Add($"IELTS {c.MinIelts}");
        if (c.MinPte is not null) parts.Add($"PTE {c.MinPte}");
        if (c.MinToefl is not null) parts.Add($"TOEFL iBT {c.MinToefl} (0-120 scale)");
        if (c.MinToeflNew is not null) parts.Add($"TOEFL iBT {c.MinToeflNew} (1-6 scale)");
        return parts.Count == 0 ? null : string.Join(" / ", parts);
    }
}
