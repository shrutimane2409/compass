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

var connectionString = config.GetConnectionString("Compass")
    ?? throw new InvalidOperationException("Connection string 'Compass' is missing.");
var aiBaseUrl = new Uri(config["AiService:BaseUrl"]
    ?? throw new InvalidOperationException("AiService:BaseUrl is missing."));
var origins = config.GetSection("Cors:Origins").Get<string[]>() ?? [];

// Data
builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));
builder.Services.AddSingleton<CourseRepository>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<CourseCatalog>();
builder.Services.AddSingleton(TimeProvider.System);

// Domain
builder.Services.AddSingleton<EligibilityEvaluator>();
builder.Services.AddSingleton<CostCalculator>();
builder.Services.AddSingleton<RecommendationEngine>();

// AI service: short, resilient calls for scoring; a separate long-lived client for streaming
builder.Services.AddHttpClient<AiServiceClient>(c => c.BaseAddress = aiBaseUrl)
    .AddStandardResilienceHandler(o =>
    {
        o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(3);
        o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(8);
        o.Retry.MaxRetryAttempts = 2;
    });
builder.Services.AddHttpClient(ChatEndpoints.StreamClient, c =>
{
    c.BaseAddress = aiBaseUrl;
    c.Timeout = TimeSpan.FromMinutes(2);
});
builder.Services.AddScoped<FitScoreService>();

// Platform
builder.Services.AddProblemDetails();
builder.Services.AddCompassRateLimits();
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
        o.KnownNetworks.Clear();
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
app.UseRateLimiter();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });
app.MapRecommendationEndpoints();
app.MapChatEndpoints();

app.Run();
