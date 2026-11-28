using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace Compass.Api.Tests.Security;

[Collection("api")]
public class AdminTests(ApiFactory api)
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("wrong-key")]
    [InlineData("test-only-admin-key")]            // prefix of the real key
    [InlineData("test-only-admin-key-0123456789x")] // real key plus one character
    public async Task Stats_need_the_exact_admin_key(string? key)
    {
        var client = api.ClientFor();
        if (key is not null) client.DefaultRequestHeaders.Add("X-Admin-Key", key);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/stats")).StatusCode);
    }

    [Fact]
    public async Task Stats_open_with_the_admin_key()
    {
        var client = api.ClientFor();
        client.DefaultRequestHeaders.Add("X-Admin-Key", ApiFactory.AdminKey);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/stats")).StatusCode);
    }

    [Fact]
    public async Task A_signed_in_student_is_not_an_admin()
    {
        var a = await api.CreateUserAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.ClientFor(a.Id).GetAsync("/api/admin/stats")).StatusCode);
    }
}

/// <summary>Limits protect the free LLM quota and the database. They are per client IP.</summary>
[Collection("api")]
public class RateLimitTests(ApiFactory api)
{
    [Fact]
    public async Task Chat_allows_10_messages_a_minute_per_client_then_429()
    {
        var ip = api.NextIp();
        var client = api.ClientFor(ip: ip);
        for (var i = 0; i < 10; i++)
            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await Chat(client)).StatusCode); // 503: AI is offline in tests
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Chat(client)).StatusCode);

        // Another student is unaffected.
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await Chat(api.ClientFor())).StatusCode);
    }

    [Fact]
    public async Task General_api_allows_60_requests_a_minute_per_client_then_429()
    {
        var client = api.ClientFor();
        for (var i = 0; i < 60; i++)
            Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsync("/api/warmup", null)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsync("/api/warmup", null)).StatusCode);
    }

    [Fact]
    public async Task Feedback_allows_5_per_10_minutes_then_429()
    {
        var client = api.ClientFor();
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/events/helpful", new { helpful = true, context = "chat" })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await client.PostAsJsonAsync("/api/events/helpful", new { helpful = true, context = "chat" })).StatusCode);
    }

    [Fact]
    public async Task Oversized_chat_message_is_refused_before_reaching_the_AI()
    {
        var body = new StringContent($$"""{"message":"{{new string('a', 40_000)}}"}""", Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await api.ClientFor().PostAsync("/api/chat", body)).StatusCode);
    }

    private static Task<HttpResponseMessage> Chat(HttpClient client) =>
        client.PostAsync("/api/chat", new StringContent("""{"message":"hi"}""", Encoding.UTF8, "application/json"));
}
