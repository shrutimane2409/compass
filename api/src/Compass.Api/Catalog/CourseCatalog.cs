using Compass.Domain;
using Microsoft.Extensions.Caching.Memory;

namespace Compass.Api.Catalog;

public sealed record CatalogSnapshot(
    IReadOnlyList<Course> Courses,
    IReadOnlyDictionary<string, decimal> MonthlyLivingByCity,
    decimal OneOffEur,
    DateTimeOffset LoadedAt,
    IReadOnlyList<Scholarship> Scholarships);

/// <summary>
/// The catalog is small (tens of courses) and read on every request, so it lives in memory.
/// A semaphore stops concurrent cache misses from all hitting the database.
/// </summary>
public sealed class CourseCatalog(CourseRepository repo, IMemoryCache cache, TimeProvider time)
{
    public const string Country = "IE";
    public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
    private const string Key = "catalog:" + Country;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<CatalogSnapshot> GetAsync(CancellationToken ct)
    {
        if (cache.TryGetValue(Key, out CatalogSnapshot? hit) && hit is not null) return hit;

        await _lock.WaitAsync(ct);
        try
        {
            if (cache.TryGetValue(Key, out hit) && hit is not null) return hit;

            var courses = await repo.GetCoursesAsync(ct);
            var living = await repo.GetMonthlyLivingByCityAsync(ct);
            var oneOff = await repo.GetOneOffTotalAsync(Country, ct);
            var scholarships = await repo.GetScholarshipsAsync(ct);
            var snapshot = new CatalogSnapshot(courses, living, oneOff, time.GetUtcNow(), scholarships);

            cache.Set(Key, snapshot, Ttl);
            return snapshot;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Invalidate() => cache.Remove(Key);
}
