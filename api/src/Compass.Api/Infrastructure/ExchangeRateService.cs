using System.Globalization;
using System.Xml.Linq;
using Microsoft.Extensions.Caching.Memory;

namespace Compass.Api.Infrastructure;

public sealed record ExchangeRate(string Currency, decimal PerEur, DateOnly Date, string Source, string SourceUrl);

/// <summary>
/// Official euro reference rates from the European Central Bank, published once per working day.
/// Cached for 6 hours; the last good rate is kept if the ECB is unreachable.
/// </summary>
public sealed class ExchangeRateService(HttpClient http, IMemoryCache cache, ILogger<ExchangeRateService> log)
{
    public const string FeedUrl = "https://www.ecb.europa.eu/stats/eurofxref/eurofxref-daily.xml";
    private const string CacheKey = "fx:ecb";
    private const string LastGoodKey = "fx:ecb:last-good";
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(6);

    public async Task<ExchangeRate?> GetAsync(string currency, CancellationToken ct)
    {
        var rates = await GetRatesAsync(ct);
        return rates?.GetValueOrDefault(currency.ToUpperInvariant());
    }

    private async Task<IReadOnlyDictionary<string, ExchangeRate>?> GetRatesAsync(CancellationToken ct)
    {
        if (cache.TryGetValue(CacheKey, out IReadOnlyDictionary<string, ExchangeRate>? hit)) return hit;
        try
        {
            var xml = await http.GetStringAsync(FeedUrl, ct);
            var rates = Parse(xml);
            cache.Set(CacheKey, rates, Ttl);
            cache.Set(LastGoodKey, rates);
            return rates;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            log.LogWarning(ex, "ECB reference rates unavailable; using the last good rates if any.");
            return cache.TryGetValue(LastGoodKey, out IReadOnlyDictionary<string, ExchangeRate>? last) ? last : null;
        }
    }

    /// <summary>Parses the ECB daily feed: Cube[@time] containing Cube[@currency, @rate] (units of currency per 1 EUR).</summary>
    public static IReadOnlyDictionary<string, ExchangeRate> Parse(string xml)
    {
        var doc = XDocument.Parse(xml);
        var day = doc.Descendants().First(e => e.Name.LocalName == "Cube" && e.Attribute("time") is not null);
        var date = DateOnly.ParseExact(day.Attribute("time")!.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        return day.Elements()
            .Where(e => e.Attribute("currency") is not null && e.Attribute("rate") is not null)
            .ToDictionary(
                e => e.Attribute("currency")!.Value,
                e => new ExchangeRate(e.Attribute("currency")!.Value,
                    decimal.Parse(e.Attribute("rate")!.Value, CultureInfo.InvariantCulture),
                    date, "European Central Bank euro reference rate", FeedUrl));
    }
}
