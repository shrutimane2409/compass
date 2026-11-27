using Compass.Domain;
using static Compass.Domain.Tests.TestData;

namespace Compass.Domain.Tests;

public class EligibilityEvaluatorTests
{
    private readonly EligibilityEvaluator _sut = new();

    [Fact]
    public void Student_meeting_every_requirement_is_eligible()
    {
        var r = _sut.Evaluate(Student(), Course());
        Assert.Equal(EligibilityStatus.Eligible, r.Status);
        Assert.False(r.HasUnknowns);
    }

    [Fact]
    public void Grade_just_below_minimum_is_borderline()
    {
        var r = _sut.Evaluate(Student(grade: 58m), Course(minGrade: 60m));
        Assert.Equal(EligibilityStatus.Borderline, r.Status);
    }

    [Fact]
    public void Grade_well_below_minimum_is_not_eligible()
    {
        var r = _sut.Evaluate(Student(grade: 50m), Course(minGrade: 60m));
        Assert.Equal(EligibilityStatus.NotEligible, r.Status);
    }

    [Fact]
    public void Unaccepted_background_is_not_eligible()
    {
        var r = _sut.Evaluate(Student(background: "commerce"), Course());
        Assert.Equal(EligibilityStatus.NotEligible, r.Status);
        Assert.Contains(r.Checks, c => c.Name == "Background" && c.Passed == false);
    }

    [Fact]
    public void Unnamed_background_is_borderline_when_page_accepts_related_disciplines()
    {
        var r = _sut.Evaluate(Student(background: "electronics"), Course(backgroundsOpen: true));
        Assert.Equal(EligibilityStatus.Borderline, r.Status);
        Assert.Contains(r.Checks, c => c.Name == "Background" && c.Passed == null);
    }

    [Fact]
    public void Named_background_still_passes_when_page_accepts_related_disciplines()
    {
        var r = _sut.Evaluate(Student(), Course(backgroundsOpen: true));
        Assert.Contains(r.Checks, c => c.Name == "Background" && c.Passed == true);
    }

    [Fact]
    public void Background_match_ignores_case()
    {
        var r = _sut.Evaluate(Student(background: "Computer-Science"), Course());
        Assert.Contains(r.Checks, c => c.Name == "Background" && c.Passed == true);
    }

    [Fact]
    public void Low_english_score_is_not_eligible()
    {
        var r = _sut.Evaluate(Student(english: new EnglishScore(EnglishTest.Ielts, 6.0m)), Course(ielts: 6.5m));
        Assert.Equal(EligibilityStatus.NotEligible, r.Status);
    }

    [Fact]
    public void Missing_english_test_is_unknown_not_a_failure()
    {
        var s = Student() with { English = null };
        var r = _sut.Evaluate(s, Course());
        Assert.Equal(EligibilityStatus.LikelyEligible, r.Status);
        Assert.Contains(r.Checks, c => c.Name == "English" && c.State == CheckState.StudentPending);
        Assert.True(r.HasUnknowns);
    }

    [Fact]
    public void Test_type_without_stated_minimum_is_unknown()
    {
        var r = _sut.Evaluate(Student(english: new EnglishScore(EnglishTest.Toefl, 100)), Course(toefl: null));
        Assert.Contains(r.Checks, c => c.Name == "English" && c.Passed is null);
    }

    [Fact]
    public void Toefl_below_old_scale_minimum_is_not_eligible()
    {
        var r = _sut.Evaluate(Student(english: new EnglishScore(EnglishTest.Toefl, 85)), Course(toefl: 90));
        Assert.Contains(r.Checks, c => c.Name == "English" && c.Passed == false);
    }

    [Theory]
    [InlineData(4.5, true)]
    [InlineData(5.0, true)]
    [InlineData(4.0, false)]
    public void Toefl_on_new_1_to_6_scale_uses_the_new_scale_minimum(double score, bool passes)
    {
        var course = Course(toefl: 90) with { MinToeflNew = 4.5m };
        var r = _sut.Evaluate(Student(english: new EnglishScore(EnglishTest.Toefl, (decimal)score)), course);
        Assert.Contains(r.Checks, c => c.Name == "English" && c.Passed == passes);
    }

    [Fact]
    public void Toefl_on_new_scale_without_a_new_scale_minimum_is_unknown_not_a_failure()
    {
        var r = _sut.Evaluate(Student(english: new EnglishScore(EnglishTest.Toefl, 4.5m)), Course(toefl: 90));
        Assert.Contains(r.Checks, c => c.Name == "English" && c.Passed is null);
        Assert.NotEqual(EligibilityStatus.NotEligible, r.Status);
    }

    [Fact]
    public void Old_scale_toefl_is_not_compared_with_the_new_scale_minimum()
    {
        var course = Course(toefl: null) with { MinToeflNew = 4.5m };
        var r = _sut.Evaluate(Student(english: new EnglishScore(EnglishTest.Toefl, 100)), course);
        Assert.Contains(r.Checks, c => c.Name == "English" && c.Passed is null);
    }

    [Fact]
    public void Unstated_grade_requirement_is_unknown()
    {
        var r = _sut.Evaluate(Student(), Course(minGrade: null));
        Assert.Contains(r.Checks, c => c.Name == "Grade" && c.State == CheckState.NotPublished);
        // Missing information must never produce a green "Eligible".
        Assert.Equal(EligibilityStatus.LikelyEligible, r.Status);
    }

    [Fact]
    public void Insufficient_work_experience_is_not_eligible()
    {
        var r = _sut.Evaluate(Student(work: 1m), Course(minWork: 2m));
        Assert.Equal(EligibilityStatus.NotEligible, r.Status);
    }

    [Fact]
    public void Borderline_grade_is_not_shown_as_met()
    {
        var r = _sut.Evaluate(Student(grade: 58m), Course(minGrade: 60m));
        Assert.Contains(r.Checks, c => c.Name == "Grade" && c.State == CheckState.Borderline && c.Passed is null);
    }

    [Fact]
    public void Borderline_takes_precedence_over_unpublished()
    {
        var r = _sut.Evaluate(Student(grade: 58m), Course(minGrade: 60m, ielts: null, pte: null));
        Assert.Equal(EligibilityStatus.Borderline, r.Status);
    }

    [Fact]
    public void Each_check_links_to_the_page_its_requirement_comes_from()
    {
        var course = Course() with
        {
            GradeSourceUrl = "https://example.org/india",
            EnglishSourceUrl = "https://example.org/english",
            EntrySourceUrl = "https://example.org/entry"
        };
        var r = _sut.Evaluate(Student(), course);
        Assert.Equal("https://example.org/india", r.Checks.Single(c => c.Name == "Grade").SourceUrl);
        Assert.Equal("https://example.org/english", r.Checks.Single(c => c.Name == "English").SourceUrl);
        Assert.Equal("https://example.org/entry", r.Checks.Single(c => c.Name == "Background").SourceUrl);
    }

    [Fact]
    public void Checks_fall_back_to_the_course_page()
    {
        var r = _sut.Evaluate(Student(), Course());
        Assert.All(r.Checks, c => Assert.Equal("https://example.org/course", c.SourceUrl));
    }
}
