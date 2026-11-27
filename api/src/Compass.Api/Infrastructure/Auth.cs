using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Compass.Api.Infrastructure;

public sealed record GoogleIdentity(string Subject, string Email, string? Name, string? Picture);

/// <summary>
/// Sign-in is optional. The browser gets a Google ID token from Google Identity Services and sends it once;
/// the API verifies it against Google's published keys, then issues its own short session token (30 days).
/// A bearer token, not a cookie, because the web app and the API live on different sites.
/// </summary>
public sealed class AuthService
{
    public const string Issuer = "compass-api";
    public const string Audience = "compass-web";
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(30);
    private static readonly string[] GoogleIssuers = ["https://accounts.google.com", "accounts.google.com"];

    private readonly string? _googleClientId;
    private readonly SymmetricSecurityKey _signingKey;
    private readonly TimeProvider _time;
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _google;
    private readonly JsonWebTokenHandler _handler = new() { MapInboundClaims = false };

    public AuthService(string? googleClientId, SymmetricSecurityKey signingKey, TimeProvider time)
    {
        _googleClientId = googleClientId;
        _signingKey = signingKey;
        _time = time;
        _google = new ConfigurationManager<OpenIdConnectConfiguration>(
            "https://accounts.google.com/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever { RequireHttps = true });
    }

    public bool Enabled => !string.IsNullOrWhiteSpace(_googleClientId);

    /// <summary>Signing key from Auth:JwtSecret. Without one (local dev), a random per-process key: sessions just don't survive restarts.</summary>
    public static SymmetricSecurityKey SigningKey(IConfiguration config)
    {
        var secret = config["Auth:JwtSecret"];
        var bytes = string.IsNullOrEmpty(secret) ? RandomNumberGenerator.GetBytes(32) : Encoding.UTF8.GetBytes(secret);
        if (bytes.Length < 32) throw new InvalidOperationException("Auth:JwtSecret must be at least 32 characters.");
        return new SymmetricSecurityKey(bytes);
    }

    public static TokenValidationParameters SessionValidation(SecurityKey key) => new()
    {
        ValidIssuer = Issuer,
        ValidAudience = Audience,
        IssuerSigningKey = key,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(2),
    };

    public async Task<GoogleIdentity?> VerifyGoogleTokenAsync(string idToken, CancellationToken ct)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(idToken) || idToken.Length > 4096) return null;
        var oidc = await _google.GetConfigurationAsync(ct);
        var result = await _handler.ValidateTokenAsync(idToken, new TokenValidationParameters
        {
            ValidIssuers = GoogleIssuers,
            ValidAudience = _googleClientId,
            IssuerSigningKeys = oidc.SigningKeys,
            ValidateLifetime = true,
        });
        if (!result.IsValid) return null;

        string? Claim(string type) => result.Claims.TryGetValue(type, out var v) ? v?.ToString() : null;
        var sub = Claim("sub");
        var email = Claim("email");
        if (sub is null || email is null || !string.Equals(Claim("email_verified"), "true", StringComparison.OrdinalIgnoreCase))
            return null;
        return new GoogleIdentity(sub, email, Claim("name"), Claim("picture"));
    }

    public string IssueSession(Guid userId) => _handler.CreateToken(new SecurityTokenDescriptor
    {
        Issuer = Issuer,
        Audience = Audience,
        Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())]),
        IssuedAt = _time.GetUtcNow().UtcDateTime,
        NotBefore = _time.GetUtcNow().UtcDateTime,
        Expires = _time.GetUtcNow().Add(SessionLifetime).UtcDateTime,
        SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256),
    });

    public static Guid? UserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;
}

public static class AuthSetup
{
    public static IServiceCollection AddCompassAuth(this IServiceCollection services, IConfiguration config)
    {
        var key = AuthService.SigningKey(config);
        // One key for issuing and validating, so a dev-mode random key still works within the process.
        services.AddSingleton(sp => new AuthService(config["Auth:GoogleClientId"], key, sp.GetRequiredService<TimeProvider>()));
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.MapInboundClaims = false;
                o.TokenValidationParameters = AuthService.SessionValidation(key);
            });
        services.AddAuthorization();
        return services;
    }
}
