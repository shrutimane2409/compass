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
                   c.backgrounds_open, c.entry_source_url, c.grade_source_url, c.application_deadline
            FROM courses c
            JOIN universities u ON u.id = c.university_id
            ORDER BY u.name, c.title
            """;
        await using var conn = await db.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<CourseRow>(new CommandDefinition(sql, cancellationToken: ct));
        var ranks = await GetRankingsAsync(conn, ct);
        return rows.Select(r => r.ToDomain() with
        {
            Rankings = ranks.TryGetValue(r.University, out var list) ? list : []
        }).ToList();
    }

    /// <summary>Rankings by university name. Optional data: an older database without the table still serves courses.</summary>
    private static async Task<Dictionary<string, List<UniversityRank>>> GetRankingsAsync(NpgsqlConnection conn, CancellationToken ct)
    {
        const string sql = """
            SELECT u.name AS university, r.source, r.edition, r.rank_text, r.rank_low, r.source_url
            FROM university_rankings r JOIN universities u ON u.id = r.university_id
            WHERE r.scope = 'overall'
            ORDER BY r.source, r.edition DESC
            """;
        try
        {
            var rows = await conn.QueryAsync<RankRow>(new CommandDefinition(sql, cancellationToken: ct));
            return rows.GroupBy(r => r.University, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Select(r => new UniversityRank(r.Source, r.Edition, r.RankText, r.RankLow, r.SourceUrl)).ToList(),
                    StringComparer.OrdinalIgnoreCase);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<Scholarship>> GetScholarshipsAsync(CancellationToken ct)
    {
        const string sql = """
            SELECT s.id, s.slug, s.name, s.provider, u.name AS university, s.amount_text, s.max_value_eur, s.fields,
                   s.min_grade_percent, s.requires_offer, s.automatic, s.competitive, s.nationality, s.how_to_apply,
                   s.deadline_text, s.source_url, s.verified_at
            FROM scholarships s LEFT JOIN universities u ON u.id = s.university_id
            ORDER BY s.name
            """;
        await using var conn = await db.OpenConnectionAsync(ct);
        try
        {
            var rows = await conn.QueryAsync<ScholarshipRow>(new CommandDefinition(sql, cancellationToken: ct));
            return rows.Select(r => new Scholarship(r.Id, r.Slug, r.Name, r.Provider, r.University, r.AmountText, r.MaxValueEur,
                r.Fields, r.MinGradePercent, r.RequiresOffer, r.Automatic, r.Competitive, r.Nationality, r.HowToApply,
                r.DeadlineText, r.SourceUrl, DateOnly.FromDateTime(r.VerifiedAt))).ToList();
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            return [];
        }
    }

    private sealed class RankRow
    {
        public string University { get; set; } = "";
        public string Source { get; set; } = "";
        public int Edition { get; set; }
        public string RankText { get; set; } = "";
        public int RankLow { get; set; }
        public string SourceUrl { get; set; } = "";
    }

    private sealed class ScholarshipRow
    {
        public int Id { get; set; }
        public string Slug { get; set; } = "";
        public string Name { get; set; } = "";
        public string Provider { get; set; } = "";
        public string? University { get; set; }
        public string AmountText { get; set; } = "";
        public decimal? MaxValueEur { get; set; }
        public string[] Fields { get; set; } = [];
        public decimal? MinGradePercent { get; set; }
        public bool RequiresOffer { get; set; }
        public bool Automatic { get; set; }
        public bool Competitive { get; set; }
        public string Nationality { get; set; } = "";
        public string HowToApply { get; set; } = "";
        public string DeadlineText { get; set; } = "";
        public string SourceUrl { get; set; } = "";
        public DateTime VerifiedAt { get; set; }
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
        public DateTime? ApplicationDeadline { get; set; }

        public Course ToDomain() => new(
            Id, University, City, Title, Field, DurationMonths, FeeNonEuEur, MinGradePercent,
            AcceptedBackgrounds, MinIelts, MinPte, MinToefl, MinWorkYears,
            SourceUrl, DateOnly.FromDateTime(VerifiedAt), IsEstimate, FeeAcademicYear, EnglishSourceUrl, MinToeflNew,
            BackgroundsOpen, EntrySourceUrl, GradeSourceUrl,
            ApplicationDeadline is { } d ? DateOnly.FromDateTime(d) : null);
    }
}
