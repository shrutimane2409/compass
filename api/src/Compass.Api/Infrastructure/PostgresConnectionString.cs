using System.Data.Common;

namespace Compass.Api.Infrastructure;

/// <summary>
/// Accepts either a key/value connection string (Host=...;Database=...) or a URL
/// (postgresql://user:password@host:port/db?sslmode=require), as hosting dashboards show both.
/// Surrounding quotes, whitespace and a trailing semicolon from copy-paste are removed.
/// </summary>
public static class PostgresConnectionString
{
    public static string Normalize(string raw)
    {
        var value = raw.Trim().Trim('"', '\'').Trim().TrimEnd(';');

        if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return value;

        var uri = new Uri(value);
        var userInfo = uri.UserInfo.Split(':', 2);
        var query = uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .ToDictionary(p => Uri.UnescapeDataString(p[0]), p => p.Length > 1 ? Uri.UnescapeDataString(p[1]) : "",
                          StringComparer.OrdinalIgnoreCase);

        var b = new DbConnectionStringBuilder
        {
            ["Host"] = uri.Host,
            ["Port"] = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
            ["Database"] = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            ["Username"] = Uri.UnescapeDataString(userInfo[0]),
        };
        if (userInfo.Length > 1) b["Password"] = Uri.UnescapeDataString(userInfo[1]);
        if (query.TryGetValue("sslmode", out var ssl) && ssl.Length > 0)
            b["SSL Mode"] = ssl switch
            {
                "require" => "Require",
                "verify-full" => "VerifyFull",
                "verify-ca" => "VerifyCA",
                "prefer" => "Prefer",
                "disable" => "Disable",
                _ => "Require"
            };
        return b.ConnectionString;
    }
}
