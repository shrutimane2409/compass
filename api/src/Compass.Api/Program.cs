using System.Text.Json.Serialization;
using Compass.Api.Ai;
using Compass.Api.Catalog;
using Compass.Api.Endpoints;
using Compass.Api.Infrastructure;
using Compass.Domain;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Npgsql;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

var connectionString = PostgresConnectionString.Normalize(config.GetConnectionString("Compass")
    ?? throw new InvalidOperationException("Connection string 'Compass' is missing."));
var aiBaseUrl = new Uri(config["AiService:BaseUrl"]
    ?? throw new InvalidOperationException("AiService:BaseUrl is missing."));
var origins = config.GetSection("Cors:Origins").Get<string[]>() ?? [];
var aiInternalKey = config["AiService:InternalKey"];
void ConfigureAi(HttpClient c)
{
    c.BaseAddress = aiBaseUrl;
    if (!string.IsNullOrEmpty(aiInternalKey)) c.DefaultRequestHeaders.Add("X-Internal-Key", aiInternalKey);
}

// Data
builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));
builder.Services.AddSingleton<CourseRepository>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<CourseCatalog>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<UsageTracker>();
builder.Services.AddHostedService<UsageWriter>();

// Domain
builder.Services.AddSingleton<EligibilityEvaluator>();
builder.Services.AddSingleton<CostCalculator>();
builder.Services.AddSingleton<RecommendationEngine>();
builder.Services.AddSingleton<ScholarshipMatcher>();

// AI service: short, resilient calls for scoring; a separate long-lived client for streaming
builder.Services.AddHttpClient<AiServiceClient>(ConfigureAi)
    .AddStandardResilienceHandler(o =>
    {
        o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(3);
        o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(8);
        o.Retry.MaxRetryAttempts = 2;
    });
builder.Services.AddHttpClient(ChatEndpoints.StreamClient, c =>
{
    ConfigureAi(c);
    c.Timeout = TimeSpan.FromMinutes(2);
});
builder.Services.AddScoped<FitScoreService>();
builder.Services.AddHttpClient<ExchangeRateService>(c => c.Timeout = TimeSpan.FromSeconds(10))
    .AddStandardResilienceHandler();

// Platform
builder.Services.AddProblemDetails();
builder.Services.AddCompassRateLimits();
builder.Services.AddCompassAuth(config);
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

if (config.GetValue<bool>("ForwardedHeaders:Enabled"))
{
    // Behind a managed proxy (e.g. Cloud Run) the only caller is the platform's front end.
    builder.Services.Configure<ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        o.KnownProxies.Clear();
        o.KnownIPNetworks.Clear();
    });
}

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("compass-api"))
    .WithTracing(t =>
    {
        t.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation();
        if (!string.IsNullOrWhiteSpace(config["OTEL_EXPORTER_OTLP_ENDPOINT"])) t.AddOtlpExporter();
    });

var app = builder.Build();

if (config.GetValue<bool>("ForwardedHeaders:Enabled")) app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });
app.MapRecommendationEndpoints();
app.MapChatEndpoints();
app.MapFeedbackEndpoints();
app.MapUsageEndpoints();
app.MapAccountEndpoints();
app.MapScholarshipEndpoints();

app.Run();
