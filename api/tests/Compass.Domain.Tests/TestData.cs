using Compass.Domain;

namespace Compass.Domain.Tests;

/// <summary>Fictional fixtures for rule tests. Not real course data.</summary>
internal static class TestData
{
    public static readonly DateOnly Today = new(2026, 11, 19);

    public static Course Course(
        int id = 1, string city = "Dublin", decimal fee = 20000m, int months = 12,
        decimal? minGrade = 60m, string[]? backgrounds = null,
        decimal? ielts = 6.5m, int? pte = 63, int? toefl = null,
        decimal minWork = 0m, DateOnly? verified = null, bool estimate = false, bool backgroundsOpen = false) =>
        new(id, "Test University", city, $"MSc Test {id}", "computer-science", months, fee, minGrade,
            backgrounds ?? ["computer-science", "information-technology"], ielts, pte, toefl, minWork,
            "https://example.org/course", verified ?? Today.AddDays(-30), estimate, BackgroundsOpen: backgroundsOpen);

    public static StudentProfile Student(
        decimal grade = 70m, string background = "computer-science",
        EnglishScore? english = null, decimal work = 0m, decimal budget = 40000m) =>
        new(grade, background, english ?? new EnglishScore(EnglishTest.Ielts, 7.0m), work, budget, ["machine learning"]);
}
