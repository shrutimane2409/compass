using Compass.Domain;
using static Compass.Domain.Tests.TestData;

namespace Compass.Domain.Tests;

public class ReputationTests
{
    private readonly RecommendationEngine _sut = new(new EligibilityEvaluator(), new CostCalculator());

    private static RecommendationContext Ctx(Priority p) =>
        new(new Dictionary<string, decimal> { ["Dublin"] = 1500m }, 1000m, new Dictionary<int, double>(), Today, p);

    private static UniversityRank Qs(int low, string text = "") => new("QS", 2027, text == "" ? low.ToString() : text, low, "https://example.org/qs");

    [Theory]
    [InlineData(75, 1.0)]
    [InlineData(220, 0.7)]
    [InlineData(721, 0.3)]
    [InlineData(1201, 0.15)]
    public void Reputation_score_uses_coarse_bands(int rankLow, double expected) =>
        Assert.Equal(expected, Scoring.ReputationScore([Qs(rankLow)]));

    [Fact]
    public void Unranked_university_gets_the_floor_score() => Assert.Equal(0.1, Scoring.ReputationScore([]));

    [Fact]
    public void Qs_is_the_headline_rank_and_the_latest_edition_wins()
    {
        UniversityRank[] ranks = [new("THE", 2027, "171", 171, "u"), new("QS", 2026, "118", 118, "u"), new("QS", 2027, "100", 100, "u")];
        Assert.Equal("100", Scoring.HeadlineRank(ranks)!.RankText);
    }

    [Fact]
    public void Rankings_only_change_the_order_when_the_student_asks_for_reputation()
    {
        // Same fee and fit; course 2 is at a far higher-ranked university but slightly dearer.
        var low = Course(id: 1, fee: 20000m) with { Rankings = [Qs(800)] };
        var high = Course(id: 2, fee: 21000m) with { Rankings = [Qs(75)] };
        var student = Student(budget: 60000m);

        Assert.Equal(1, _sut.Recommend(student, [low, high], Ctx(Priority.Balanced))[0].Course.Id);
        Assert.Equal(2, _sut.Recommend(student, [low, high], Ctx(Priority.Reputation))[0].Course.Id);
    }

    [Fact]
    public void Reputation_priority_explains_the_rank()
    {
        var r = _sut.Recommend(Student(), [Course() with { Rankings = [Qs(721, "721-730")] }], Ctx(Priority.Reputation)).Single();
        Assert.Contains(r.Reasons, x => x.Contains("721-730") && x.Contains("QS"));
        Assert.Equal(0.3, r.Breakdown.Reputation);
    }
}

public class ScholarshipMatcherTests
{
    private readonly ScholarshipMatcher _sut = new();

    private static Scholarship S(string slug, string? uni = null, bool automatic = false, bool competitive = false,
        string[]? fields = null, decimal? minGrade = null) =>
        new(1, slug, slug, "Provider", uni, "€2000", 2000m, fields ?? [], minGrade, true, automatic, competitive,
            "non-eu", "Apply", "March", "https://example.org/s", Today);

    [Fact]
    public void University_scholarship_only_applies_to_that_universitys_courses()
    {
        var m = _sut.Match(Student(), [S("other", uni: "Other University")], [Course()]).Single();
        Assert.Equal(ScholarshipFit.NotApplicable, m.Fit);
    }

    [Fact]
    public void Automatic_non_competitive_award_is_likely_and_competitive_one_is_possible()
    {
        var result = _sut.Match(Student(), [S("auto", uni: "Test University", automatic: true), S("national", competitive: true)], [Course()]);
        Assert.Equal(ScholarshipFit.Likely, result.Single(x => x.Scholarship.Slug == "auto").Fit);
        Assert.Equal(ScholarshipFit.Possible, result.Single(x => x.Scholarship.Slug == "national").Fit);
        Assert.Equal("auto", result[0].Scholarship.Slug);
    }

    [Fact]
    public void Field_and_grade_rules_exclude_with_a_reason()
    {
        var byField = _sut.Match(Student(), [S("biz", fields: ["business-analytics"])], [Course()]).Single();
        Assert.Equal(ScholarshipFit.NotApplicable, byField.Fit);

        var byGrade = _sut.Match(Student(grade: 60m), [S("merit", minGrade: 80m)], [Course()]).Single();
        Assert.Equal(ScholarshipFit.NotApplicable, byGrade.Fit);
        Assert.Contains(byGrade.Reasons, r => r.Contains("80"));
    }
}
