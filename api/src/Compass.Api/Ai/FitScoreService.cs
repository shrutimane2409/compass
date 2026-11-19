using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace Compass.Api.Ai;

public sealed record FitResult(IReadOnlyDictionary<int, double> Scores, bool Available);

/// <summary>
/// Fit scores depend only on the student's interests and the course set, so they are cached.
/// What-if changes (budget, grade) reuse them and never leave the API process.
/// </summary>
public sealed class FitScoreService(AiServiceClient ai, IMemoryCache cache)
{
    public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);
    private static readonly IReadOnlyDictionary<int, double> Empty = new Dictionary<int, double>();

    public async Task<FitResult> GetAsync(IReadOnlyList<string> interests, IReadOnlyList<int> courseIds, CancellationToken ct)
    {
        if (interests.Count == 0 || courseIds.Count == 0) return new FitResult(Empty, false);

        var key = CacheKey(interests, courseIds);
        if (cache.TryGetValue(key, out IReadOnlyDictionary<int, double>? cached) && cached is not null)
            return new FitResult(cached, true);

        var scores = await ai.GetFitScoresAsync(new FitRequest(interests, courseIds), ct);
        if (scores is null) return new FitResult(Empty, false);

        cache.Set(key, scores, Ttl);
        return new FitResult(scores, true);
    }

    private static string CacheKey(IReadOnlyList<string> interests, IReadOnlyList<int> courseIds)
    {
        var normalised = string.Join('|', interests.Select(i => i.Trim().ToLowerInvariant()).Where(i => i.Length > 0).Order())
                       + "#" + string.Join(',', courseIds.Order());
        return "fit:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalised)));
    }
}
