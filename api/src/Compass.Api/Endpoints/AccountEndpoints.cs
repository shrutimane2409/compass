using System.Text.Json;
using System.Text.RegularExpressions;
using Compass.Api.Infrastructure;
using Compass.Domain;
using Dapper;
using Npgsql;

namespace Compass.Api.Endpoints;

public sealed record GoogleSignInRequest(string Credential);
public sealed record UserDto(Guid Id, string Email, string? Name, string? PictureUrl);
public sealed record SignInResponse(string Token, DateTimeOffset ExpiresAt, UserDto User);

public sealed record SaveProfileRequest(StudentProfile Profile, Priority? Priority);
public sealed record SaveCourseRequest(string? Note);
public sealed record ApplicationRequest(string Status, Dictionary<string, bool>? Documents, string? Notes);
public sealed record ChecklistRequest(bool Done);

public sealed record SavedCourseDto(int CourseId, string? Note, DateTime CreatedAt);
public sealed record ApplicationDto(int CourseId, string Status, Dictionary<string, bool> Documents, string? Notes, DateTime UpdatedAt);
public sealed record MeResponse(
    UserDto User,
    StudentProfile? Profile,
    Priority Priority,
    IReadOnlyList<SavedCourseDto> Saved,
    IReadOnlyList<ApplicationDto> Applications,
    IReadOnlyList<string> ChecklistDone);

/// <summary>
/// Everything a signed-in student saves. All queries are scoped to the user id in the session token,
/// so one student can never read or change another's data.
/// </summary>
public static partial class AccountEndpoints
{
    public static readonly string[] Statuses = ["researching", "preparing", "applied", "offer", "accepted", "rejected"];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    [GeneratedRegex("^[a-z0-9-]{1,40}$")]
    private static partial Regex Slug();

    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/auth/config", (AuthService auth, IConfiguration config) =>
            Results.Ok(new { enabled = auth.Enabled, googleClientId = auth.Enabled ? config["Auth:GoogleClientId"] : null }));

        app.MapPost("/api/auth/google", SignInAsync).RequireRateLimiting(RateLimits.Api);

        var me = app.MapGroup("/api/me").RequireAuthorization().RequireRateLimiting(RateLimits.Api);
        me.MapGet("", GetMeAsync);
        me.MapPut("/profile", SaveProfileAsync);
        me.MapPut("/saved/{courseId:int}", SaveCourseAsync);
        me.MapDelete("/saved/{courseId:int}", (int courseId, HttpContext http, NpgsqlDataSource db, CancellationToken ct) =>
            Exec(db, http, "DELETE FROM saved_courses WHERE user_id = @u AND course_id = @courseId", new { courseId }, ct));
        me.MapPut("/applications/{courseId:int}", SaveApplicationAsync);
        me.MapDelete("/applications/{courseId:int}", (int courseId, HttpContext http, NpgsqlDataSource db, CancellationToken ct) =>
            Exec(db, http, "DELETE FROM applications WHERE user_id = @u AND course_id = @courseId", new { courseId }, ct));
        me.MapPut("/checklist/{itemId}", SaveChecklistAsync);
        me.MapGet("/export", ExportAsync);
        me.MapDelete("", (HttpContext http, NpgsqlDataSource db, CancellationToken ct) =>
            Exec(db, http, "DELETE FROM users WHERE id = @u", null, ct));
        return app;
    }

    private static async Task<IResult> SignInAsync(GoogleSignInRequest req, AuthService auth, NpgsqlDataSource db,
        UsageTracker usage, HttpContext http, TimeProvider time, CancellationToken ct)
    {
        if (!auth.Enabled) return Results.Problem("Sign-in isn't configured on this server.", statusCode: StatusCodes.Status503ServiceUnavailable);
        var identity = await auth.VerifyGoogleTokenAsync(req.Credential, ct);
        if (identity is null) return Results.Problem("Google sign-in could not be verified.", statusCode: StatusCodes.Status401Unauthorized);

        const string sql = """
            INSERT INTO users (google_sub, email, name, picture_url) VALUES (@Subject, @Email, @Name, @Picture)
            ON CONFLICT (google_sub) DO UPDATE SET email = EXCLUDED.email, name = EXCLUDED.name,
                picture_url = EXCLUDED.picture_url, last_seen_at = now()
            RETURNING id, email, name, picture_url AS PictureUrl
            """;
        await using var conn = await db.OpenConnectionAsync(ct);
        var user = await conn.QuerySingleAsync<UserDto>(new CommandDefinition(sql, identity, cancellationToken: ct));
        usage.Track("signin", http);
        return Results.Ok(new SignInResponse(auth.IssueSession(user.Id), time.GetUtcNow().Add(AuthService.SessionLifetime), user));
    }

    private static async Task<IResult> GetMeAsync(HttpContext http, NpgsqlDataSource db, CancellationToken ct)
    {
        if (AuthService.UserId(http.User) is not { } u) return Results.Unauthorized();
        await using var conn = await db.OpenConnectionAsync(ct);
        var user = await conn.QuerySingleOrDefaultAsync<UserDto>(new CommandDefinition(
            "SELECT id, email, name, picture_url AS PictureUrl FROM users WHERE id = @u", new { u }, cancellationToken: ct));
        if (user is null) return Results.Unauthorized(); // account deleted: the client signs out

        var profileRow = await conn.QuerySingleOrDefaultAsync<ProfileRow>(new CommandDefinition(
            "SELECT profile::text AS Profile, priority AS Priority FROM user_profiles WHERE user_id = @u", new { u }, cancellationToken: ct));
        var saved = (await conn.QueryAsync<SavedCourseDto>(new CommandDefinition(
            "SELECT course_id AS CourseId, note, created_at AS CreatedAt FROM saved_courses WHERE user_id = @u ORDER BY created_at DESC",
            new { u }, cancellationToken: ct))).ToList();
        var apps = (await conn.QueryAsync<ApplicationRow>(new CommandDefinition(
            "SELECT course_id AS CourseId, status AS Status, documents::text AS Documents, notes AS Notes, updated_at AS UpdatedAt FROM applications WHERE user_id = @u ORDER BY updated_at DESC",
            new { u }, cancellationToken: ct)))
            .Select(a => new ApplicationDto(a.CourseId, a.Status,
                JsonSerializer.Deserialize<Dictionary<string, bool>>(a.Documents, Json) ?? [], a.Notes, a.UpdatedAt))
            .ToList();
        var done = (await conn.QueryAsync<string>(new CommandDefinition(
            "SELECT item_id FROM checklist_progress WHERE user_id = @u", new { u }, cancellationToken: ct))).ToList();

        StudentProfile? profile = null;
        var priority = Priority.Balanced;
        if (profileRow is not null)
        {
            var p = profileRow;
            profile = JsonSerializer.Deserialize<StudentProfile>(p.Profile, Json);
            Enum.TryParse(p.Priority, out priority);
        }
        return Results.Ok(new MeResponse(user, profile, priority, saved, apps, done));
    }

    private static async Task<IResult> SaveProfileAsync(SaveProfileRequest req, HttpContext http, NpgsqlDataSource db, CancellationToken ct)
    {
        var errors = new RecommendationRequest(req.Profile, req.Priority).Validate();
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var profile = req.Profile with { Interests = req.Profile.Interests ?? [] };
        return await Exec(db, http, """
            INSERT INTO user_profiles (user_id, profile, priority) VALUES (@u, @profile::jsonb, @priority)
            ON CONFLICT (user_id) DO UPDATE SET profile = EXCLUDED.profile, priority = EXCLUDED.priority, updated_at = now()
            """, new { profile = JsonSerializer.Serialize(profile, Json), priority = (req.Priority ?? Priority.Balanced).ToString() }, ct);
    }

    private static async Task<IResult> SaveCourseAsync(int courseId, SaveCourseRequest? req, HttpContext http, NpgsqlDataSource db,
        UsageTracker usage, CancellationToken ct)
    {
        if (req?.Note is { Length: > 500 }) return Results.ValidationProblem(new Dictionary<string, string[]> { ["note"] = ["At most 500 characters."] });
        usage.Track("save", http);
        return await Exec(db, http, """
            INSERT INTO saved_courses (user_id, course_id, note) VALUES (@u, @courseId, @note)
            ON CONFLICT (user_id, course_id) DO UPDATE SET note = EXCLUDED.note
            """, new { courseId, note = req?.Note?.Trim() }, ct);
    }

    private static async Task<IResult> SaveApplicationAsync(int courseId, ApplicationRequest req, HttpContext http, NpgsqlDataSource db,
        UsageTracker usage, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (!Statuses.Contains(req.Status)) errors["status"] = [$"One of: {string.Join(", ", Statuses)}."];
        var docs = req.Documents ?? [];
        if (docs.Count > 15 || docs.Keys.Any(k => !Slug().IsMatch(k))) errors["documents"] = ["Up to 15 items with short lowercase ids."];
        if (req.Notes is { Length: > 2000 }) errors["notes"] = ["At most 2000 characters."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        usage.Track("track", http, new { req.Status });
        return await Exec(db, http, """
            INSERT INTO applications (user_id, course_id, status, documents, notes) VALUES (@u, @courseId, @status, @docs::jsonb, @notes)
            ON CONFLICT (user_id, course_id) DO UPDATE SET status = EXCLUDED.status, documents = EXCLUDED.documents,
                notes = EXCLUDED.notes, updated_at = now()
            """, new { courseId, status = req.Status, docs = JsonSerializer.Serialize(docs, Json), notes = req.Notes?.Trim() }, ct);
    }

    private static async Task<IResult> SaveChecklistAsync(string itemId, ChecklistRequest req, HttpContext http, NpgsqlDataSource db, CancellationToken ct)
    {
        if (!Slug().IsMatch(itemId)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["itemId"] = ["Invalid item id."] });
        return await Exec(db, http, req.Done
            ? "INSERT INTO checklist_progress (user_id, item_id) VALUES (@u, @itemId) ON CONFLICT DO NOTHING"
            : "DELETE FROM checklist_progress WHERE user_id = @u AND item_id = @itemId", new { itemId }, ct);
    }

    /// <summary>Everything stored about the student, as a downloadable JSON file.</summary>
    private static async Task<IResult> ExportAsync(HttpContext http, NpgsqlDataSource db, CancellationToken ct)
    {
        var me = await GetMeAsync(http, db, ct);
        if (me is not Microsoft.AspNetCore.Http.HttpResults.Ok<MeResponse> ok) return me;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { exportedAt = DateTimeOffset.UtcNow, data = ok.Value }, Json);
        return Results.File(bytes, "application/json", "compass-my-data.json");
    }

    private sealed class ProfileRow
    {
        public string Profile { get; set; } = "{}";
        public string Priority { get; set; } = "Balanced";
    }

    private sealed class ApplicationRow
    {
        public int CourseId { get; set; }
        public string Status { get; set; } = "";
        public string Documents { get; set; } = "{}";
        public string? Notes { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>Runs a statement for the signed-in user (@u). Unknown course ids come back as 400, not 500.</summary>
    private static async Task<IResult> Exec(NpgsqlDataSource db, HttpContext http, string sql, object? args, CancellationToken ct)
    {
        if (AuthService.UserId(http.User) is not { } u) return Results.Unauthorized();
        var p = new DynamicParameters(args);
        p.Add("u", u);
        try
        {
            await using var conn = await db.OpenConnectionAsync(ct);
            await conn.ExecuteAsync(new CommandDefinition(sql, p, cancellationToken: ct));
            return Results.NoContent();
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["courseId"] = ["Unknown course."] });
        }
    }
}
