using Compass.Domain;
using static Compass.Domain.Tests.TestData;

namespace Compass.Domain.Tests;

public class RecommendationEngineTests
{
    private readonly RecommendationEngine _sut = new(new EligibilityEvaluator(), new CostCalculator());

    private static RecommendationContext Ctx(Dictionary<int, double>? fit = null) =>
        new(new Dictionary<string, decimal> { ["Dublin"] = 1500m, ["Galway"] = 1200m }, 1000m,
            fit ?? new Dictionary<int, double>(), Today);

    [Fact]
    public void Ineligible_courses_rank_after_eligible_ones_even_with_higher_fit()
    {
        var eligible = Course(id: 1);
        var ineligible = Course(id: 2, minGrade: 90m);
        var recs = _sut.Recommend(Student(), [ineligible, eligible], Ctx(new() { [1] = 0.3, [2] = 0.99 }));
        Assert.Equal(1, recs[0].Course.Id);
        Assert.Contains(Tag.NotEligible, recs[1].Tags);
    }

    [Fact]
    public void Higher_fit_ranks_first_when_eligibility_and_cost_are_equal()
    {
        var recs = _sut.Recommend(Student(), [Course(id: 1), Course(id: 2)], Ctx(new() { [1] = 0.4, [2] = 0.9 }));
        Assert.Equal(2, recs[0].Course.Id);
        Assert.Contains(Tag.GoodFit, recs[0].Tags);
    }

    [Fact]
    public void Cheaper_city_can_turn_over_budget_into_affordable()
    {
        var student = Student(budget: 36000m);
        var recs = _sut.Recommend(student, [Course(id: 1, city: "Dublin"), Course(id: 2, city: "Galway")], Ctx());
        Assert.Contains(Tag.Affordable, recs.Single(r => r.Course.Id == 2).Tags);   // 20000 + 14400 + 1000
        Assert.DoesNotContain(Tag.Affordable, recs.Single(r => r.Course.Id == 1).Tags); // 20000 + 18000 + 1000
    }

    [Fact]
    public void Raising_budget_changes_the_shortlist()
    {
        var c = Course(id: 1);
        var before = _sut.Recommend(Student(budget: 30000m), [c], Ctx()).Single();
        var after = _sut.Recommend(Student(budget: 40000m), [c], Ctx()).Single();
        Assert.Contains(Tag.OverBudget, before.Tags);
        Assert.Contains(Tag.Affordable, after.Tags);
        Assert.True(after.Score > before.Score);
    }

    [Fact]
    public void Old_verification_date_is_flagged_as_stale()
    {
        var recs = _sut.Recommend(Student(), [Course(verified: Today.AddDays(-200))], Ctx());
        Assert.Contains(Tag.StaleData, recs[0].Tags);
    }

    [Fact]
    public void Borderline_grade_is_tagged_reach()
    {
        var recs = _sut.Recommend(Student(grade: 58m), [Course(minGrade: 60m)], Ctx());
        Assert.Contains(Tag.Reach, recs[0].Tags);
    }

    [Fact]
    public void Every_recommendation_explains_itself()
    {
        var rec = _sut.Recommend(Student(), [Course()], Ctx()).Single();
        Assert.NotEmpty(rec.Reasons);
        Assert.Contains(rec.Reasons, r => r.Contains("budget"));
    }

    [Fact]
    public void Missing_city_cost_data_fails_loudly()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _sut.Recommend(Student(), [Course(city: "Sligo")], Ctx()));
    }
}
