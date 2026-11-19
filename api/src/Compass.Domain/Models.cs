namespace Compass.Domain;

public enum EnglishTest { Ielts, Pte, Toefl }

public sealed record EnglishScore(EnglishTest Test, decimal Score);

/// <summary>What the student tells us. Budget is the total they can spend on the whole course, living costs included.</summary>
public sealed record StudentProfile(
    decimal GradePercent,
    string Background,
    EnglishScore? English,
    decimal WorkYears,
    decimal BudgetEur,
    IReadOnlyList<string> Interests);

/// <summary>A verified course record. FeeNonEuEur is the total programme fee for non-EU students.</summary>
public sealed record Course(
    int Id,
    string University,
    string City,
    string Title,
    string Field,
    int DurationMonths,
    decimal FeeNonEuEur,
    decimal? MinGradePercent,
    IReadOnlyCollection<string> AcceptedBackgrounds,
    decimal? MinIelts,
    int? MinPte,
    int? MinToefl,
    decimal MinWorkYears,
    string SourceUrl,
    DateOnly VerifiedAt,
    bool IsEstimate,
    string? FeeAcademicYear = null,
    string? EnglishSourceUrl = null,
    decimal? MinToeflNew = null,
    bool BackgroundsOpen = false,
    string? EntrySourceUrl = null,
    string? GradeSourceUrl = null);
