using Compass.Domain;
using static Compass.Domain.Tests.TestData;

namespace Compass.Domain.Tests;

public class RankingTests
{
    private readonly RecommendationEngine _sut = new(new EligibilityEvaluator(), new CostCalculator());

    private static RecommendationContext Ctx(Dictionary<int, double>? fit = null, Priority priority = Priority.Balanced) =>
        new(new Dictionary<string, decimal> { ["Dublin"] = 1500m, ["Galway"] = 1200m }, 1000m,
            fit ?? new Dictionary<int, double>(), Today, priority);

    [Fact]
    public void Priority_changes_the_order_between_a_cheap_course_and_a_better_fit()
    {
        // Course 1: better fit but expensive. Course 2: weaker fit, much cheaper.
        var courses = new[] { Course(id: 1, fee: 24000m), Course(id: 2, fee: 12000m, city: "Galway") };
        var fit = new Dictionary<int, double> { [1] = 0.95, [2] = 0.45 };
        var student = Student(budget: 45000m);

        Assert.Equal(1, _sut.Recommend(student, courses, Ctx(fit, Priority.BestFit))[0].Course.Id);
        Assert.Equal(2, _sut.Recommend(student, courses, Ctx(fit, Priority.LowestCost))[0].Course.Id);
    }

    [Fact]
    public void Breakdown_adds_up_to_the_score()
    {
        var r = _sut.Recommend(Student(), [Course()], Ctx(new() { [1] = 0.8 })).Single();
        var b = r.Breakdown;
        var expected = b.Weights.Fit * b.Fit + b.Weights.Affordability * b.Affordability + b.Weights.Eligibility * b.Eligibility;
        Assert.Equal(expected, r.Score, 3);
    }

    [Fact]
    public void Missing_fit_score_is_neutral_and_never_tagged_good_fit()
    {
        var r = _sut.Recommend(Student(), [Course()], Ctx()).Single();
        Assert.Equal(RecommendationEngine.NeutralFit, r.FitScore);
        Assert.DoesNotContain(Tag.GoodFit, r.Tags);
    }

    [Fact]
    public void Course_with_unpublished_requirements_ranks_below_a_fully_verified_equivalent()
    {
        var verified = Course(id: 1);
        var unpublished = Course(id: 2, minGrade: null);
        var recs = _sut.Recommend(Student(), [unpublished, verified], Ctx());
        Assert.Equal(1, recs[0].Course.Id);
        Assert.Contains(Tag.LikelyEligible, recs[1].Tags);
    }

    [Fact]
    public void Ties_are_broken_by_lower_cost_then_id()
    {
        var recs = _sut.Recommend(Student(budget: 100000m), [Course(id: 2), Course(id: 1)], Ctx());
        Assert.Equal(new[] { 1, 2 }, recs.Select(r => r.Course.Id));
    }

    [Theory]
    [InlineData("2025/26", true)]
    [InlineData(null, true)]
    [InlineData("2027/28", false)]
    public void Fee_from_an_older_or_unstated_year_is_flagged(string? feeYear, bool flagged)
    {
        var course = Course() with { FeeAcademicYear = feeYear };   // Today is Nov 2026 → intake 2027/28
        var r = _sut.Recommend(Student(), [course], Ctx()).Single();
        Assert.Equal(flagged, r.Tags.Contains(Tag.FeeYearUnconfirmed));
    }

    [Theory]
    [InlineData(2026, 11, 19, "2027/28")]
    [InlineData(2027, 2, 1, "2027/28")]
    [InlineData(2027, 9, 1, "2028/29")]
    public void Upcoming_intake_is_the_next_september(int y, int m, int d, string expected) =>
        Assert.Equal(expected, AcademicYear.UpcomingIntake(new DateOnly(y, m, d)));

    [Fact]
    public void More_headroom_scores_higher_within_affordable()
    {
        var tight = Scoring.AffordabilityScore(Affordability.Affordable, 39000m, 40000m);
        var roomy = Scoring.AffordabilityScore(Affordability.Affordable, 25000m, 40000m);
        Assert.True(roomy > tight);
        Assert.InRange(tight, 0.7, 1.0);
        Assert.Equal(1.0, roomy, 6);
    }
}
