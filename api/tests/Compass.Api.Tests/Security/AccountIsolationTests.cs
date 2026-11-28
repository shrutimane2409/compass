using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Compass.Api.Tests.Security;

/// <summary>One student must never be able to read or change another student's saved data.</summary>
[Collection("api")]
public class AccountIsolationTests(ApiFactory api)
{
    [Fact]
    public async Task Another_student_sees_none_of_my_data_in_me_or_export()
    {
        var a = await api.CreateUserAsync();
        var b = await api.CreateUserAsync();
        await SeedPrivateDataAsync(a.Id);

        var clientB = api.ClientFor(b.Id);
        foreach (var path in new[] { "/api/me", "/api/me/export" })
        {
            var body = await clientB.GetStringAsync(path);
            Assert.DoesNotContain("A-private", body);
            Assert.DoesNotContain(a.Email, body);
            Assert.Contains(b.Email, body);
        }
    }

    [Fact]
    public async Task Another_student_cannot_delete_or_overwrite_my_data()
    {
        var a = await api.CreateUserAsync();
        var b = await api.CreateUserAsync();
        await SeedPrivateDataAsync(a.Id);
        var clientB = api.ClientFor(b.Id);

        // B targets the same course and checklist item; each call only ever touches B's own rows.
        Assert.Equal(HttpStatusCode.NoContent, (await clientB.DeleteAsync($"/api/me/saved/{api.CourseId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await clientB.DeleteAsync($"/api/me/applications/{api.CourseId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await clientB.PutAsJsonAsync("/api/me/checklist/visa", new { done = false })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await clientB.PutAsJsonAsync($"/api/me/saved/{api.CourseId}", new { note = "B note" })).StatusCode);

        Assert.Equal("A-private-note", await api.QueryAsync<string>(
            "SELECT note FROM saved_courses WHERE user_id = @u AND course_id = @c", new { u = a.Id, c = api.CourseId }));
        Assert.Equal("applied", await api.QueryAsync<string>(
            "SELECT status FROM applications WHERE user_id = @u AND course_id = @c", new { u = a.Id, c = api.CourseId }));
        Assert.Equal(1L, await api.QueryAsync<long>(
            "SELECT count(*) FROM checklist_progress WHERE user_id = @u AND item_id = 'visa'", new { u = a.Id }));
    }

    [Fact]
    public void No_account_route_accepts_a_user_id_and_all_require_sign_in()
    {
        var routes = AccountRoutes();
        Assert.NotEmpty(routes);
        foreach (var route in routes)
        {
            Assert.All(route.RoutePattern.Parameters, p => Assert.Contains(p.Name, new[] { "courseId", "itemId" }));
            Assert.NotNull(route.Metadata.GetMetadata<IAuthorizeData>());
        }
    }

    [Fact]
    public async Task Every_account_route_returns_401_without_a_token()
    {
        var client = api.ClientFor();
        foreach (var route in AccountRoutes())
        {
            var path = route.RoutePattern.RawText!
                .Replace("{courseId:int}", api.CourseId.ToString())
                .Replace("{itemId}", "visa");
            foreach (var method in route.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods)
            {
                var request = new HttpRequestMessage(new HttpMethod(method), path);
                if (method is "PUT" or "POST") request.Content = JsonContent.Create(new { });
                var response = await client.SendAsync(request);
                Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, $"{method} {path} returned {(int)response.StatusCode}");
            }
        }
    }

    private List<RouteEndpoint> AccountRoutes()
    {
        api.CreateClient().Dispose(); // starts the host so endpoints are registered
        return api.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api/me") == true)
            .ToList();
    }

    private async Task SeedPrivateDataAsync(Guid userId)
    {
        var client = api.ClientFor(userId);
        (await client.PutAsJsonAsync($"/api/me/saved/{api.CourseId}", new { note = "A-private-note" })).EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync($"/api/me/applications/{api.CourseId}",
            new { status = "applied", documents = new Dictionary<string, bool> { ["sop"] = true }, notes = "A-private-notes" })).EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync("/api/me/checklist/visa", new { done = true })).EnsureSuccessStatusCode();
    }
}
