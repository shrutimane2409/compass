using System.Net.Http.Headers;
using Compass.Api.Infrastructure;
using Microsoft.AspNetCore.Http.Features;

namespace Compass.Api.Endpoints;

/// <summary>
/// Relays the AI service's server-sent events to the browser.
/// Keeps one public origin, one rate limiter and one validation point.
/// </summary>
public static class ChatEndpoints
{
    public const string StreamClient = "ai-stream";
    private const long MaxBodyBytes = 32 * 1024;

    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/chat", ProxyAsync).RequireRateLimiting(RateLimits.Chat);
        app.MapPost("/api/warmup", Warmup).RequireRateLimiting(RateLimits.Api);
        return app;
    }

    private static long _lastWarmup;

    /// <summary>
    /// The free-tier AI service sleeps when idle and takes ~30-60 s to wake. The web app calls this on page load,
    /// so the AI service is usually awake by the time the student submits the form. Fire and forget, at most once a minute.
    /// </summary>
    private static IResult Warmup(IHttpClientFactory factory, ILogger<ChatStreamLog> log, TimeProvider time)
    {
        var now = time.GetUtcNow().ToUnixTimeSeconds();
        var last = Interlocked.Read(ref _lastWarmup);
        if (now - last < 60 || Interlocked.CompareExchange(ref _lastWarmup, now, last) != last) return Results.Accepted();
        _ = Task.Run(async () =>
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
                using var res = await factory.CreateClient(StreamClient).GetAsync("/health", cts.Token);
            }
            catch (Exception ex)
            {
                log.LogInformation("AI warm-up did not complete: {Message}", ex.Message);
            }
        });
        return Results.Accepted();
    }

    private static async Task ProxyAsync(HttpContext ctx, IHttpClientFactory factory, ILogger<ChatStreamLog> log, UsageTracker usage, CancellationToken ct)
    {
        if (ctx.Request.ContentLength is null or 0)
        {
            await Results.Problem("Request body is required.", statusCode: StatusCodes.Status400BadRequest).ExecuteAsync(ctx);
            return;
        }
        if (ctx.Request.ContentLength > MaxBodyBytes)
        {
            await Results.Problem("Message is too large.", statusCode: StatusCodes.Status413PayloadTooLarge).ExecuteAsync(ctx);
            return;
        }

        var client = factory.CreateClient(StreamClient);
        using var upstreamRequest = new HttpRequestMessage(HttpMethod.Post, "/chat")
        {
            Content = new StreamContent(ctx.Request.Body)
        };
        upstreamRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        upstreamRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        HttpResponseMessage upstream;
        try
        {
            upstream = await client.SendAsync(upstreamRequest, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (Exception ex) when ((ex is HttpRequestException or TaskCanceledException) && !ct.IsCancellationRequested)
        {
            log.LogWarning(ex, "AI service unreachable for chat.");
            await Unavailable(ctx);
            return;
        }

        using (upstream)
        {
            if (!upstream.IsSuccessStatusCode)
            {
                log.LogWarning("AI service returned {Status} for chat.", (int)upstream.StatusCode);
                await Unavailable(ctx);
                return;
            }

            usage.Track("chat", ctx);
            ctx.Response.ContentType = "text/event-stream";
            ctx.Response.Headers.CacheControl = "no-cache";
            ctx.Response.Headers["X-Accel-Buffering"] = "no";
            ctx.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

            await using var stream = await upstream.Content.ReadAsStreamAsync(ct);
            var buffer = new byte[4096];
            int read;
            while ((read = await stream.ReadAsync(buffer, ct)) > 0)
            {
                await ctx.Response.Body.WriteAsync(buffer.AsMemory(0, read), ct);
                await ctx.Response.Body.FlushAsync(ct);
            }
        }
    }

    private static Task Unavailable(HttpContext ctx) =>
        Results.Problem(
            "Chat is temporarily unavailable. Your shortlist and cost comparison still work.",
            statusCode: StatusCodes.Status503ServiceUnavailable).ExecuteAsync(ctx);

    /// <summary>Log category marker for chat streaming.</summary>
    public sealed class ChatStreamLog;
}
