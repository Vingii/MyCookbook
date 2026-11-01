using System.Net.Http.Json;
using MyCookbook.Api.Dto;
using MyCookbook.Test.Common;

namespace MyCookbook.Test.Api;

// The X-Dev-User header lets E2E tests run as their own user, so their cleanup never deletes the
// developer's DEV_AUTO_LOGIN ("devuser") recipes.
public class DevAutoLoginTests : IDisposable
{
    private readonly TestingWebAppFactory<Program> _factory;
    private readonly HttpClient _client;

    private record MeResponse(string Username, bool IsAuthenticated);

    public DevAutoLoginTests()
    {
        _factory = new TestingWebAppFactory<Program>();
        _client = _factory.CreateClient();
    }

    public void Dispose() => _factory.Dispose();

    private HttpRequestMessage AsUser(HttpMethod method, string url, string? user, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (user != null) request.Headers.Add("X-Dev-User", user);
        if (body != null) request.Content = JsonContent.Create(body);
        return request;
    }

    [Fact]
    public async Task WithoutHeader_SignsInAsDevAutoLoginUser()
    {
        var me = await _client.GetFromJsonAsync<MeResponse>("/api/auth/me");

        Assert.Equal("devuser", me!.Username);
        Assert.True(me.IsAuthenticated);
    }

    [Fact]
    public async Task WithHeader_SignsInAsThatUser()
    {
        var response = await _client.SendAsync(AsUser(HttpMethod.Get, "/api/auth/me", "e2e-user"));
        var me = await response.Content.ReadFromJsonAsync<MeResponse>();

        Assert.Equal("e2e-user", me!.Username);
    }

    [Fact]
    public async Task HeaderUser_HasSeparateRecipes()
    {
        await _client.SendAsync(AsUser(HttpMethod.Post, "/api/recipes", "e2e-user", new { name = "E2E only", servings = 2 }));

        var e2eRecipes = await (await _client.SendAsync(AsUser(HttpMethod.Get, "/api/recipes", "e2e-user")))
            .Content.ReadFromJsonAsync<List<RecipeDto>>();
        var devRecipes = await _client.GetFromJsonAsync<List<RecipeDto>>("/api/recipes");

        Assert.Equal(["E2E only"], e2eRecipes!.Select(r => r.Name));
        Assert.Empty(devRecipes!);
    }
}
