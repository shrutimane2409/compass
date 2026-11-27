using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Compass.Api.Infrastructure;

public static class RateLimits
{
    public const string Api = "api";
    public const string Chat = "chat";
    public const string Feedback = "feedback";

    public static IServiceCollection AddCompassRateLimits(this IServiceCollection services) =>
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            o.AddPolicy(Api, ctx => RateLimitPartition.GetFixedWindowLimiter(ClientKey(ctx), _ =>
                new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

            o.AddPolicy(Feedback, ctx => RateLimitPartition.GetFixedWindowLimiter(ClientKey(ctx), _ =>
                new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(10), QueueLimit = 0 }));

            // Chat consumes free LLM quota, so it gets a tighter token bucket.
            o.AddPolicy(Chat, ctx => RateLimitPartition.GetTokenBucketLimiter(ClientKey(ctx), _ =>
                new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 10,
                    TokensPerPeriod = 10,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
        });

    private static string ClientKey(HttpContext ctx) => ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
