namespace Compass.Domain;

/// <summary>
/// Eligible only when every requirement is published and met. Missing information never produces a green result.
/// </summary>
public enum EligibilityStatus
{
    /// <summary>Every requirement is published and the student meets all of them.</summary>
    Eligible,
    /// <summary>Nothing fails, but at least one requirement is unpublished or the student hasn't provided it yet.</summary>
    LikelyEligible,
    /// <summary>Close to a published minimum, or in a discretionary category ("related disciplines"). Possible, not certain.</summary>
    Borderline,
    /// <summary>At least one published requirement is not met.</summary>
    NotEligible
}

public enum CheckState
{
    Met,
    NotMet,
    /// <summary>Just below a minimum, or a discretionary case the university decides.</summary>
    Borderline,
    /// <summary>The university does not publish this requirement for the course.</summary>
    NotPublished,
    /// <summary>The requirement is published; the student hasn't supplied the value yet (e.g. no English test).</summary>
    StudentPending
}

/// <summary>One requirement, its outcome, and the official page the requirement comes from.</summary>
public sealed record Check(string Name, CheckState State, string Detail, string? SourceUrl)
{
    /// <summary>True = met, false = not met, null = undecided (borderline, unpublished or pending).</summary>
    public bool? Passed => State switch
    {
        CheckState.Met => true,
        CheckState.NotMet => false,
        _ => null
    };
}

public sealed record EligibilityResult(EligibilityStatus Status, IReadOnlyList<Check> Checks)
{
    public bool HasUnknowns => Checks.Any(c => c.State is CheckState.NotPublished or CheckState.StudentPending);
}

/// <summary>Deterministic eligibility rules. No AI is involved in this decision.</summary>
public sealed class EligibilityEvaluator
{
    public const decimal BorderlineMarginPercent = 3m;
    private const decimal NewToeflScaleMax = 6m;

    public EligibilityResult Evaluate(StudentProfile s, Course c)
    {
        var entrySource = c.EntrySourceUrl ?? c.SourceUrl;
        var checks = new List<Check>
        {
            EvaluateGrade(s, c, c.GradeSourceUrl ?? entrySource),
            EvaluateBackground(s, c, entrySource),
            EvaluateEnglish(s.English, c, c.EnglishSourceUrl ?? c.SourceUrl),
            EvaluateWork(s, c, entrySource),
        };
        return new EligibilityResult(StatusOf(checks), checks);
    }

    /// <summary>Precedence: any failure → NotEligible; any borderline → Borderline; any unknown → LikelyEligible; else Eligible.</summary>
    public static EligibilityStatus StatusOf(IReadOnlyCollection<Check> checks) =>
        checks.Any(x => x.State == CheckState.NotMet) ? EligibilityStatus.NotEligible
        : checks.Any(x => x.State == CheckState.Borderline) ? EligibilityStatus.Borderline
        : checks.Any(x => x.State is CheckState.NotPublished or CheckState.StudentPending) ? EligibilityStatus.LikelyEligible
        : EligibilityStatus.Eligible;

    private static Check EvaluateGrade(StudentProfile s, Course c, string source)
    {
        if (c.MinGradePercent is not { } min)
            return new("Grade", CheckState.NotPublished,
                "The university doesn't publish a minimum grade for Indian degrees for this course. Check the course page.", source);
        if (s.GradePercent >= min)
            return new("Grade", CheckState.Met, $"{s.GradePercent}% meets the {min}% minimum.", source);
        if (s.GradePercent >= min - BorderlineMarginPercent)
            return new("Grade", CheckState.Borderline,
                $"{s.GradePercent}% is just below the {min}% minimum. Some universities consider this; it is not guaranteed.", source);
        return new("Grade", CheckState.NotMet, $"{s.GradePercent}% is below the {min}% minimum.", source);
    }

    private static Check EvaluateBackground(StudentProfile s, Course c, string source)
    {
        if (c.AcceptedBackgrounds.Count == 0)
            return new("Background", CheckState.NotPublished, "Accepted degree backgrounds are not published.", source);
        if (c.AcceptedBackgrounds.Contains(s.Background, StringComparer.OrdinalIgnoreCase))
            return new("Background", CheckState.Met, $"A {s.Background} degree is accepted.", source);
        if (c.BackgroundsOpen)
            return new("Background", CheckState.Borderline,
                $"A {s.Background} degree isn't named, but the course also accepts related disciplines. The university decides case by case.", source);
        return new("Background", CheckState.NotMet, $"A {s.Background} degree is not in the accepted list.", source);
    }

    private static Check EvaluateWork(StudentProfile s, Course c, string source)
    {
        if (c.MinWorkYears <= 0)
            return new("Work experience", CheckState.Met, "No work experience required.", source);
        return s.WorkYears >= c.MinWorkYears
            ? new("Work experience", CheckState.Met, $"{s.WorkYears} years meets the {c.MinWorkYears}-year requirement.", source)
            : new("Work experience", CheckState.NotMet, $"Needs {c.MinWorkYears} years; you have {s.WorkYears}.", source);
    }

    private static Check EvaluateEnglish(EnglishScore? score, Course c, string source)
    {
        var required = RequirementText(c);
        if (required is null)
            return new("English", CheckState.NotPublished, "English requirement not published.", source);
        if (score is null)
            return new("English", CheckState.StudentPending, $"No test taken yet. Required: {required}.", source);

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
            return new("English", CheckState.NotPublished, $"{label} minimum not published. Required: {required}.", source);

        return score.Score >= min
            ? new("English", CheckState.Met, $"{label} {score.Score} meets the {min} minimum.", source)
            : new("English", CheckState.NotMet, $"{label} {score.Score} is below the {min} minimum.", source);
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
