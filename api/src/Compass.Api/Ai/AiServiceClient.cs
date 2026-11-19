using System.Net.Http.Json;

namespace Compass.Api.Ai;

public sealed record FitRequest(IReadOnlyList<string> Interests, IReadOnlyList<int> CourseIds);
public sealed record FitResponse(Dictionary<int, double> Scores);

/// <summary>Typed client for the AI service. Resilience (timeout, retry, circuit breaker) is configured on the HttpClient.</summary>
public sealed class AiServiceClient(HttpClient http, ILogger<AiServiceClient> log)
{
    public async Task<IReadOnlyDictionary<int, double>?> GetFitScoresAsync(FitRequest request, CancellationToken ct)
    {
        try
        {
            using var response = await http.PostAsJsonAsync("/fit", request, ct);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<FitResponse>(ct);
            return body?.Scores;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            log.LogWarning(ex, "Fit scoring unavailable; falling back to neutral scores.");
            return null;
        }
    }
}
