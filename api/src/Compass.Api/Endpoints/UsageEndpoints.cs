using System.Security.Cryptography;
using System.Text;
using Compass.Api.Infrastructure;
using Dapper;
using Npgsql;

namespace Compass.Api.Endpoints;

public sealed record HelpfulRequest(bool Helpful, string Context)
{
    public static readonly string[] Contexts = ["shortlist", "chat"];
}

public sealed class UsageStats
{
    public long SessionsTotal { get; set; }
    public long Sessions7d { get; set; }
    public long Searches { get; set; }
    public long Searches7d { get; set; }
    public long Chats { get; set; }
    public long HelpfulYes { get; set; }
    public long HelpfulNo { get; set; }
    public double? SearchP50Ms { get; set; }
    public double? SearchP95Ms { get; set; }
    public long ReportsOpen { get; set; }
    public long ReportsTotal { get; set; }
}

public static class UsageEndpoints
{
    public static IEndpointRouteBuilder MapUsageEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/events/helpful", (HelpfulRequest req, HttpContext http, UsageTracker usage) =>
        {
            if (!HelpfulRequest.Contexts.Contains(req.Context))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["context"] = ["shortlist or chat."] });
            usage.Track("helpful", http, new { req.Helpful, req.Context });
            return Results.Accepted();
        }).RequireRateLimiting(RateLimits.Feedback);

        // Owner-only metrics. Disabled (404) unless Admin:Key is configured.
        app.MapGet("/api/admin/stats", async (HttpContext http, IConfiguration config, NpgsqlDataSource db, CancellationToken ct) =>
        {
            var key = config["Admin:Key"];
            if (string.IsNullOrEmpty(key)) return Results.NotFound();
            var given = Encoding.UTF8.GetBytes(http.Request.Headers["X-Admin-Key"].ToString());
            if (!CryptographicOperations.FixedTimeEquals(given, Encoding.UTF8.GetBytes(key))) return Results.Unauthorized();

            const string sql = """
                SELECT
                  (SELECT count(DISTINCT session_id) FROM usage_events) AS SessionsTotal,
                  (SELECT count(DISTINCT session_id) FROM usage_events WHERE occurred_at > now() - interval '7 days') AS Sessions7d,
                  (SELECT count(*) FROM usage_events WHERE event = 'search') AS Searches,
                  (SELECT count(*) FROM usage_events WHERE event = 'search' AND occurred_at > now() - interval '7 days') AS Searches7d,
                  (SELECT count(*) FROM usage_events WHERE event = 'chat') AS Chats,
                  (SELECT count(*) FROM usage_events WHERE event = 'helpful' AND (props->>'helpful')::boolean) AS HelpfulYes,
                  (SELECT count(*) FROM usage_events WHERE event = 'helpful' AND NOT (props->>'helpful')::boolean) AS HelpfulNo,
                  (SELECT percentile_cont(0.5) WITHIN GROUP (ORDER BY (props->>'ms')::double precision)
                     FROM usage_events WHERE event = 'search') AS SearchP50Ms,
                  (SELECT percentile_cont(0.95) WITHIN GROUP (ORDER BY (props->>'ms')::double precision)
                     FROM usage_events WHERE event = 'search') AS SearchP95Ms,
                  (SELECT count(*) FROM course_feedback WHERE status = 'open') AS ReportsOpen,
                  (SELECT count(*) FROM course_feedback) AS ReportsTotal
                """;
            await using var conn = await db.OpenConnectionAsync(ct);
            return Results.Ok(await conn.QuerySingleAsync<UsageStats>(new CommandDefinition(sql, cancellationToken: ct)));
        });

        return app;
    }
}
