using System.Net;
using System.Net.Http.Json;

namespace Compass.Api.Tests.Security;

[Collection("api")]
public class AccountDeletionTests(ApiFactory api)
{
    [Fact]
    public async Task Deleting_an_account_removes_every_row_and_retires_the_token()
    {
        var a = await api.CreateUserAsync();
        var client = api.ClientFor(a.Id);
        (await client.PutAsJsonAsync($"/api/me/saved/{api.CourseId}", new { note = "n" })).EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync($"/api/me/applications/{api.CourseId}", new { status = "preparing" })).EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync("/api/me/checklist/visa", new { done = true })).EnsureSuccessStatusCode();
        await api.ExecAsync("INSERT INTO user_profiles (user_id, profile) VALUES (@u, '{}'::jsonb)", new { u = a.Id });

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/me")).StatusCode);

        var remaining = await api.QueryAsync<long>("""
            SELECT (SELECT count(*) FROM users WHERE id = @u)
                 + (SELECT count(*) FROM user_profiles WHERE user_id = @u)
                 + (SELECT count(*) FROM saved_courses WHERE user_id = @u)
                 + (SELECT count(*) FROM applications WHERE user_id = @u)
                 + (SELECT count(*) FROM checklist_progress WHERE user_id = @u)
            """, new { u = a.Id });
        Assert.Equal(0L, remaining);

        // The 30-day token is still cryptographically valid, but it no longer opens anything.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me/export")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync($"/api/me/saved/{api.CourseId}", new { note = "n" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync("/api/me/checklist/visa", new { done = true })).StatusCode);
    }
}
