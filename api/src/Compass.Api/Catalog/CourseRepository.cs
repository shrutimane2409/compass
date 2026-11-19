using Compass.Domain;
using Dapper;
using Npgsql;

namespace Compass.Api.Catalog;

public sealed class CourseRepository(NpgsqlDataSource db)
{
    static CourseRepository() => DefaultTypeMap.MatchNamesWithUnderscores = true;

    public async Task<IReadOnlyList<Course>> GetCoursesAsync(CancellationToken ct)
    {
        const string sql = """
            SELECT c.id, u.name AS university, u.city, c.title, c.field, c.duration_months,
                   c.fee_non_eu_eur, c.min_grade_percent, c.accepted_backgrounds,
                   c.min_ielts, c.min_pte, c.min_toefl, c.min_toefl_new, c.min_work_years,
                   c.source_url, c.verified_at, c.is_estimate, c.fee_academic_year, c.english_source_url,
                   c.backgrounds_open, c.entry_source_url, c.grade_source_url
            FROM courses c
            JOIN universities u ON u.id = c.university_id
            ORDER BY u.name, c.title
            """;
        await using var conn = await db.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<CourseRow>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task<IReadOnlyDictionary<string, decimal>> GetMonthlyLivingByCityAsync(CancellationToken ct)
    {
        const string sql = "SELECT city, SUM(monthly_eur) AS total FROM living_costs GROUP BY city";
        await using var conn = await db.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<(string City, decimal Total)>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.ToDictionary(r => r.City, r => r.Total, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<decimal> GetOneOffTotalAsync(string country, CancellationToken ct)
    {
        const string sql = "SELECT COALESCE(SUM(amount_eur), 0) FROM one_off_costs WHERE country = @country";
        await using var conn = await db.OpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<decimal>(new CommandDefinition(sql, new { country }, cancellationToken: ct));
    }

    private sealed class CourseRow
    {
        public int Id { get; set; }
        public string University { get; set; } = "";
        public string City { get; set; } = "";
        public string Title { get; set; } = "";
        public string Field { get; set; } = "";
        public int DurationMonths { get; set; }
        public decimal FeeNonEuEur { get; set; }
        public decimal? MinGradePercent { get; set; }
        public string[] AcceptedBackgrounds { get; set; } = [];
        public decimal? MinIelts { get; set; }
        public int? MinPte { get; set; }
        public int? MinToefl { get; set; }
        public decimal? MinToeflNew { get; set; }
        public decimal MinWorkYears { get; set; }
        public string SourceUrl { get; set; } = "";
        public DateTime VerifiedAt { get; set; }
        public bool IsEstimate { get; set; }
        public string? FeeAcademicYear { get; set; }
        public string? EnglishSourceUrl { get; set; }
        public bool BackgroundsOpen { get; set; }
        public string? EntrySourceUrl { get; set; }
        public string? GradeSourceUrl { get; set; }

        public Course ToDomain() => new(
            Id, University, City, Title, Field, DurationMonths, FeeNonEuEur, MinGradePercent,
            AcceptedBackgrounds, MinIelts, MinPte, MinToefl, MinWorkYears,
            SourceUrl, DateOnly.FromDateTime(VerifiedAt), IsEstimate, FeeAcademicYear, EnglishSourceUrl, MinToeflNew,
            BackgroundsOpen, EntrySourceUrl, GradeSourceUrl);
    }
}
