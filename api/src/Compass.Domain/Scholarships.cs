namespace Compass.Domain;

/// <summary>A published scholarship. University is null for national schemes open at any Irish university.</summary>
public sealed record Scholarship(
    int Id,
    string Slug,
    string Name,
    string Provider,
    string? University,
    string AmountText,
    decimal? MaxValueEur,
    IReadOnlyCollection<string> Fields,
    decimal? MinGradePercent,
    bool RequiresOffer,
    bool Automatic,
    bool Competitive,
    string Nationality,
    string HowToApply,
    string DeadlineText,
    string SourceUrl,
    DateOnly VerifiedAt);

public enum ScholarshipFit { Likely, Possible, NotApplicable }

public sealed record ScholarshipMatch(
    Scholarship Scholarship,
    ScholarshipFit Fit,
    IReadOnlyList<int> CourseIds,
    IReadOnlyList<string> Reasons);

/// <summary>
/// Matches scholarships to the courses a student is considering. Rules only: it never promises an award,
/// it says whether the student is in scope and what they have to do. Compass serves Indian students, so
/// both "non-eu" and "india" schemes apply.
/// </summary>
public sealed class ScholarshipMatcher
{
    public IReadOnlyList<ScholarshipMatch> Match(StudentProfile student, IEnumerable<Scholarship> scholarships, IReadOnlyList<Course> courses) =>
        scholarships
            .Select(s => MatchOne(student, s, courses))
            .OrderBy(m => m.Fit)
            .ThenBy(m => m.Scholarship.Competitive)
            .ThenByDescending(m => m.Scholarship.MaxValueEur ?? decimal.MaxValue)
            .ThenBy(m => m.Scholarship.Name, StringComparer.Ordinal)
            .ToList();

    private static ScholarshipMatch MatchOne(StudentProfile student, Scholarship s, IReadOnlyList<Course> courses)
    {
        var reasons = new List<string>();
        var relevant = courses
            .Where(c => s.University is null || string.Equals(c.University, s.University, StringComparison.OrdinalIgnoreCase))
            .Where(c => s.Fields.Count == 0 || s.Fields.Contains(c.Field, StringComparer.OrdinalIgnoreCase))
            .Select(c => c.Id)
            .ToList();

        if (relevant.Count == 0)
        {
            reasons.Add(s.University is null
                ? "None of the courses you're considering are covered."
                : $"Only for {s.University} courses that aren't on your list.");
            return new(s, ScholarshipFit.NotApplicable, relevant, reasons);
        }

        if (s.MinGradePercent is { } min && student.GradePercent < min)
        {
            reasons.Add($"Needs at least {min:0.#}% in your degree; you entered {student.GradePercent:0.#}%.");
            return new(s, ScholarshipFit.NotApplicable, relevant, reasons);
        }

        reasons.Add(relevant.Count == 1 ? "Applies to 1 course you're considering." : $"Applies to {relevant.Count} courses you're considering.");
        if (s.Automatic) reasons.Add("Considered automatically when you apply: no separate form.");
        else if (s.RequiresOffer) reasons.Add("You can apply once you hold an offer.");
        if (s.Competitive) reasons.Add("Competitive: awarded to a limited number of applicants.");

        var fit = s.Automatic && !s.Competitive ? ScholarshipFit.Likely : ScholarshipFit.Possible;
        return new(s, fit, relevant, reasons);
    }
}
