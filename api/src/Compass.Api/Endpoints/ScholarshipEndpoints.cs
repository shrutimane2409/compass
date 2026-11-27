using Compass.Api.Catalog;
using Compass.Api.Infrastructure;
using Compass.Domain;

namespace Compass.Api.Endpoints;

/// <summary>CourseIds = the courses the student is weighing (saved ones). Without them, every course they can apply to.</summary>
public sealed record ScholarshipMatchRequest(StudentProfile Profile, IReadOnlyList<int>? CourseIds);

public static class ScholarshipEndpoints
{
    public static IEndpointRouteBuilder MapScholarshipEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/scholarships").RequireRateLimiting(RateLimits.Api);

        api.MapGet("", async (CourseCatalog catalog, CancellationToken ct) => Results.Ok((await catalog.GetAsync(ct)).Scholarships));

        api.MapPost("/match", async (ScholarshipMatchRequest req, CourseCatalog catalog, RecommendationEngine engine,
            ScholarshipMatcher matcher, TimeProvider time, CancellationToken ct) =>
        {
            var errors = new RecommendationRequest(req.Profile).Validate();
            if (req.CourseIds is { Count: > 100 }) errors["courseIds"] = ["At most 100 courses."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);

            var profile = req.Profile with { Interests = req.Profile.Interests ?? [] };
            var snapshot = await catalog.GetAsync(ct);
            IReadOnlyList<Course> courses;
            if (req.CourseIds is { Count: > 0 } ids)
            {
                courses = snapshot.Courses.Where(c => ids.Contains(c.Id)).ToList();
            }
            else
            {
                // Rules only (no AI call): which courses the student can apply to.
                var ctx = new RecommendationContext(snapshot.MonthlyLivingByCity, snapshot.OneOffEur, new Dictionary<int, double>(),
                    DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime));
                courses = engine.Recommend(profile, snapshot.Courses, ctx)
                    .Where(r => r.Eligibility.Status != EligibilityStatus.NotEligible)
                    .Select(r => r.Course).ToList();
            }
            return Results.Ok(matcher.Match(profile, snapshot.Scholarships, courses));
        });

        return app;
    }
}
