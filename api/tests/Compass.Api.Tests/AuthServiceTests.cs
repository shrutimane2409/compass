using Compass.Api.Infrastructure;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Compass.Api.Tests;

public class AuthServiceTests
{
    private static readonly SymmetricSecurityKey Key = new(Encoding.UTF8.GetBytes(new string('k', 40)));

    [Fact]
    public async Task Issued_session_validates_and_carries_the_user_id()
    {
        var auth = new AuthService("client-id", Key, TimeProvider.System);
        var userId = Guid.NewGuid();
        var token = auth.IssueSession(userId);

        var result = await new JsonWebTokenHandler { MapInboundClaims = false }
            .ValidateTokenAsync(token, AuthService.SessionValidation(Key));

        Assert.True(result.IsValid);
        Assert.Equal(userId.ToString(), result.Claims["sub"]);
    }

    [Fact]
    public async Task Session_signed_with_another_key_is_rejected()
    {
        var token = new AuthService("client-id", Key, TimeProvider.System).IssueSession(Guid.NewGuid());
        var other = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(new string('x', 40)));
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, AuthService.SessionValidation(other));
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Sign_in_is_disabled_without_a_client_id_and_rejects_tokens()
    {
        var auth = new AuthService(null, Key, TimeProvider.System);
        Assert.False(auth.Enabled);
        Assert.Null(await auth.VerifyGoogleTokenAsync("anything", CancellationToken.None));
    }

    [Fact]
    public void Short_secret_is_refused()
    {
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:JwtSecret"] = "too-short" }).Build();
        Assert.Throws<InvalidOperationException>(() => AuthService.SigningKey(config));
    }
}
