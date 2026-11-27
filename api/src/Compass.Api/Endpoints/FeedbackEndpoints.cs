using Compass.Api.Infrastructure;
using Npgsql;

namespace Compass.Api.Endpoints;

public sealed record FeedbackRequest(int CourseId, string Field, string Message, string? PageUrl)
{
    public static readonly string[] Fields = ["fee", "entry-requirements", "english", "deadline", "living-costs", "other"];

    public Dictionary<string, string[]> Validate()
    {
        var e = new Dictionary<string, string[]>();
        if (CourseId <= 0) e["courseId"] = ["Required."];
        if (!Fields.Contains(Field)) e["field"] = [$"One of: {string.Join(", ", Fields)}."];
        if (string.IsNullOrWhiteSpace(Message) || Message.Length > 1000) e["message"] = ["1 to 1000 characters."];
        if (PageUrl is { Length: > 500 }) e["pageUrl"] = ["At most 500 characters."];
        return e;
    }
}

/// <summary>
/// "Report an error" from a course card. Reports go to a review queue; nothing changes until a human
/// re-checks the official page and updates data/courses.csv.
/// </summary>
public static class FeedbackEndpoints
{
    public static IEndpointRouteBuilder MapFeedbackEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/feedback", async (FeedbackRequest req, NpgsqlDataSource db, UsageTracker usage, HttpContext http, CancellationToken ct) =>
        {
            var errors = req.Validate();
            if (errors.Count > 0) return Results.ValidationProblem(errors);

            await using var cmd = db.CreateCommand(
                "INSERT INTO course_feedback (course_id, field, message, page_url) VALUES ($1, $2, $3, $4)");
            cmd.Parameters.AddWithValue(req.CourseId);
            cmd.Parameters.AddWithValue(req.Field);
            cmd.Parameters.AddWithValue(req.Message.Trim());
            cmd.Parameters.AddWithValue((object?)req.PageUrl ?? DBNull.Value);
            try
            {
                await cmd.ExecuteNonQueryAsync(ct);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["courseId"] = ["Unknown course."] });
            }
            usage.Track("report", http, new { req.Field });
            return Results.Accepted();
        }).RequireRateLimiting(RateLimits.Feedback);

        app.MapGet("/api/fx/{currency}", async (string currency, ExchangeRateService fx, CancellationToken ct) =>
        {
            var rate = await fx.GetAsync(currency, ct);
            return rate is null
                ? Results.Problem("Exchange rate unavailable. Enter the rate manually.", statusCode: StatusCodes.Status503ServiceUnavailable)
                : Results.Ok(rate);
        }).RequireRateLimiting(RateLimits.Api);

        return app;
    }
}
