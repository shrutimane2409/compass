namespace Compass.Domain;

/// <summary>
/// Converts a 10-point CGPA to a percentage. Conversion rules differ by Indian university,
/// so the factor is explicit and the result is always shown to the student as an estimate.
/// </summary>
public static class GradeConverter
{
    public const decimal DefaultCgpaFactor = 9.5m;

    public static decimal CgpaToPercent(decimal cgpa, decimal factor = DefaultCgpaFactor)
    {
        if (cgpa is < 0 or > 10) throw new ArgumentOutOfRangeException(nameof(cgpa), "CGPA must be between 0 and 10.");
        if (factor <= 0) throw new ArgumentOutOfRangeException(nameof(factor));
        return Math.Min(100m, Math.Round(cgpa * factor, 2));
    }
}
