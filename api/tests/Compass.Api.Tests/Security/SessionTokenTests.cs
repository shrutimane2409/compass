using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Compass.Api.Infrastructure;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Compass.Api.Tests.Security;

/// <summary>Only tokens this API issued, unmodified and unexpired, open an account.</summary>
[Collection("api")]
public class SessionTokenTests(ApiFactory api)
{
    [Fact]
    public async Task A_valid_token_opens_the_account()
    {
        var a = await api.CreateUserAsync();
        Assert.Equal(HttpStatusCode.OK, (await api.ClientFor(token: Token(a.Id)).GetAsync("/api/me")).StatusCode);
    }

    public static TheoryData<string> BadTokens() => new()
    {
        "not-a-jwt",
        "wrong-key",
        "expired",
        "wrong-issuer",
        "wrong-audience",
        "alg-none",
    };

    [Theory]
    [MemberData(nameof(BadTokens))]
    public async Task Bad_tokens_are_rejected(string kind)
    {
        var a = await api.CreateUserAsync();
        var now = DateTime.UtcNow;
        var token = kind switch
        {
            "not-a-jwt" => "not-a-jwt",
            "wrong-key" => Token(a.Id, secret: "some-other-secret-that-is-long-enough-123"),
            "expired" => Token(a.Id, notBefore: now.AddDays(-31), expires: now.AddDays(-1)),
            "wrong-issuer" => Token(a.Id, issuer: "evil-issuer"),
            "wrong-audience" => Token(a.Id, audience: "evil-audience"),
            "alg-none" => $"{B64("""{"alg":"none","typ":"JWT"}""")}.{B64($$"""{"sub":"{{a.Id}}","iss":"{{AuthService.Issuer}}","aud":"{{AuthService.Audience}}","exp":{{DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()}}}""")}.",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.ClientFor(token: token).GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task Changing_the_user_id_inside_a_token_breaks_it()
    {
        var a = await api.CreateUserAsync();
        var b = await api.CreateUserAsync();
        var parts = Token(a.Id).Split('.');
        var forgedPayload = Base64UrlEncoder.Decode(parts[1]).Replace(a.Id.ToString(), b.Id.ToString());
        var forged = $"{parts[0]}.{Base64UrlEncoder.Encode(forgedPayload)}.{parts[2]}";

        Assert.Equal(HttpStatusCode.Unauthorized, (await api.ClientFor(token: forged).GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task Valid_token_for_an_unknown_user_cannot_read_or_write()
    {
        var client = api.ClientFor(token: Token(Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync($"/api/me/saved/{api.CourseId}", new { note = "x" })).StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("oversized")]
    public async Task Google_sign_in_rejects_empty_or_oversized_credentials(string credential)
    {
        if (credential == "oversized") credential = new string('a', 5000);
        var response = await api.ClientFor().PostAsJsonAsync("/api/auth/google", new { credential });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static string B64(string s) => Base64UrlEncoder.Encode(s);

    private static string Token(Guid sub, string secret = ApiFactory.JwtSecret, string issuer = AuthService.Issuer,
        string audience = AuthService.Audience, DateTime? notBefore = null, DateTime? expires = null)
    {
        var nbf = notBefore ?? DateTime.UtcNow;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, sub.ToString())]),
            IssuedAt = nbf,
            NotBefore = nbf,
            Expires = expires ?? nbf.AddHours(1),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), SecurityAlgorithms.HmacSha256),
        });
    }
}
