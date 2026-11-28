using System.Net;
using System.Net.Http.Headers;
using Compass.Api.Infrastructure;
using Dapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Compass.Api.Tests.Security;

/// <summary>
/// Hosts the real API in memory against a throwaway Postgres (Docker) built from db/init, so the security tests
/// exercise the actual auth, SQL scoping and rate limiting. The AI service points at a closed port.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string JwtSecret = "test-only-jwt-secret-0123456789-abcdefghij";
    public const string AdminKey = "test-only-admin-key-0123456789";

    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder().WithImage("pgvector/pgvector:pg16").Build();
    private int _ip;

    public int CourseId { get; private set; }
    public string ConnectionString => _db.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        foreach (var file in Directory.GetFiles(Path.Combine(RepoRoot(), "db", "init"), "*.sql").Order(StringComparer.Ordinal))
            await conn.ExecuteAsync(await File.ReadAllTextAsync(file));

        CourseId = await conn.ExecuteScalarAsync<int>("""
            INSERT INTO courses (university_id, title, award, field, duration_months, intake_month, fee_non_eu_eur, source_url, verified_at)
            VALUES ((SELECT min(id) FROM universities), 'MSc Security Test', 'MSc', 'computer-science', 12, 9, 20000,
                    'https://example.org/course', current_date)
            RETURNING id
            """);
    }

    Task IAsyncLifetime.DisposeAsync() => _db.DisposeAsync().AsTask();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Compass", ConnectionString);
        builder.UseSetting("AiService:BaseUrl", "http://127.0.0.1:9");
        builder.UseSetting("Auth:JwtSecret", JwtSecret);
        builder.UseSetting("Auth:GoogleClientId", "test-client.apps.googleusercontent.com");
        builder.UseSetting("Admin:Key", AdminKey);
        builder.UseSetting("ForwardedHeaders:Enabled", "false");
        builder.ConfigureTestServices(s => s.AddSingleton<IStartupFilter, TestClientIpFilter>());
    }

    /// <summary>A client with its own IP (so rate limits don't leak between tests) and, optionally, a session token.</summary>
    public HttpClient ClientFor(Guid? userId = null, string? token = null, string? ip = null)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TestClientIpFilter.Header, ip ?? NextIp());
        token ??= userId is { } u ? Services.GetRequiredService<AuthService>().IssueSession(u) : null;
        if (token is not null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public string NextIp()
    {
        var n = Interlocked.Increment(ref _ip);
        return $"10.{(n >> 16) & 255}.{(n >> 8) & 255}.{n & 255}";
    }

    public async Task<(Guid Id, string Email)> CreateUserAsync()
    {
        var email = $"student-{Guid.NewGuid():N}@example.org";
        await using var conn = new NpgsqlConnection(ConnectionString);
        var id = await conn.ExecuteScalarAsync<Guid>(
            "INSERT INTO users (google_sub, email) VALUES (@sub, @email) RETURNING id", new { sub = Guid.NewGuid().ToString(), email });
        return (id, email);
    }

    public async Task<T> QueryAsync<T>(string sql, object? args = null)
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        return await conn.ExecuteScalarAsync<T>(sql, args) ?? throw new InvalidOperationException("No result.");
    }

    public async Task ExecAsync(string sql, object? args = null)
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.ExecuteAsync(sql, args);
    }

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (Directory.Exists(Path.Combine(dir.FullName, "db", "init"))) return dir.FullName;
        throw new DirectoryNotFoundException("Could not find db/init above the test output folder.");
    }
}

/// <summary>Test-only: lets each client pick its source IP, which is what the rate limiter partitions on.</summary>
internal sealed class TestClientIpFilter : IStartupFilter
{
    public const string Header = "X-Test-Client-Ip";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (ctx, nextMiddleware) =>
        {
            if (IPAddress.TryParse(ctx.Request.Headers[Header].ToString(), out var ip)) ctx.Connection.RemoteIpAddress = ip;
            await nextMiddleware(ctx);
        });
        next(app);
    };
}

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>;
