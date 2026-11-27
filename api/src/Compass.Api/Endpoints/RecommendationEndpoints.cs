using Compass.Api.Ai;
using Compass.Api.Catalog;
using Compass.Api.Infrastructure;
using Compass.Domain;

namespace Compass.Api.Endpoints;

public sealed record RecommendationRequest(StudentProfile Profile, Priority? Priority = null)
{
    public Dictionary<string, string[]> Validate()
    {
        var e = new Dictionary<string, string[]>();
        if (Profile is null) { e["profile"] = ["Profile is required."]; return e; }
        if (Profile.GradePercent is < 0 or > 100) e["profile.gradePercent"] = ["Must be between 0 and 100."];
        if (Profile.BudgetEur <= 0) e["profile.budgetEur"] = ["Must be greater than 0."];
        if (Profile.WorkYears is < 0 or > 50) e["profile.workYears"] = ["Must be between 0 and 50."];
        if (string.IsNullOrWhiteSpace(Profile.Background)) e["profile.background"] = ["Required."];
        if (Profile.Interests is { Count: > 20 }) e["profile.interests"] = ["At most 20 interests."];
        return e;
    }
}

public sealed record RecommendationResponse(
    IReadOnlyList<Recommendation> Items,
    bool FitAvailable,
    DateTimeOffset CatalogLoadedAt,
    string TargetIntake,
    Priority Priority);

public static class RecommendationEndpoints
{
    public static IEndpointRouteBuilder MapRecommendationEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireRateLimiting(RateLimits.Api);

        api.MapGet("/courses", async (CourseCatalog catalog, CancellationToken ct) =>
            Results.Ok((await catalog.GetAsync(ct)).Courses));

        api.MapPost("/recommendations", RecommendAsync);
        return app;
    }

    private static async Task<IResult> RecommendAsync(
        RecommendationRequest req,
        CourseCatalog catalog,
        FitScoreService fit,
        RecommendationEngine engine,
        TimeProvider time,
        UsageTracker usage,
        HttpContext http,
        CancellationToken ct)
    {
        var started = time.GetTimestamp();
        var errors = req.Validate();
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var profile = req.Profile with { Interests = req.Profile.Interests ?? [] };
        var snapshot = await catalog.GetAsync(ct);
        var fitResult = await fit.GetAsync(profile.Interests, snapshot.Courses.Select(c => c.Id).ToList(), ct);

        var ctx = new RecommendationContext(
            snapshot.MonthlyLivingByCity,
            snapshot.OneOffEur,
            fitResult.Scores,
            DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime),
            req.Priority ?? Priority.Balanced);

        try
        {
            var items = engine.Recommend(profile, snapshot.Courses, ctx);
            usage.Track("search", http, new
            {
                ms = (int)time.GetElapsedTime(started).TotalMilliseconds,
                priority = ctx.Priority.ToString(),
                results = items.Count,
                eligible = items.Count(i => i.Eligibility.Status != EligibilityStatus.NotEligible),
                fitAvailable = fitResult.Available,
                interests = profile.Interests.Count,
            });
            return Results.Ok(new RecommendationResponse(items, fitResult.Available, snapshot.LoadedAt,
                AcademicYear.UpcomingIntake(ctx.Today), ctx.Priority));
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }
}
